using ImageMagick;
using ImvixPro.Models;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    public sealed class AiImageEnhancementService
    {
        private const int OutputScale4x = 4;

        private static readonly HashSet<string> EngineInputExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly HashSet<string> NormalizableRasterExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp",
            ".tif",
            ".tiff",
            ".gif"
        };

        public static bool IsEligible(ImageItemViewModel image)
        {
            if (image.IsPdfDocument || image.IsAnimatedGif)
            {
                return false;
            }

            var extension = Path.GetExtension(image.FilePath);
            return EngineInputExtensions.Contains(extension) || NormalizableRasterExtensions.Contains(extension);
        }

        public static int CountEligible(IReadOnlyList<ImageItemViewModel> images)
        {
            return images.Count(IsEligible);
        }

        public async Task<AiEnhancementBatchResult> EnhanceAsync(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            IProgress<AiEnhancementProgress>? progress,
            CancellationToken cancellationToken)
        {
            var eligibleItems = images
                .Select((image, index) => (image, index))
                .Where(static item => IsEligible(item.image))
                .ToArray();

            if (eligibleItems.Length == 0)
            {
                return AiEnhancementBatchResult.Empty;
            }

            var messages = new AiErrorLocalizer(options.LanguageCode);
            var runtime = ResolveRuntime(options, messages);
            var workingDirectory = Path.Combine(Path.GetTempPath(), "ImvixPro", "AI", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            var normalizedDirectory = Path.Combine(workingDirectory, "normalized");
            var enhancedDirectory = Path.Combine(workingDirectory, "enhanced");
            Directory.CreateDirectory(normalizedDirectory);
            Directory.CreateDirectory(enhancedDirectory);

            var overrides = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var failedInputs = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var failures = new ConcurrentBag<ConversionFailure>();
            var completedCount = 0;
            var forceCpuForRemaining = 0;
            var forceDefaultModelForRemaining = 0;

            var maxWorkerCount = NormalizeOutputScale(options.AiEnhancementScale) > OutputScale4x ? 2 : 4;
            var workerCount = Math.Min(eligibleItems.Length, Math.Clamp(options.MaxDegreeOfParallelism, 1, maxWorkerCount));
            var nextIndex = -1;

            var workers = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(async () =>
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var eligibleIndex = Interlocked.Increment(ref nextIndex);
                        if (eligibleIndex >= eligibleItems.Length)
                        {
                            break;
                        }

                        var (image, imageIndex) = eligibleItems[eligibleIndex];
                        progress?.Report(new AiEnhancementProgress(
                            Volatile.Read(ref completedCount),
                            eligibleItems.Length,
                            image.FileName,
                            isFileCompleted: false,
                            succeeded: false,
                            countsAsConversionFailure: false,
                            error: null));

                        var succeeded = false;
                        string? error = null;
                        try
                        {
                            var outputPath = await EnhanceSingleAsync(
                                image,
                                imageIndex,
                                options,
                                messages,
                                runtime,
                                normalizedDirectory,
                                enhancedDirectory,
                                () => Volatile.Read(ref forceCpuForRemaining) == 1,
                                () => Interlocked.Exchange(ref forceCpuForRemaining, 1),
                                () => Volatile.Read(ref forceDefaultModelForRemaining) == 1,
                                () => Interlocked.Exchange(ref forceDefaultModelForRemaining, 1),
                                cancellationToken).ConfigureAwait(false);

                            overrides[image.FilePath] = outputPath;
                            succeeded = true;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                            failedInputs.TryAdd(image.FilePath, 0);
                            failures.Add(new ConversionFailure(image.FileName, ex.Message));
                        }
                        finally
                        {
                            var completed = Interlocked.Increment(ref completedCount);
                            progress?.Report(new AiEnhancementProgress(
                                completed,
                                eligibleItems.Length,
                                image.FileName,
                                isFileCompleted: true,
                                succeeded,
                                countsAsConversionFailure: !succeeded,
                                error));
                        }
                    }
                }, cancellationToken))
                .ToArray();

            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch
            {
                DeleteDirectorySafe(workingDirectory);
                throw;
            }

            return new AiEnhancementBatchResult(
                workingDirectory,
                new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(failedInputs.Keys, StringComparer.OrdinalIgnoreCase),
                failures
                    .OrderBy(static failure => failure.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                eligibleItems.Length);
        }

        private static async Task<string> EnhanceSingleAsync(
            ImageItemViewModel image,
            int imageIndex,
            ConversionOptions options,
            AiErrorLocalizer messages,
            ResolvedAiRuntime runtime,
            string normalizedDirectory,
            string enhancedDirectory,
            Func<bool> useCpuForRemaining,
            Action markCpuFallback,
            Func<bool> useDefaultModelForRemaining,
            Action markDefaultModelFallback,
            CancellationToken cancellationToken)
        {
            var preparedInputPath = PrepareEngineInput(image, imageIndex, normalizedDirectory, messages);
            var requestedScale = NormalizeOutputScale(options.AiEnhancementScale);
            var inferenceScale = AiEnhancementModelCatalog.ResolveInferenceScale(requestedScale);
            var requiresSecondPass = AiEnhancementModelCatalog.RequiresSecondPass(requestedScale);
            var rawOutputScale = AiEnhancementModelCatalog.ResolveRawOutputScale(requestedScale);
            var outputPath = Path.Combine(enhancedDirectory, $"{imageIndex:D4}.png");
            var rawOutputPath = requestedScale == rawOutputScale
                ? outputPath
                : Path.Combine(enhancedDirectory, $"{imageIndex:D4}.{rawOutputScale}x.png");
            var firstPassOutputPath = requiresSecondPass
                ? Path.Combine(enhancedDirectory, $"{imageIndex:D4}.stage1.4x.png")
                : rawOutputPath;
            var activeModel = useDefaultModelForRemaining() && runtime.DefaultModel.HasValue
                ? runtime.DefaultModel.Value
                : runtime.PrimaryModel;

            if (options.AiExecutionMode == AiExecutionMode.ForceCpu || useCpuForRemaining())
            {
                var (cpuResult, _) = await RunInferencePipelineWithDefaultModelFallbackAsync(
                    runtime,
                    activeModel,
                    preparedInputPath,
                    firstPassOutputPath,
                    rawOutputPath,
                    inferenceScale,
                    requiresSecondPass,
                    useCpu: true,
                    messages,
                    markDefaultModelFallback,
                    cancellationToken).ConfigureAwait(false);
                if (!cpuResult.Succeeded)
                {
                    throw new InvalidOperationException(cpuResult.ErrorMessage);
                }

                FinalizeEnhancedOutput(rawOutputPath, outputPath, image, requestedScale, rawOutputScale, messages);
                return outputPath;
            }

            var (gpuResult, fallbackModel) = await RunInferencePipelineWithDefaultModelFallbackAsync(
                runtime,
                activeModel,
                preparedInputPath,
                firstPassOutputPath,
                rawOutputPath,
                inferenceScale,
                requiresSecondPass,
                useCpu: false,
                messages,
                markDefaultModelFallback,
                cancellationToken).ConfigureAwait(false);
            if (gpuResult.Succeeded)
            {
                FinalizeEnhancedOutput(rawOutputPath, outputPath, image, requestedScale, rawOutputScale, messages);
                return outputPath;
            }

            var (cpuFallbackResult, _) = await RunInferencePipelineWithDefaultModelFallbackAsync(
                runtime,
                fallbackModel,
                preparedInputPath,
                firstPassOutputPath,
                rawOutputPath,
                inferenceScale,
                requiresSecondPass,
                useCpu: true,
                messages,
                markDefaultModelFallback,
                cancellationToken).ConfigureAwait(false);
            if (cpuFallbackResult.Succeeded)
            {
                markCpuFallback();
                FinalizeEnhancedOutput(rawOutputPath, outputPath, image, requestedScale, rawOutputScale, messages);
                return outputPath;
            }

            throw new InvalidOperationException(BuildFallbackFailureMessage(gpuResult.ErrorMessage, cpuFallbackResult.ErrorMessage, messages));
        }

        private static int NormalizeOutputScale(int scale)
        {
            return AiEnhancementModelCatalog.NormalizeRequestedOutputScale(scale);
        }

        private static async Task<(AiProcessResult Result, ResolvedAiModel ActiveModel)> RunInferencePipelineWithDefaultModelFallbackAsync(
            ResolvedAiRuntime runtime,
            ResolvedAiModel model,
            string inputPath,
            string firstPassOutputPath,
            string rawOutputPath,
            int inferenceScale,
            bool requiresSecondPass,
            bool useCpu,
            AiErrorLocalizer messages,
            Action markDefaultModelFallback,
            CancellationToken cancellationToken)
        {
            var (firstPassResult, activeModel) = await RunProcessWithDefaultModelFallbackAsync(
                runtime,
                model,
                inputPath,
                firstPassOutputPath,
                inferenceScale,
                useCpu,
                messages,
                markDefaultModelFallback,
                cancellationToken).ConfigureAwait(false);
            if (!firstPassResult.Succeeded || !requiresSecondPass)
            {
                return (firstPassResult, activeModel);
            }

            try
            {
                var (secondPassResult, secondPassModel) = await RunProcessWithDefaultModelFallbackAsync(
                    runtime,
                    activeModel,
                    firstPassOutputPath,
                    rawOutputPath,
                    inferenceScale,
                    useCpu,
                    messages,
                    markDefaultModelFallback,
                    cancellationToken).ConfigureAwait(false);
                return (secondPassResult, secondPassModel);
            }
            finally
            {
                DeleteFileSafe(firstPassOutputPath);
            }
        }

        private static async Task<(AiProcessResult Result, ResolvedAiModel ActiveModel)> RunProcessWithDefaultModelFallbackAsync(
            ResolvedAiRuntime runtime,
            ResolvedAiModel model,
            string inputPath,
            string outputPath,
            int inferenceScale,
            bool useCpu,
            AiErrorLocalizer messages,
            Action markDefaultModelFallback,
            CancellationToken cancellationToken)
        {
            var result = await RunProcessAsync(
                runtime,
                model,
                inputPath,
                outputPath,
                inferenceScale,
                useCpu,
                messages,
                cancellationToken).ConfigureAwait(false);
            if (result.Succeeded ||
                !runtime.DefaultModel.HasValue ||
                model.Equals(runtime.DefaultModel.Value) ||
                !IsModelLoadFailure(result.ErrorMessage, messages))
            {
                return (result, model);
            }

            markDefaultModelFallback();
            var defaultModel = runtime.DefaultModel.Value;
            var fallbackResult = await RunProcessAsync(
                runtime,
                defaultModel,
                inputPath,
                outputPath,
                inferenceScale,
                useCpu,
                messages,
                cancellationToken).ConfigureAwait(false);
            return (fallbackResult, defaultModel);
        }

        private static void FinalizeEnhancedOutput(
            string rawOutputPath,
            string finalOutputPath,
            ImageItemViewModel image,
            int requestedScale,
            int rawOutputScale,
            AiErrorLocalizer messages)
        {
            try
            {
                if (requestedScale == rawOutputScale)
                {
                    if (!string.Equals(rawOutputPath, finalOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteFileSafe(finalOutputPath);
                        File.Move(rawOutputPath, finalOutputPath, overwrite: true);
                    }

                    return;
                }

                ResizeEnhancedOutput(rawOutputPath, finalOutputPath, image, requestedScale, rawOutputScale, messages);
            }
            catch
            {
                DeleteFileSafe(rawOutputPath);
                DeleteFileSafe(finalOutputPath);
                throw;
            }
        }

        private static void ResizeEnhancedOutput(
            string rawOutputPath,
            string finalOutputPath,
            ImageItemViewModel image,
            int requestedScale,
            int rawOutputScale,
            AiErrorLocalizer messages)
        {
            try
            {
                using var enhancedImage = new MagickImage(rawOutputPath);
                var targetWidth = ResolvePostProcessedDimension(image.PixelWidth, enhancedImage.Width, requestedScale, rawOutputScale);
                var targetHeight = ResolvePostProcessedDimension(image.PixelHeight, enhancedImage.Height, requestedScale, rawOutputScale);

                if (enhancedImage.Width == targetWidth && enhancedImage.Height == targetHeight)
                {
                    DeleteFileSafe(finalOutputPath);
                    File.Move(rawOutputPath, finalOutputPath, overwrite: true);
                    return;
                }

                enhancedImage.FilterType = FilterType.Lanczos;
                enhancedImage.Resize(targetWidth, targetHeight);
                enhancedImage.Format = MagickFormat.Png32;

                DeleteFileSafe(finalOutputPath);
                enhancedImage.Write(finalOutputPath);
                DeleteFileSafe(rawOutputPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MagickException)
            {
                throw new InvalidOperationException(messages.PostResizeFailed(ex.Message), ex);
            }
        }

        private static uint ResolvePostProcessedDimension(
            int sourceDimension,
            uint enhancedDimension,
            int requestedScale,
            int rawOutputScale)
        {
            if (sourceDimension > 0)
            {
                return (uint)Math.Clamp(sourceDimension * (long)requestedScale, 1L, uint.MaxValue);
            }

            var scaledDimension = (long)Math.Round(
                enhancedDimension * (requestedScale / (double)Math.Max(1, rawOutputScale)),
                MidpointRounding.AwayFromZero);
            return (uint)Math.Clamp(scaledDimension, 1L, uint.MaxValue);
        }

        private static bool IsModelLoadFailure(string errorMessage, AiErrorLocalizer messages)
        {
            return string.Equals(errorMessage, messages.ModelLoadFailed(), StringComparison.Ordinal);
        }

        private static string PrepareEngineInput(ImageItemViewModel image, int imageIndex, string normalizedDirectory, AiErrorLocalizer messages)
        {
            var extension = Path.GetExtension(image.FilePath);
            if (EngineInputExtensions.Contains(extension))
            {
                var stagedInputPath = Path.Combine(
                    normalizedDirectory,
                    $"{imageIndex:D4}{extension.ToLowerInvariant()}");
                File.Copy(image.FilePath, stagedInputPath, overwrite: true);
                return stagedInputPath;
            }

            var normalizedPath = Path.Combine(normalizedDirectory, $"{imageIndex:D4}.png");
            using var bitmap = DecodeRasterBitmap(image.FilePath);
            if (bitmap is null)
            {
                throw new InvalidOperationException(messages.NormalizeUnsupported());
            }

            using var encodedImage = SKImage.FromBitmap(bitmap);
            using var data = encodedImage.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
            {
                throw new InvalidOperationException(messages.PrepareTemporaryImage());
            }

            using var stream = File.Open(normalizedPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(stream);
            return normalizedPath;
        }

        private static SKBitmap? DecodeRasterBitmap(string inputPath)
        {
            var decoded = SKBitmap.Decode(inputPath);
            return decoded ?? TryDecodeWithSystemDrawing(inputPath);
        }

        private static SKBitmap? TryDecodeWithSystemDrawing(string inputPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(inputPath);
                using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                using var memory = new MemoryStream();
                image.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                return SKBitmap.Decode(memory);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<AiProcessResult> RunProcessAsync(
            ResolvedAiRuntime runtime,
            ResolvedAiModel model,
            string inputPath,
            string outputPath,
            int scale,
            bool useCpu,
            AiErrorLocalizer messages,
            CancellationToken cancellationToken)
        {
            DeleteFileSafe(outputPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = runtime.ExecutablePath,
                WorkingDirectory = runtime.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add(model.ModelDirectoryPath);
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add(model.ModelNameArgument);
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add(Math.Clamp(scale, 2, 4).ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("png");

            if (useCpu)
            {
                startInfo.ArgumentList.Add("-g");
                startInfo.ArgumentList.Add("-1");
            }

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore cancellation-time kill failures.
                }
            });

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            var success = process.ExitCode == 0 && File.Exists(outputPath);
            if (success)
            {
                return AiProcessResult.Success(outputPath);
            }

            var errorMessage = BuildProcessErrorMessage(standardOutput, standardError, process.ExitCode, useCpu, messages);
            DeleteFileSafe(outputPath);
            return AiProcessResult.Failure(errorMessage);
        }

        private static ResolvedAiRuntime ResolveRuntime(ConversionOptions options, AiErrorLocalizer messages)
        {
            var runtimeRootDirectory = RuntimeAssetLocator.AiEnhancementRootDirectory;
            var engineDirectory = RuntimeAssetLocator.AiEnhancementEngineDirectory;
            var modelsDirectory = RuntimeAssetLocator.AiEnhancementModelsDirectory;
            var executablePath = RuntimeAssetLocator.AiEnhancementExecutablePath;

            if (string.IsNullOrWhiteSpace(AppContext.BaseDirectory) || !Directory.Exists(runtimeRootDirectory))
            {
                throw new InvalidOperationException(messages.RuntimeFolderMissing());
            }

            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException(messages.RuntimeExecutableMissing());
            }

            if (!AiEnhancementModelCatalog.TryResolveModelSelection(
                    modelsDirectory,
                    options.AiEnhancementModel,
                    options.AiEnhancementScale,
                    out var resolvedSelection))
            {
                throw new InvalidOperationException(messages.ModelMissing(AiEnhancementModelCatalog.DefaultRealEsrganModelName));
            }

            ResolvedAiModel? defaultModel = null;
            if (AiEnhancementModelCatalog.TryResolveDefaultModel(modelsDirectory, options.AiEnhancementScale, out var resolvedDefaultModel))
            {
                defaultModel = resolvedDefaultModel;
            }

            return new ResolvedAiRuntime(
                engineDirectory,
                executablePath,
                resolvedSelection.ResolvedModel,
                defaultModel);
        }

        private static string BuildProcessErrorMessage(string standardOutput, string standardError, int exitCode, bool useCpu, AiErrorLocalizer messages)
        {
            var merged = string.Join(
                Environment.NewLine,
                new[] { standardOutput, standardError }
                    .Where(static text => !string.IsNullOrWhiteSpace(text))
                    .Select(static text => text.Trim()));

            if (useCpu && merged.Contains("invalid gpu device", StringComparison.OrdinalIgnoreCase))
            {
                return messages.CpuModeUnsupported();
            }

            if (merged.Contains("fopen", StringComparison.OrdinalIgnoreCase) ||
                merged.Contains("model", StringComparison.OrdinalIgnoreCase) && merged.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                return messages.ModelLoadFailed();
            }

            if (!string.IsNullOrWhiteSpace(merged))
            {
                return merged;
            }

            return messages.ProcessExitCode(exitCode);
        }

        private static string BuildFallbackFailureMessage(string gpuError, string cpuError, AiErrorLocalizer messages)
        {
            if (string.Equals(gpuError, cpuError, StringComparison.Ordinal))
            {
                return gpuError;
            }

            return $"{messages.GpuAttemptFailed(gpuError)}{Environment.NewLine}{messages.CpuFallbackFailed(cpuError)}";
        }

        private static void DeleteFileSafe(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore best-effort cleanup failures.
            }
        }

        private static void DeleteDirectorySafe(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Ignore best-effort cleanup failures.
            }
        }

        private sealed class AiErrorLocalizer
        {
            private readonly LocalizationService _localizationService = new();

            public AiErrorLocalizer(string? languageCode)
            {
                _localizationService.SetLanguage(string.IsNullOrWhiteSpace(languageCode) ? "en-US" : languageCode);
            }

            public string NormalizeUnsupported() => T("AiErrorNormalizeUnsupported");

            public string PrepareTemporaryImage() => T("AiErrorPrepareTemporaryImage");

            public string RuntimeFolderMissing() => T("AiErrorRuntimeFolderMissing");

            public string RuntimeExecutableMissing() => T("AiErrorRuntimeExecutableMissing");

            public string ModelMissing(string modelName) => Format("AiErrorModelMissingTemplate", modelName);

            public string LightweightModelMissing() => T("AiErrorLightweightModelMissing");

            public string CpuModeUnsupported() => T("AiErrorCpuModeUnsupported");

            public string ModelLoadFailed() => T("AiErrorModelLoadFailed");

            public string ProcessExitCode(int exitCode) => Format("AiErrorProcessExitCodeTemplate", exitCode);

            public string GpuAttemptFailed(string message) => Format("AiErrorGpuAttemptFailedTemplate", message);

            public string CpuFallbackFailed(string message) => Format("AiErrorCpuFallbackFailedTemplate", message);

            public string PostResizeFailed(string message) => Format("AiErrorPostResizeFailedTemplate", message);

            private string T(string key) => _localizationService.Translate(key);

            private string Format(string key, params object[] args) =>
                string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        private readonly record struct ResolvedAiRuntime(
            string WorkingDirectory,
            string ExecutablePath,
            ResolvedAiModel PrimaryModel,
            ResolvedAiModel? DefaultModel);

        private readonly record struct AiProcessResult(bool Succeeded, string OutputPath, string ErrorMessage)
        {
            public static AiProcessResult Success(string outputPath) => new(true, outputPath, string.Empty);

            public static AiProcessResult Failure(string errorMessage) => new(false, string.Empty, errorMessage);
        }
    }

    public sealed class AiEnhancementBatchResult
    {
        public static AiEnhancementBatchResult Empty { get; } = new(
            string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [],
            0);

        public AiEnhancementBatchResult(
            string workingDirectory,
            IReadOnlyDictionary<string, string> inputOverrides,
            IReadOnlySet<string> failedInputs,
            IReadOnlyList<ConversionFailure> failures,
            int eligibleCount)
        {
            WorkingDirectory = workingDirectory;
            InputOverrides = inputOverrides;
            FailedInputs = failedInputs;
            Failures = failures;
            EligibleCount = eligibleCount;
        }

        public string WorkingDirectory { get; }

        public IReadOnlyDictionary<string, string> InputOverrides { get; }

        public IReadOnlySet<string> FailedInputs { get; }

        public IReadOnlyList<ConversionFailure> Failures { get; }

        public int EligibleCount { get; }
    }

    public sealed class AiEnhancementProgress
    {
        public AiEnhancementProgress(
            int completedCount,
            int totalCount,
            string fileName,
            bool isFileCompleted,
            bool succeeded,
            bool countsAsConversionFailure,
            string? error)
        {
            CompletedCount = completedCount;
            TotalCount = totalCount;
            FileName = fileName;
            IsFileCompleted = isFileCompleted;
            Succeeded = succeeded;
            CountsAsConversionFailure = countsAsConversionFailure;
            Error = error;
        }

        public int CompletedCount { get; }

        public int TotalCount { get; }

        public string FileName { get; }

        public bool IsFileCompleted { get; }

        public bool Succeeded { get; }

        public bool CountsAsConversionFailure { get; }

        public string? Error { get; }
    }
}
