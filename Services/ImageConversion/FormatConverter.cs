using Avalonia.Media.Imaging;
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
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    internal static class FormatConverter
    {
        private static readonly PdfExportService PdfExportService = new();

        internal static string ConvertPdfInput(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            object reservationGate,
            HashSet<string> reservedDestinations,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            if (!AppServices.PdfRenderService.TryReadDocumentInfo(inputPath, out var documentInfo, out var error))
            {
                throw new InvalidOperationException(error ?? "Unable to read PDF document.");
            }

            if (options.OutputFormat == OutputImageFormat.Pdf)
            {
                var exportMode = ImageConversionService.ResolvePdfDocumentExportMode(documentInfo.PageCount, options);
                if (exportMode == PdfDocumentExportMode.SplitSinglePages)
                {
                    var splitFolder = ImageConversionService.ReservePdfDerivedFolder(inputPath, outputFolder, options, index, reservationGate, reservedDestinations);
                    Directory.CreateDirectory(splitFolder);
                    ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                    ConvertPdfToSinglePageDocuments(inputPath, splitFolder, documentInfo, onWorkItemCompleted, pauseController, cancellationToken);
                    return splitFolder;
                }

                var destinationPath = ImageConversionService.BuildDestinationPath(
                    inputPath,
                    outputFolder,
                    options,
                    index,
                    reservationGate,
                    reservedDestinations);

                var selectedPageIndex = ImageConversionService.ResolvePdfPageIndex(inputPath, documentInfo.PageCount, options);
                var selectedRange = ImageConversionService.ResolvePdfPageRange(inputPath, documentInfo.PageCount, options);
                var pdfBytes = PdfExportService.ExportPdfDocument(
                    inputPath,
                    exportMode,
                    selectedPageIndex,
                    selectedRange,
                    documentInfo.PageCount);

                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                File.WriteAllBytes(destinationPath, pdfBytes);
                return outputFolder;
            }

            if (documentInfo.PageCount > 1 &&
                ImageConversionService.ResolvePdfImageExportMode(documentInfo.PageCount, options) == PdfImageExportMode.AllPages)
            {
                var pagesFolder = ImageConversionService.ReservePdfDerivedFolder(inputPath, outputFolder, options, index, reservationGate, reservedDestinations);
                Directory.CreateDirectory(pagesFolder);
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                ConvertPdfToImages(
                    inputPath,
                    pagesFolder,
                    options,
                    documentInfo,
                    onWorkItemCompleted,
                    pauseController,
                    cancellationToken);
                return pagesFolder;
            }

            var destination = ImageConversionService.BuildDestinationPath(
                inputPath,
                outputFolder,
                options,
                index,
                reservationGate,
                reservedDestinations);

            var pageIndex = ImageConversionService.ResolvePdfPageIndex(inputPath, documentInfo.PageCount, options);
            ConvertPdfPageToOutput(inputPath, pageIndex, destination, options, documentInfo, pauseController, cancellationToken);
            return outputFolder;
        }

        private static void ConvertPdfToImages(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            PdfDocumentInfo documentInfo,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            for (var pageIndex = 0; pageIndex < documentInfo.PageCount; pageIndex++)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                var destinationPath = Path.Combine(outputFolder, $"page_{pageIndex + 1}{ImageConversionService.GetFileExtension(options.OutputFormat)}");

                ConvertPdfPageToOutput(inputPath, pageIndex, destinationPath, options, documentInfo, pauseController, cancellationToken);
                onWorkItemCompleted?.Invoke();
            }
        }

        private static void ConvertPdfToSinglePageDocuments(
            string inputPath,
            string outputFolder,
            PdfDocumentInfo documentInfo,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            for (var pageIndex = 0; pageIndex < documentInfo.PageCount; pageIndex++)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                var pdfBytes = PdfExportService.ExportPdfDocument(
                    inputPath,
                    PdfDocumentExportMode.CurrentPage,
                    pageIndex,
                    new PdfPageRangeSelection(pageIndex, pageIndex),
                    documentInfo.PageCount);

                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                var destinationPath = Path.Combine(outputFolder, $"page_{pageIndex + 1}.pdf");
                File.WriteAllBytes(destinationPath, pdfBytes);
                onWorkItemCompleted?.Invoke();
            }
        }

        private static void ConvertPdfPageToOutput(
            string inputPath,
            int pageIndex,
            string destinationPath,
            ConversionOptions options,
            PdfDocumentInfo documentInfo,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var pageBitmap = RenderPdfPageBitmapForOutput(inputPath, pageIndex, options, documentInfo);
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var preparedBitmap = ImageSaver.CreatePreparedBitmap(pageBitmap, options);

            if (options.OutputFormat == OutputImageFormat.Svg)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                ImageSaver.ConvertBitmapToSvg(preparedBitmap, destinationPath);
                return;
            }

            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
        }

        private static SKBitmap RenderPdfPageBitmapForOutput(
            string inputPath,
            int pageIndex,
            ConversionOptions options,
            PdfDocumentInfo documentInfo)
        {
            var (targetWidth, _) = ImageSaver.CalculateTargetDimensions(
                Math.Max(1, documentInfo.FirstPageWidth),
                Math.Max(1, documentInfo.FirstPageHeight),
                options);

            var minimumWidth = Math.Max(targetWidth, Math.Max(1, documentInfo.FirstPageWidth * 2));
            return AppServices.PdfRenderService.RenderPageForExport(inputPath, pageIndex, minimumWidth);
        }

        internal static void ConvertSourceToPdf(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                options.GifHandlingMode == GifHandlingMode.SpecificFrame)
            {
                var gifSpecificFrameIndex = options.GifSpecificFrameSelections.TryGetValue(inputPath, out var cachedFrameIndex)
                    ? Math.Max(0, cachedFrameIndex)
                    : Math.Max(0, options.GifSpecificFrameIndex);
                try
                {
                    using var frameBitmap = DecodeGifFrame(inputPath, gifSpecificFrameIndex);
                    ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                    using var preparedBitmap = ImageSaver.CreatePreparedBitmap(frameBitmap, options);
                    ConvertBitmapToPdf(preparedBitmap, destinationPath, options, pauseController, cancellationToken);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Fall back to the static image path if per-frame extraction is unavailable.
                }
            }

            var (useBackground, backgroundColor) = ImageSaver.ResolvePdfBackground(inputPath, options);
            using var sourceBitmap = ImageSaver.DecodeToBitmap(inputPath, useBackground, backgroundColor);
            if (sourceBitmap is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var prepared = ImageSaver.CreatePreparedBitmap(sourceBitmap, options);
            ConvertBitmapToPdf(prepared, destinationPath, options, pauseController, cancellationToken);
        }

        internal static void ConvertGifToSpecificFrame(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            int gifSpecificFrameIndex,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var frameBitmap = DecodeGifFrame(inputPath, gifSpecificFrameIndex);
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var preparedBitmap = ImageSaver.CreatePreparedBitmap(frameBitmap, options);

            if (options.OutputFormat == OutputImageFormat.Svg)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                ImageSaver.ConvertBitmapToSvg(preparedBitmap, destinationPath);
                return;
            }

            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
        }

        private static void ConvertBitmapToPdf(
            SKBitmap preparedBitmap,
            string destinationPath,
            ConversionOptions options,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            var jpegPage = CreateRenderedJpegPage(preparedBitmap, ImageSaver.ResolveQuality(options));
            var pdfBytes = PdfExportService.CreatePdfFromJpegs([jpegPage]);
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            File.WriteAllBytes(destinationPath, pdfBytes);
        }

        internal static string ConvertGifToPdfFrames(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            int frameCount,
            object reservationGate,
            HashSet<string> reservedDestinations,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var stream = File.OpenRead(inputPath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            var actualFrameCount = Math.Max(1, codec.FrameInfo.Length);
            if (actualFrameCount <= 1)
            {
                var fallbackDestinationPath = ImageConversionService.BuildDestinationPath(
                    inputPath,
                    outputFolder,
                    options,
                    index,
                    reservationGate,
                    reservedDestinations);
                ConvertSourceToPdf(inputPath, fallbackDestinationPath, options, pauseController, cancellationToken);
                return outputFolder;
            }

            var baseName = ImageConversionService.ResolveGifPdfBaseFolderName(inputPath, options, index);
            var pdfFolder = ImageConversionService.ReserveGifPdfFolder(outputFolder, baseName, reservationGate, reservedDestinations);
            Directory.CreateDirectory(pdfFolder);

            var info = codec.Info;
            for (var frameIndex = 0; frameIndex < actualFrameCount; frameIndex++)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                using var frameBitmap = new SKBitmap(info);
                var decodeOptions = new SKCodecOptions(frameIndex)
                {
                    PriorFrame = -1
                };

                var result = codec.GetPixels(info, frameBitmap.GetPixels(), decodeOptions);
                if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                {
                    throw new InvalidOperationException("Failed to decode GIF frame.");
                }

                using var preparedBitmap = ImageSaver.CreatePreparedBitmap(frameBitmap, options);
                var destinationPath = Path.Combine(pdfFolder, $"frame_{frameIndex + 1}.pdf");
                ConvertBitmapToPdf(preparedBitmap, destinationPath, options, pauseController, cancellationToken);
                onWorkItemCompleted?.Invoke();
            }

            return pdfFolder;
        }

        internal static string ConvertGifSpecificFrameToPdf(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            int frameCount,
            object reservationGate,
            HashSet<string> reservedDestinations,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            var resolvedFrameIndex = ImageConversionService.ResolveGifSpecificFrameIndex(inputPath, frameCount, options);
            var baseName = ImageConversionService.ResolveGifPdfBaseFolderName(inputPath, options, index);
            var pdfFolder = ImageConversionService.ReserveGifPdfFolder(outputFolder, baseName, reservationGate, reservedDestinations);
            Directory.CreateDirectory(pdfFolder);

            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var frameBitmap = DecodeGifFrame(inputPath, resolvedFrameIndex);
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var preparedBitmap = ImageSaver.CreatePreparedBitmap(frameBitmap, options);
            var destinationPath = Path.Combine(pdfFolder, $"frame_{resolvedFrameIndex + 1}.pdf");
            ConvertBitmapToPdf(preparedBitmap, destinationPath, options, pauseController, cancellationToken);
            return pdfFolder;
        }

        private static PdfExportService.RenderedJpegPage CreateRenderedJpegPage(SKBitmap bitmap, int quality)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 60, 100));
            if (data is null)
            {
                throw new InvalidOperationException("Failed to encode JPEG page for PDF output.");
            }

            return new PdfExportService.RenderedJpegPage(data.ToArray(), bitmap.Width, bitmap.Height);
        }

        internal static string ConvertGifToFrames(
            string inputPath,
            string outputFolder,
            ConversionOptions options,
            int index,
            object reservationGate,
            HashSet<string> reservedDestinations,
            Action? onWorkItemCompleted,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var stream = File.OpenRead(inputPath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            var frameInfos = codec.FrameInfo;
            var frameCount = Math.Max(1, frameInfos.Length);
            var baseName = ImageConversionService.BuildBaseName(inputPath, options, index);
            var framesFolder = ImageConversionService.ReserveGifFramesFolder(outputFolder, baseName, reservationGate, reservedDestinations);

            Directory.CreateDirectory(framesFolder);

            var info = codec.Info;
            var extension = ImageConversionService.GetFileExtension(options.OutputFormat);

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                using var frameBitmap = new SKBitmap(info);
                var decodeOptions = new SKCodecOptions(frameIndex)
                {
                    PriorFrame = -1
                };

                var result = codec.GetPixels(info, frameBitmap.GetPixels(), decodeOptions);
                if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                {
                    throw new InvalidOperationException("Failed to decode GIF frame.");
                }

                using var preparedBitmap = ImageSaver.CreatePreparedBitmap(frameBitmap, options);
                var frameName = $"frame_{frameIndex + 1}{extension}";
                var destinationPath = Path.Combine(framesFolder, frameName);

                if (options.OutputFormat == OutputImageFormat.Svg)
                {
                    ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                    ImageSaver.ConvertBitmapToSvg(preparedBitmap, destinationPath);
                }
                else
                {
                    ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                    ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
                }

                onWorkItemCompleted?.Invoke();
            }

            return framesFolder;
        }

        private static SKBitmap DecodeGifFrame(string inputPath, int requestedFrameIndex)
        {
            using var stream = File.OpenRead(inputPath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            var info = codec.Info;
            var frameCount = Math.Max(1, codec.FrameInfo.Length);
            var frameIndex = Math.Clamp(requestedFrameIndex, 0, frameCount - 1);
            var frameBitmap = new SKBitmap(info);
            var decodeOptions = new SKCodecOptions(frameIndex)
            {
                PriorFrame = -1
            };

            var result = codec.GetPixels(info, frameBitmap.GetPixels(), decodeOptions);
            if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            {
                return frameBitmap;
            }

            frameBitmap.Dispose();
            throw new InvalidOperationException("Failed to decode GIF frame.");
        }

        internal static bool TryGetGifFrameCount(string inputPath, out int frameCount)
        {
            frameCount = 1;

            try
            {
                using var stream = File.OpenRead(inputPath);
                using var codec = SKCodec.Create(stream);
                if (codec is null)
                {
                    return false;
                }

                frameCount = Math.Max(1, codec.FrameInfo.Length);
                return true;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(FormatConverter), $"Failed to inspect GIF frame count for '{inputPath}'.", ex);
                frameCount = 1;
                return false;
            }
        }

        internal static void ConvertGifToAnimatedGif(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
            using var stream = File.OpenRead(inputPath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            var frameInfos = codec.FrameInfo;
            var frameCount = Math.Max(1, frameInfos.Length);
            var selectedRange = ResolveGifFrameRange(inputPath, options, frameCount);
            var gifQuality = ImageSaver.ResolveGifQuality(options);
            var shouldApplyGifQuality = gifQuality < 100;

            if (frameCount <= 1)
            {
                using var sourceBitmap = ImageSaver.DecodeToBitmap(inputPath, false, null);
                if (sourceBitmap is null)
                {
                    throw new InvalidOperationException("Failed to decode static GIF.");
                }
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                using var preparedBitmap = ImageSaver.CreatePreparedBitmap(sourceBitmap, options);
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                ImageSaver.SaveBitmap(preparedBitmap, destinationPath, options);
                return;
            }

            var info = codec.Info;
            using var firstFrame = new SKBitmap(info);
            var decodeOptions = new SKCodecOptions(0) { PriorFrame = -1 };
            codec.GetPixels(info, firstFrame.GetPixels(), decodeOptions);

            var (targetWidth, targetHeight) = ImageSaver.CalculateTargetDimensions(firstFrame.Width, firstFrame.Height, options);
            var needsResize = targetWidth != firstFrame.Width || targetHeight != firstFrame.Height;
            var isFullFrameRange = selectedRange.StartIndex == 0 && selectedRange.EndIndex == frameCount - 1;

            if (!needsResize && options.ResizeMode == ResizeMode.None && !shouldApplyGifQuality && isFullFrameRange)
            {
                stream.Position = 0;
                ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                using var outputStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.CopyTo(outputStream);
                return;
            }

            ConvertGifToAnimatedGifWithSkia(
                inputPath,
                destinationPath,
                options,
                codec,
                frameInfos,
                frameCount,
                info,
                targetWidth,
                targetHeight,
                gifQuality,
                selectedRange,
                pauseController,
                cancellationToken);
        }

        private static void ConvertGifToAnimatedGifWithSkia(
            string inputPath,
            string destinationPath,
            ConversionOptions options,
            SKCodec codec,
            SKCodecFrameInfo[] frameInfos,
            int frameCount,
            SKImageInfo info,
            int targetWidth,
            int targetHeight,
            int gifQuality,
            GifFrameRangeSelection selectedRange,
            ConversionPauseController? pauseController,
            CancellationToken cancellationToken)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"Imvix_Gif_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var shouldQuantize = ImageSaver.TryCreateGifQuantizationTable(gifQuality, out var quantizationTable);

            try
            {
                var framePaths = new List<string>();
                var durations = new List<int>();
                var frameDurations = BuildGifFrameDurationsMs(inputPath, frameInfos, frameCount);

                using var accumulatedBitmap = new SKBitmap(info);
                using var accumulatedCanvas = new SKCanvas(accumulatedBitmap);

                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                    var frameInfo = frameInfos[frameIndex];
                    var disposeMethod = frameInfo.DisposalMethod;

                    using var frameBitmap = new SKBitmap(info);
                    var frameDecodeOptions = new SKCodecOptions(frameIndex);
                    var result = codec.GetPixels(info, frameBitmap.GetPixels(), frameDecodeOptions);

                    if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                    {
                        continue;
                    }

                    switch (disposeMethod)
                    {
                        case SKCodecAnimationDisposalMethod.RestorePrevious:
                            break;
                        case SKCodecAnimationDisposalMethod.RestoreBackgroundColor:
                            accumulatedCanvas.Clear(SKColors.Transparent);
                            accumulatedCanvas.Flush();
                            break;
                        default:
                            break;
                    }

                    accumulatedCanvas.DrawBitmap(frameBitmap, 0, 0);
                    accumulatedCanvas.Flush();

                    using var snapshot = SKImage.FromBitmap(accumulatedBitmap);
                    using var preparedFrame = ResizeImage(snapshot, targetWidth, targetHeight);
                    using var quantizedFrame = shouldQuantize && quantizationTable is not null
                        ? ImageSaver.QuantizeGifColors(preparedFrame, quantizationTable)
                        : null;
                    var frameOutputBitmap = quantizedFrame ?? preparedFrame;
                    if (frameIndex >= selectedRange.StartIndex && frameIndex <= selectedRange.EndIndex)
                    {
                        var framePath = Path.Combine(tempDir, $"frame_{frameIndex:D4}.png");

                        ImageConversionService.ThrowIfInterrupted(pauseController, cancellationToken);
                        using (var pngData = frameOutputBitmap.Encode(SKEncodedImageFormat.Png, 100))
                        using (var fileStream = File.OpenWrite(framePath))
                        {
                            pngData.SaveTo(fileStream);
                        }

                        framePaths.Add(framePath);

                        if (frameIndex < frameDurations.Count)
                        {
                            durations.Add(frameDurations[frameIndex]);
                        }
                        else
                        {
                            durations.Add(Math.Max(20, frameInfo.Duration));
                        }
                    }

                    if (disposeMethod == SKCodecAnimationDisposalMethod.RestoreBackgroundColor)
                    {
                        accumulatedCanvas.Clear(SKColors.Transparent);
                        accumulatedCanvas.Flush();
                    }
                }

                if (framePaths.Count == 0)
                {
                    throw new InvalidOperationException("No frames could be decoded from the GIF.");
                }

                var loopCount = OperatingSystem.IsWindows() && TryReadGifLoopCount(inputPath, out var originalLoopCount)
                    ? originalLoopCount
                    : (ushort?)null;

                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Animated GIF output is only supported on Windows in this build.");
                }

                MetadataProcessor.CreateAnimatedGifFromFrames(framePaths, durations, destinationPath, loopCount, pauseController, cancellationToken);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch (Exception ex)
                    {
                AppServices.Logger.LogDebug(nameof(FormatConverter), $"Failed to delete temporary GIF directory '{tempDir}'.", ex);
                    }
                }
            }
        }

        private static GifFrameRangeSelection ResolveGifFrameRange(string inputPath, ConversionOptions options, int frameCount)
        {
            if (frameCount <= 1)
            {
                return new GifFrameRangeSelection(0, 0);
            }

            if (!options.GifFrameRanges.TryGetValue(inputPath, out var selection))
            {
                selection = new GifFrameRangeSelection(0, frameCount - 1);
            }

            var maxIndex = frameCount - 1;
            var start = Math.Clamp(selection.StartIndex, 0, maxIndex);
            var end = Math.Clamp(selection.EndIndex, start, maxIndex);
            return new GifFrameRangeSelection(start, end);
        }

        private static List<int> BuildGifFrameDurationsMs(
            string inputPath,
            SKCodecFrameInfo[] frameInfos,
            int frameCount)
        {
            if (OperatingSystem.IsWindows() &&
                TryReadGifFrameDelays(inputPath, frameCount, out var delaysMs))
            {
                return delaysMs;
            }

            var durations = new List<int>(frameCount);
            for (var i = 0; i < frameCount; i++)
            {
                var duration = Math.Max(20, frameInfos[i].Duration);
                durations.Add(duration);
            }

            return durations;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool TryReadGifFrameDelays(string inputPath, int frameCount, out List<int> delaysMs)
        {
            delaysMs = [];
            try
            {
                using var stream = File.OpenRead(inputPath);
                using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

                if (!image.PropertyIdList.Contains(GifPropertyTagFrameDelay))
                {
                    return false;
                }

                var item = image.GetPropertyItem(GifPropertyTagFrameDelay);
                if (item is null)
                {
                    return false;
                }
                var value = item.Value;
                if (value is null || value.Length < 4)
                {
                    return false;
                }

                var available = value.Length / 4;
                if (available <= 0)
                {
                    return false;
                }

                var count = Math.Min(frameCount, available);
                delaysMs = new List<int>(frameCount);

                var lastNonZeroCs = 0;
                for (var i = 0; i < count; i++)
                {
                    var delayCs = BitConverter.ToInt32(value, i * 4);
                    if (delayCs <= 0)
                    {
                        delayCs = lastNonZeroCs > 0 ? lastNonZeroCs : 10;
                    }
                    else
                    {
                        lastNonZeroCs = delayCs;
                    }

                    delaysMs.Add(delayCs * 10);
                }

                var paddingCs = lastNonZeroCs > 0 ? lastNonZeroCs : 10;
                for (var i = count; i < frameCount; i++)
                {
                    delaysMs.Add(paddingCs * 10);
                }

                return delaysMs.Count > 0;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(FormatConverter), $"Failed to read GIF frame delays for '{inputPath}'.", ex);
                delaysMs.Clear();
                return false;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool TryReadGifLoopCount(string inputPath, out ushort loopCount)
        {
            loopCount = 0;

            try
            {
                using var stream = File.OpenRead(inputPath);
                using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

                if (!image.PropertyIdList.Contains(GifPropertyTagLoopCount))
                {
                    return false;
                }

                var item = image.GetPropertyItem(GifPropertyTagLoopCount);
                if (item?.Value is null || item.Value.Length < 2)
                {
                    return false;
                }

                loopCount = BitConverter.ToUInt16(item.Value, 0);
                return true;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(FormatConverter), $"Failed to read GIF loop count for '{inputPath}'.", ex);
                loopCount = 0;
                return false;
            }
        }

        private static SKBitmap ResizeImage(SKImage source, int targetWidth, int targetHeight)
        {
            var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            var result = new SKBitmap(info);
            using var targetCanvas = new SKCanvas(result);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            targetCanvas.Clear(SKColors.Transparent);

            if (source.Width == targetWidth && source.Height == targetHeight)
            {
                targetCanvas.DrawImage(source, 0, 0);
            }
            else
            {
                targetCanvas.DrawImage(source, SKRect.Create(targetWidth, targetHeight), paint);
            }

            targetCanvas.Flush();

            return result;
        }

        private const int GifPropertyTagFrameDelay = 0x5100;
        private const int GifPropertyTagLoopCount = 0x5101;
    }
}
