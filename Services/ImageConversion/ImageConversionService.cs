﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Avalonia.Media.Imaging;
using ImvixPro.Models;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    public sealed partial class ImageConversionService
    {
        private static readonly PdfExportService PdfExportService = new();
        private static readonly Dictionary<OutputImageFormat, string> Extensions = new()
        {
            [OutputImageFormat.Png] = ".png",
            [OutputImageFormat.Jpeg] = ".jpg",
            [OutputImageFormat.Webp] = ".webp",
            [OutputImageFormat.Bmp] = ".bmp",
            [OutputImageFormat.Gif] = ".gif",
            [OutputImageFormat.Tiff] = ".tiff",
            [OutputImageFormat.Ico] = ".ico",
            [OutputImageFormat.Svg] = ".svg",
            [OutputImageFormat.Pdf] = ".pdf"
        };

        public static bool OutputFormatSupportsTransparency(OutputImageFormat outputFormat)
        {
            return outputFormat is not (OutputImageFormat.Jpeg or OutputImageFormat.Gif or OutputImageFormat.Pdf);
        }

        public static string GetFileExtension(OutputImageFormat outputFormat)
        {
            return Extensions.TryGetValue(outputFormat, out var extension)
                ? extension
                : ".png";
        }

        public static IReadOnlyCollection<string> SupportedInputExtensions { get; } =
        [
            ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".svg", ".pdf", ".psd", ".exe", ".lnk"
        ];

        public static bool TryGetCachedGifPreview(string filePath, int maxWidth, [NotNullWhen(true)] out GifPreviewHandle? handle)
        {
            return ImageLoader.TryGetCachedGifPreview(filePath, maxWidth, out handle);
        }

        public static Task<GifPreviewHandle?> GetOrLoadGifPreviewAsync(string filePath, int maxWidth)
        {
            return ImageLoader.GetOrLoadGifPreviewAsync(filePath, maxWidth);
        }

        public static void WarmGifPreview(string filePath, int maxWidth)
        {
            ImageLoader.WarmGifPreview(filePath, maxWidth);
        }

        public static Bitmap? TryCreatePreview(string filePath, int maxWidth, bool svgUseBackground = false, string? svgBackgroundColor = null)
        {
            return ImageLoader.TryCreatePreview(filePath, maxWidth, svgUseBackground, svgBackgroundColor);
        }

        private readonly record struct ConversionWorkload(ImageItemViewModel Image, int TotalWorkItems);

        public Task<ConversionSummary> ConvertAsync(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            IProgress<ConversionProgress>? progress,
            ConversionPauseController? pauseController = null,
            CancellationToken cancellationToken = default)
        {
            return ConvertAsync(images, options, progress, inputPathOverrides: null, pauseController, cancellationToken);
        }

        internal Task<ConversionSummary> ConvertAsync(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            IProgress<ConversionProgress>? progress,
            IReadOnlyDictionary<string, string>? inputPathOverrides,
            ConversionPauseController? pauseController = null,
            CancellationToken cancellationToken = default)
        {
            return ConvertInternalAsync(images, options, progress, inputPathOverrides, pauseController, cancellationToken);
        }

        public Task<ConversionSummary> ConvertAsync(
            IReadOnlyList<ImageItemViewModel> images,
            OutputImageFormat outputFormat,
            int quality,
            string outputDirectory,
            bool useSourceFolder,
            bool allowOverwrite,
            bool svgUseBackground,
            string svgBackgroundColor,
            IProgress<ConversionProgress>? progress,
            ConversionPauseController? pauseController = null,
            CancellationToken cancellationToken = default)
        {
            var options = new ConversionOptions
            {
                OutputFormat = outputFormat,
                CompressionMode = CompressionMode.Custom,
                Quality = Math.Clamp(quality, 1, 100),
                ResizeMode = ResizeMode.None,
                RenameMode = RenameMode.KeepOriginal,
                OutputDirectoryRule = useSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                OutputDirectory = outputDirectory,
                AllowOverwrite = allowOverwrite,
                SvgUseBackground = svgUseBackground,
                SvgBackgroundColor = string.IsNullOrWhiteSpace(svgBackgroundColor) ? "#FFFFFFFF" : svgBackgroundColor,
                GifHandlingMode = GifHandlingMode.FirstFrame,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
            };

            return ConvertAsync(images, options, progress, pauseController, cancellationToken);
        }

        public Task ExportRasterToPathAsync(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            var normalized = ImageSaver.NormalizeOptions(options);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                ExportRasterToExactPath(inputPath, destinationPath, normalized, cancellationToken);
            }, cancellationToken);
        }

        private static async Task<ConversionSummary> ConvertInternalAsync(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            IProgress<ConversionProgress>? progress,
            IReadOnlyDictionary<string, string>? inputPathOverrides,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            var normalized = ImageSaver.NormalizeOptions(options);
            var stopwatch = Stopwatch.StartNew();
            var workloads = images
                .Select(image => new ConversionWorkload(image, EstimateWorkItemCount(image, normalized)))
                .ToArray();
            var totalWorkCount = workloads.Sum(workload => workload.TotalWorkItems);

            var failures = new ConcurrentBag<ConversionFailure>();
            var outputFolders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reservationGate = new object();

            var successCount = 0;
            var processedWorkCount = 0;
            var processedCount = 0;
            var wasCanceled = false;

            var workerCount = Math.Min(images.Count, Math.Max(1, normalized.MaxDegreeOfParallelism));
            if (workerCount == 0)
            {
                stopwatch.Stop();
                return new ConversionSummary(0, 0, 0, [], [], stopwatch.Elapsed);
            }

            var nextIndex = -1;
            var workers = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(() =>
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pauseController?.WaitIfPaused(cancellationToken);

                        var index = Interlocked.Increment(ref nextIndex);
                        if (index >= images.Count)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        pauseController?.WaitIfPaused(cancellationToken);

                        var workload = workloads[index];
                        var image = workload.Image;
                        var folder = ResolveOutputFolder(image.FilePath, normalized);
                        var actualInputPath = ResolveActualInputPath(image.FilePath, inputPathOverrides);
                        outputFolders.TryAdd(folder, 0);

                        var succeeded = false;
                        string? error = null;
                        var currentFileProcessedCount = 0;
                        var lastReportedWorkCount = Volatile.Read(ref processedWorkCount);

                        progress?.Report(CreateConversionProgress(
                            lastReportedWorkCount,
                            totalWorkCount,
                            Volatile.Read(ref processedCount),
                            images.Count,
                            image.FileName,
                            0,
                            workload.TotalWorkItems,
                            isFileCompleted: false,
                            succeeded: false,
                            error: null));

                        void ReportWorkItemCompleted()
                        {
                            var currentFileProcessed = Interlocked.Increment(ref currentFileProcessedCount);
                            var processedWork = Interlocked.Increment(ref processedWorkCount);
                            lastReportedWorkCount = processedWork;

                            progress?.Report(CreateConversionProgress(
                                processedWork,
                                totalWorkCount,
                                Volatile.Read(ref processedCount),
                                images.Count,
                                image.FileName,
                                currentFileProcessed,
                                workload.TotalWorkItems,
                                isFileCompleted: false,
                                succeeded: true,
                                error: null));
                        }

                        try
                        {
                            if (ShouldSkipLockedPdf(image, normalized))
                            {
                                throw new InvalidOperationException(GetLockedPdfSkipReason(normalized.LanguageCode));
                            }

                            Directory.CreateDirectory(folder);
                            cancellationToken.ThrowIfCancellationRequested();
                            pauseController?.WaitIfPaused(cancellationToken);

                            var actualOutputFolder = ConvertSingle(
                                actualInputPath,
                                image.FilePath,
                                folder,
                                normalized,
                                index,
                                reservationGate,
                                reservedDestinations,
                                ReportWorkItemCompleted,
                                pauseController,
                                cancellationToken);

                            outputFolders.TryAdd(actualOutputFolder, 0);

                            var completedWorkItems = Volatile.Read(ref currentFileProcessedCount);
                            if (completedWorkItems == 0)
                            {
                                completedWorkItems = workload.TotalWorkItems;
                                Interlocked.Exchange(ref currentFileProcessedCount, completedWorkItems);
                                lastReportedWorkCount = Interlocked.Add(ref processedWorkCount, completedWorkItems);
                            }
                            else
                            {
                                lastReportedWorkCount = Volatile.Read(ref processedWorkCount);
                            }

                            Interlocked.Increment(ref successCount);
                            succeeded = true;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                            failures.Add(new ConversionFailure(image.FileName, ex.Message));
                        }
                        finally
                        {
                            if (succeeded || error is not null)
                            {
                                var completedWorkItems = Volatile.Read(ref currentFileProcessedCount);
                                if (!succeeded && error is not null)
                                {
                                    var remainingWorkItems = Math.Max(0, workload.TotalWorkItems - completedWorkItems);
                                    if (remainingWorkItems > 0)
                                    {
                                        completedWorkItems += remainingWorkItems;
                                        Interlocked.Exchange(ref currentFileProcessedCount, completedWorkItems);
                                        lastReportedWorkCount = Interlocked.Add(ref processedWorkCount, remainingWorkItems);
                                    }
                                    else
                                    {
                                        lastReportedWorkCount = Volatile.Read(ref processedWorkCount);
                                    }
                                }
                                else if (completedWorkItems > 0)
                                {
                                    lastReportedWorkCount = Volatile.Read(ref processedWorkCount);
                                }

                                var processedFiles = Interlocked.Increment(ref processedCount);
                                progress?.Report(CreateConversionProgress(
                                    lastReportedWorkCount,
                                    totalWorkCount,
                                    processedFiles,
                                    images.Count,
                                    image.FileName,
                                    Math.Max(completedWorkItems, workload.TotalWorkItems),
                                    workload.TotalWorkItems,
                                    isFileCompleted: true,
                                    succeeded,
                                    error));
                            }
                        }
                    }
                }, cancellationToken))
                .ToArray();

            try
            {
                await Task.WhenAll(workers);
            }
            catch (OperationCanceledException)
            {
                wasCanceled = true;
            }

            stopwatch.Stop();

            var failureList = failures
                .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var folderList = outputFolders.Keys
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ConversionSummary(images.Count, processedCount, successCount, failureList, folderList, stopwatch.Elapsed, wasCanceled);
        }

        internal static void ThrowIfInterrupted(ConversionPauseController? pauseController, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pauseController?.WaitIfPaused(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static void ExportRasterToExactPath(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            CancellationToken cancellationToken)
        {
            ThrowIfInterrupted(null, cancellationToken);

            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Raster preview export does not support PDF sources.");
            }

            if (options.OutputFormat == OutputImageFormat.Pdf)
            {
                FormatConverter.ConvertSourceToPdf(inputPath, destinationPath, options, pauseController: null, cancellationToken);
                return;
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.GifHandlingMode == GifHandlingMode.SpecificFrame &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                var gifSpecificFrameIndex = options.GifSpecificFrameSelections.TryGetValue(inputPath, out var cachedFrameIndex)
                    ? Math.Max(0, cachedFrameIndex)
                    : Math.Max(0, options.GifSpecificFrameIndex);
                FormatConverter.ConvertGifToSpecificFrame(inputPath, destinationPath, options, gifSpecificFrameIndex, pauseController: null, cancellationToken);
                return;
            }

            if (options.OutputFormat == OutputImageFormat.Svg)
            {
                ImageSaver.ConvertToSvg(inputPath, destinationPath, options);
                return;
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.OutputFormat == OutputImageFormat.Gif)
            {
                FormatConverter.ConvertGifToAnimatedGif(inputPath, destinationPath, options, pauseController: null, cancellationToken);
                return;
            }

            var (useBackground, backgroundColor) = ImageSaver.ResolveConfiguredBackground(inputPath, options);
            using var sourceBitmap = ImageSaver.DecodeToBitmap(inputPath, useBackground, backgroundColor);
            if (sourceBitmap is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            ThrowIfInterrupted(null, cancellationToken);
            using var preparedBitmap = ImageSaver.CreatePreparedBitmap(sourceBitmap, options);
            ThrowIfInterrupted(null, cancellationToken);
            ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
        }

        private static string ConvertSingle(
            string inputPath,
            string outputIdentityPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            object reservationGate,
            HashSet<string> reservedDestinations,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ThrowIfInterrupted(pauseController, cancellationToken);
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return FormatConverter.ConvertPdfInput(
                    inputPath,
                    outputFolder,
                    options,
                    index,
                    reservationGate,
                    reservedDestinations,
                    onWorkItemCompleted,
                    pauseController,
                    cancellationToken);
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.OutputFormat == OutputImageFormat.Pdf &&
                FormatConverter.TryGetGifFrameCount(inputPath, out var gifFrameCount) &&
                gifFrameCount > 1)
            {
                if (options.GifHandlingMode == GifHandlingMode.AllFrames)
                {
                    return FormatConverter.ConvertGifToPdfFrames(
                        inputPath,
                        outputFolder,
                        options,
                        index,
                        gifFrameCount,
                        reservationGate,
                        reservedDestinations,
                        onWorkItemCompleted,
                        pauseController,
                        cancellationToken);
                }

                if (options.GifHandlingMode == GifHandlingMode.SpecificFrame)
                {
                    return FormatConverter.ConvertGifSpecificFrameToPdf(
                        inputPath,
                        outputFolder,
                        options,
                        index,
                        gifFrameCount,
                        reservationGate,
                        reservedDestinations,
                        pauseController,
                        cancellationToken);
                }
            }

            if (options.OutputFormat == OutputImageFormat.Pdf)
            {
                var pdfDestinationPath = BuildDestinationPath(
                    outputIdentityPath,
                    outputFolder,
                    options,
                    index,
                    reservationGate,
                    reservedDestinations);
                FormatConverter.ConvertSourceToPdf(inputPath, pdfDestinationPath, options, pauseController, cancellationToken);
                return outputFolder;
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.GifHandlingMode == GifHandlingMode.AllFrames &&
                FormatConverter.TryGetGifFrameCount(inputPath, out var gifFrameCountForImages) &&
                gifFrameCountForImages > 1 &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                return FormatConverter.ConvertGifToFrames(
                    inputPath,
                    outputFolder,
                    options,
                    index,
                    reservationGate,
                    reservedDestinations,
                    onWorkItemCompleted,
                    pauseController,
                    cancellationToken);
            }

            var destinationPath = BuildDestinationPath(
                outputIdentityPath,
                outputFolder,
                options,
                index,
                reservationGate,
                reservedDestinations);

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.GifHandlingMode == GifHandlingMode.SpecificFrame &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                var gifSpecificFrameIndex = options.GifSpecificFrameSelections.TryGetValue(outputIdentityPath, out var cachedFrameIndex)
                    ? Math.Max(0, cachedFrameIndex)
                    : Math.Max(0, options.GifSpecificFrameIndex);
                FormatConverter.ConvertGifToSpecificFrame(inputPath, destinationPath, options, gifSpecificFrameIndex, pauseController, cancellationToken);
                return outputFolder;
            }

            if (options.OutputFormat == OutputImageFormat.Svg)
            {
                ThrowIfInterrupted(pauseController, cancellationToken);
                ImageSaver.ConvertToSvg(inputPath, destinationPath, options);
                return outputFolder;
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.OutputFormat == OutputImageFormat.Gif)
            {
                FormatConverter.ConvertGifToAnimatedGif(inputPath, destinationPath, options, pauseController, cancellationToken);
                return outputFolder;
            }

            var (useBackground, backgroundColor) = ImageSaver.ResolveConfiguredBackground(inputPath, options);

            using var sourceBitmap = ImageSaver.DecodeToBitmap(inputPath, useBackground, backgroundColor);
            if (sourceBitmap is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            ThrowIfInterrupted(pauseController, cancellationToken);
            using var preparedBitmap = ImageSaver.CreatePreparedBitmap(sourceBitmap, options);
            ThrowIfInterrupted(pauseController, cancellationToken);
            ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
            return outputFolder;
        }

        internal static string ReserveGifFramesFolder(
            string outputFolder,
            string baseName,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            return ReserveUniqueFolderPathWithParentheses(outputFolder, baseName, reservationGate, reservedDestinations);
        }

        internal static string ReserveGifPdfFolder(
            string outputFolder,
            string baseName,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            return ReserveUniqueFolderPathWithParentheses(outputFolder, baseName, reservationGate, reservedDestinations);
        }

        internal static string ReservePdfDerivedFolder(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            var baseName = BuildBaseName(inputPath, options, index);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "PDF";
            }

            return ReserveUniqueFolderPathWithParentheses(outputFolder, baseName, reservationGate, reservedDestinations);
        }

        internal static string ResolveGifPdfBaseFolderName(string inputPath, ConversionOptions options, int index)
        {
            var baseName = BuildBaseName(inputPath, options, index);
            if (!string.IsNullOrWhiteSpace(baseName))
            {
                return baseName;
            }

            var originalName = Path.GetFileNameWithoutExtension(inputPath);
            return string.IsNullOrWhiteSpace(originalName) ? "GIF" : originalName;
        }

        private static string ReserveUniqueFolderPath(
            string outputFolder,
            string baseFolderName,
            bool allowOverwrite,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            lock (reservationGate)
            {
                var folderPath = Path.Combine(outputFolder, baseFolderName);
                if (allowOverwrite &&
                    !File.Exists(folderPath) &&
                    !reservedDestinations.Contains(folderPath))
                {
                    reservedDestinations.Add(folderPath);
                    return folderPath;
                }

                var suffix = 1;
                while (File.Exists(folderPath) || Directory.Exists(folderPath) || reservedDestinations.Contains(folderPath))
                {
                    folderPath = Path.Combine(outputFolder, $"{baseFolderName}_{suffix}");
                    suffix++;
                }

                reservedDestinations.Add(folderPath);
                return folderPath;
            }
        }

        private static string ReserveUniqueFolderPathWithParentheses(
            string outputFolder,
            string baseFolderName,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            lock (reservationGate)
            {
                var folderPath = Path.Combine(outputFolder, baseFolderName);
                if (!File.Exists(folderPath) &&
                    !Directory.Exists(folderPath) &&
                    !reservedDestinations.Contains(folderPath))
                {
                    reservedDestinations.Add(folderPath);
                    return folderPath;
                }

                var suffix = 1;
                while (File.Exists(folderPath) || Directory.Exists(folderPath) || reservedDestinations.Contains(folderPath))
                {
                    folderPath = Path.Combine(outputFolder, $"{baseFolderName}({suffix})");
                    suffix++;
                }

                reservedDestinations.Add(folderPath);
                return folderPath;
            }
        }

        private static string ResolveOutputFolder(string inputPath, ConversionOptions options)
        {
            var sourceFolder = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;

            return options.OutputDirectoryRule switch
            {
                OutputDirectoryRule.SourceFolder => sourceFolder,
                OutputDirectoryRule.SpecificFolder =>
                    string.IsNullOrWhiteSpace(options.OutputDirectory) ? sourceFolder : options.OutputDirectory,
                OutputDirectoryRule.AutoCreateResultFolder =>
                    Path.Combine(
                        string.IsNullOrWhiteSpace(options.OutputDirectory) ? sourceFolder : options.OutputDirectory,
                        options.AutoResultFolderName),
                _ => sourceFolder
            };
        }

        internal static string BuildDestinationPath(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            var baseName = BuildBaseName(inputPath, options, index);
            return BuildDestinationPathForBaseName(baseName, outputFolder, options, reservationGate, reservedDestinations);
        }

        private static string BuildDestinationPathForBaseName(
            string baseName,
            string outputFolder,
            ConversionOptions options,
            object reservationGate,
            HashSet<string> reservedDestinations)
        {
            var extension = Extensions[options.OutputFormat];

            lock (reservationGate)
            {
                var destinationPath = Path.Combine(outputFolder, $"{baseName}{extension}");

                if (options.AllowOverwrite && !Directory.Exists(destinationPath))
                {
                    reservedDestinations.Add(destinationPath);
                    return destinationPath;
                }

                var suffix = 1;
                while (File.Exists(destinationPath) || Directory.Exists(destinationPath) || reservedDestinations.Contains(destinationPath))
                {
                    destinationPath = Path.Combine(outputFolder, $"{baseName}({suffix}){extension}");
                    suffix++;
                }

                reservedDestinations.Add(destinationPath);
                return destinationPath;
            }
        }

        internal static SKBitmap? TryDecodeWithSystemDrawing(string inputPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                return TryDecodeWithSystemDrawingCore(inputPath);
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(ImageConversionService), $"System.Drawing fallback decode failed for '{inputPath}'.", ex);
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static SKBitmap? TryDecodeWithSystemDrawingCore(string inputPath)
        {
            using var stream = File.OpenRead(inputPath);
            using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            using var memory = new MemoryStream();
            image.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            return SKBitmap.Decode(memory);
        }

        internal static int EstimateWorkItemCount(ImageItemViewModel image, ConversionOptions options)
        {
            if (image.IsPdfDocument)
            {
                var pageCount = Math.Max(1, image.PdfPageCount);
                if (options.OutputFormat == OutputImageFormat.Pdf)
                {
                    return ResolvePdfDocumentExportMode(pageCount, options) == PdfDocumentExportMode.SplitSinglePages
                        ? pageCount
                        : 1;
                }

                return ResolvePdfImageExportMode(pageCount, options) == PdfImageExportMode.AllPages
                    ? pageCount
                    : 1;
            }

            if (image.IsAnimatedGif &&
                options.GifHandlingMode == GifHandlingMode.AllFrames &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                return Math.Max(1, image.GifFrameCount);
            }

            return 1;
        }

        private static string ResolveActualInputPath(string inputPath, IReadOnlyDictionary<string, string>? inputPathOverrides)
        {
            if (inputPathOverrides is not null &&
                inputPathOverrides.TryGetValue(inputPath, out var overriddenPath) &&
                !string.IsNullOrWhiteSpace(overriddenPath))
            {
                return overriddenPath;
            }

            return inputPath;
        }

        private static bool ShouldSkipLockedPdf(ImageItemViewModel image, ConversionOptions options)
        {
            if (!image.IsPdfDocument)
            {
                return false;
            }

            if (image.NeedsPdfUnlock)
            {
                return true;
            }

            return options.PdfUnlockStates.TryGetValue(image.FilePath, out var isUnlocked) && !isUnlocked;
        }

        private static string GetLockedPdfSkipReason(string languageCode)
        {
            var localization = AppServices.CreateLocalizationService(languageCode);
            var localized = localization.Translate("PdfLockedSkipReason");
            return string.Equals(localized, "PdfLockedSkipReason", StringComparison.Ordinal)
                ? "PDF is locked and was skipped"
                : localized;
        }

        private static ConversionProgress CreateConversionProgress(
            int processedCount,
            int totalCount,
            int processedFileCount,
            int totalFileCount,
            string fileName,
            int currentFileProcessedCount,
            int currentFileTotalCount,
            bool isFileCompleted,
            bool succeeded,
            string? error)
        {
            return new ConversionProgress(
                processedCount,
                totalCount,
                processedFileCount,
                totalFileCount,
                fileName,
                currentFileProcessedCount,
                currentFileTotalCount,
                isFileCompleted,
                succeeded,
                error);
        }

        internal static PdfImageExportMode ResolvePdfImageExportMode(int pageCount, ConversionOptions options)
        {
            if (pageCount <= 1)
            {
                return PdfImageExportMode.CurrentPage;
            }

            return options.PdfImageExportMode;
        }

        internal static PdfDocumentExportMode ResolvePdfDocumentExportMode(int pageCount, ConversionOptions options)
        {
            if (pageCount <= 1)
            {
                return PdfDocumentExportMode.AllPages;
            }

            return options.PdfDocumentExportMode;
        }

        internal static int ResolvePdfPageIndex(string inputPath, int pageCount, ConversionOptions options)
        {
            var maxIndex = Math.Max(0, pageCount - 1);
            var pageIndex = options.PdfPageSelections.TryGetValue(inputPath, out var cachedPageIndex)
                ? cachedPageIndex
                : options.PdfPageIndex;
            return Math.Clamp(pageIndex, 0, maxIndex);
        }

        internal static PdfPageRangeSelection ResolvePdfPageRange(string inputPath, int pageCount, ConversionOptions options)
        {
            var maxIndex = Math.Max(0, pageCount - 1);
            if (!options.PdfPageRanges.TryGetValue(inputPath, out var selection))
            {
                selection = new PdfPageRangeSelection(0, maxIndex);
            }

            return PdfExportService.ClampRange(selection, maxIndex);
        }

        internal static int ResolveGifSpecificFrameIndex(string inputPath, int frameCount, ConversionOptions options)
        {
            var maxIndex = Math.Max(0, frameCount - 1);
            var frameIndex = options.GifSpecificFrameSelections.TryGetValue(inputPath, out var cachedFrameIndex)
                ? cachedFrameIndex
                : options.GifSpecificFrameIndex;
            return Math.Clamp(frameIndex, 0, maxIndex);
        }

        internal static string BuildBaseName(string inputPath, ConversionOptions options, int index)
        {
            var original = Path.GetFileNameWithoutExtension(inputPath);

            return options.RenameMode switch
            {
                RenameMode.AutoNumber => (options.RenameStartNumber + index).ToString($"D{options.RenameNumberDigits}", System.Globalization.CultureInfo.InvariantCulture),
                RenameMode.Prefix => SanitizeNameSegment(options.RenamePrefix) + original,
                RenameMode.Suffix => original + SanitizeNameSegment(options.RenameSuffix),
                _ => original
            };
        }

        private static string SanitizeNameSegment(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var result = text;
            foreach (var invalidChar in invalidChars)
            {
                result = result.Replace(invalidChar.ToString(), string.Empty, StringComparison.Ordinal);
            }

            return result;
        }

        internal static void DisposePreviewFrames(IReadOnlyList<Bitmap> frames)
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }

        internal static Bitmap? CreatePreviewFromBitmap(SKBitmap bitmap, int maxWidth)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
            {
                return null;
            }

            using var memory = new MemoryStream(data.ToArray());
            return Bitmap.DecodeToWidth(memory, maxWidth);
        }

        internal static SKEncodedImageFormat ToSkiaFormat(OutputImageFormat outputFormat)
        {
            return outputFormat switch
            {
                OutputImageFormat.Png => SKEncodedImageFormat.Png,
                OutputImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
                OutputImageFormat.Webp => SKEncodedImageFormat.Webp,
                _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null)
            };
        }

        public sealed class GifPreviewHandle : IDisposable
        {
            private readonly string _cacheKey;
            private readonly ImageLoader.GifPreviewCacheEntry _entry;
            private bool _isDisposed;

            internal GifPreviewHandle(string cacheKey, ImageLoader.GifPreviewCacheEntry entry)
            {
                _cacheKey = cacheKey;
                _entry = entry;
            }

            public IReadOnlyList<Bitmap> Frames => _entry.Frames;

            public IReadOnlyList<TimeSpan> Durations => _entry.Durations;

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                ImageLoader.ReleaseGifPreview(_cacheKey, _entry);
            }
        }
    }
}
