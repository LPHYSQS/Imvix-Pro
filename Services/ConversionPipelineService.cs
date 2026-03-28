using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    public sealed class ConversionPipelineService
    {
        private readonly ImageConversionService _imageConversionService;
        private readonly AiImageEnhancementService _aiImageEnhancementService;
        private readonly AppLogger _logger;

        public ConversionPipelineService()
            : this(AppServices.ImageConversionService, AppServices.AiImageEnhancementService, AppServices.Logger)
        {
        }

        internal ConversionPipelineService(
            ImageConversionService imageConversionService,
            AiImageEnhancementService aiImageEnhancementService,
            AppLogger logger)
        {
            _imageConversionService = imageConversionService ?? throw new ArgumentNullException(nameof(imageConversionService));
            _aiImageEnhancementService = aiImageEnhancementService ?? throw new ArgumentNullException(nameof(aiImageEnhancementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ConversionSummary> ConvertAsync(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            IProgress<ConversionProgress>? progress,
            ConversionPauseController? pauseController = null,
            CancellationToken cancellationToken = default)
        {
            if (!options.AiEnhancementEnabled)
            {
                return await _imageConversionService
                    .ConvertAsync(images, options, progress, pauseController, cancellationToken)
                    .ConfigureAwait(false);
            }

            var stopwatch = Stopwatch.StartNew();
            var eligibleCount = AiImageEnhancementService.CountEligible(images);
            var totalConversionWork = images.Sum(image => ImageConversionService.EstimateWorkItemCount(image, options));
            var overallTotalWork = eligibleCount + totalConversionWork;

            AiEnhancementBatchResult? batchResult = null;
            var skippedConversionWork = 0;
            var failedFileCount = 0;

            try
            {
                var aiProgress = new Progress<AiEnhancementProgress>(item =>
                {
                    if (item.IsFileCompleted && item.CountsAsConversionFailure)
                    {
                        skippedConversionWork += 1;
                        failedFileCount += 1;
                    }

                    progress?.Report(new ConversionProgress(
                        item.CompletedCount + skippedConversionWork,
                        overallTotalWork,
                        failedFileCount,
                        images.Count,
                        item.FileName,
                        item.IsFileCompleted ? 1 : 0,
                        1,
                        item.IsFileCompleted,
                        item.Succeeded,
                        item.Error,
                        ConversionStage.AiEnhancement));
                });

                batchResult = await _aiImageEnhancementService
                    .EnhanceAsync(images, options, aiProgress, cancellationToken)
                    .ConfigureAwait(false);

                var filteredImages = images
                    .Where(image => !batchResult.FailedInputs.Contains(image.FilePath))
                    .ToList();

                ConversionSummary conversionSummary;
                if (filteredImages.Count == 0)
                {
                    conversionSummary = new ConversionSummary(0, 0, 0, [], [], TimeSpan.Zero, false);
                }
                else
                {
                    var mappedProgress = new Progress<ConversionProgress>(item =>
                    {
                        progress?.Report(new ConversionProgress(
                            batchResult.EligibleCount + skippedConversionWork + item.ProcessedCount,
                            overallTotalWork,
                            failedFileCount + item.ProcessedFileCount,
                            images.Count,
                            item.FileName,
                            item.CurrentFileProcessedCount,
                            item.CurrentFileTotalCount,
                            item.IsFileCompleted,
                            item.Succeeded,
                            item.Error,
                            item.Stage));
                    });

                    conversionSummary = await _imageConversionService
                        .ConvertAsync(
                            filteredImages,
                            options,
                            mappedProgress,
                            batchResult.InputOverrides,
                            pauseController,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                stopwatch.Stop();

                var failures = batchResult.Failures
                    .Concat(conversionSummary.Failures)
                    .OrderBy(static failure => failure.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var processedCount = conversionSummary.ProcessedCount + batchResult.Failures.Count;
                var successCount = conversionSummary.SuccessCount;

                return new ConversionSummary(
                    images.Count,
                    processedCount,
                    successCount,
                    failures,
                    conversionSummary.OutputDirectories,
                    stopwatch.Elapsed,
                    conversionSummary.WasCanceled);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var failures = batchResult?.Failures ?? [];
                return new ConversionSummary(
                    images.Count,
                    failedFileCount,
                    0,
                    failures,
                    [],
                    stopwatch.Elapsed,
                    wasCanceled: true);
            }
            finally
            {
                if (batchResult is not null && !string.IsNullOrWhiteSpace(batchResult.WorkingDirectory))
                {
                    TryDeleteWorkingDirectory(batchResult.WorkingDirectory);
                }
            }
        }

        private void TryDeleteWorkingDirectory(string path)
        {
            try
            {
                if (System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ConversionPipelineService), $"Failed to delete temporary AI working directory '{path}'.", ex);
            }
        }
    }
}
