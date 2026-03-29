using ImvixPro.Models;
using System;
using System.IO;

namespace ImvixPro.Services
{
    public sealed class ConversionStatusSummaryService
    {
        public RuntimeStatusSummary CreateReadyRuntimeStatus()
        {
            return new RuntimeStatusSummary("StatusReady", string.Empty, 0, 0);
        }

        public RuntimeStatusSummary CreatePendingRuntimeStatus(int remainingCount)
        {
            return new RuntimeStatusSummary("StatusConverting", string.Empty, Math.Max(0, remainingCount), 0);
        }

        public RuntimeStatusSummary CreateProgressRuntimeStatus(
            ConversionProgress progress,
            ConversionOptions options,
            int remainingCount,
            double progressPercent)
        {
            ArgumentNullException.ThrowIfNull(progress);
            ArgumentNullException.ThrowIfNull(options);

            var usesFrameProgress = ShouldShowGifPdfFrameProgress(progress, options);
            var statusKey = progress.Stage == ConversionStage.AiEnhancement
                ? "StatusAiEnhancing"
                : usesFrameProgress
                ? "StatusProcessingGifFrames"
                : "StatusConverting";

            int? currentSubItemIndex = usesFrameProgress
                ? progress.CurrentFileProcessedCount <= 0
                    ? 1
                    : Math.Min(progress.CurrentFileProcessedCount, progress.CurrentFileTotalCount)
                : null;
            int? currentSubItemCount = usesFrameProgress ? progress.CurrentFileTotalCount : null;

            return new RuntimeStatusSummary(
                statusKey,
                progress.FileName,
                Math.Max(0, remainingCount),
                Math.Clamp(progressPercent, 0d, 100d),
                currentSubItemIndex,
                currentSubItemCount);
        }

        public RuntimeStatusSummary CreateCompletionRuntimeStatus(CompletionSummaryModel summary)
        {
            ArgumentNullException.ThrowIfNull(summary);

            var statusKey = summary.WasCanceled
                ? "StatusCanceled"
                : summary.FailureCount > 0
                ? "StatusCompletedWithFailures"
                : "StatusCompleted";
            var progressPercent = summary.TotalCount == 0
                ? 0
                : Math.Clamp(100d * summary.ProcessedCount / summary.TotalCount, 0d, 100d);

            return new RuntimeStatusSummary(
                statusKey,
                string.Empty,
                summary.RemainingCount,
                progressPercent);
        }

        public CompletionSummaryModel CreateCompletionSummary(
            ConversionSummary summary,
            ConversionTriggerSource source,
            OutputImageFormat outputFormat,
            SizeEstimateResult estimate,
            string? failureLogPath)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(estimate);

            return new CompletionSummaryModel(
                source,
                outputFormat,
                summary.TotalCount,
                summary.ProcessedCount,
                summary.SuccessCount,
                summary.FailureCount,
                summary.Duration,
                summary.WasCanceled,
                estimate.OriginalTotalBytes,
                estimate.EstimatedMinBytes,
                estimate.EstimatedMaxBytes,
                failureLogPath ?? string.Empty);
        }

        public CompletionSummaryModel CreateCompletionSummary(ConversionHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return new CompletionSummaryModel(
                entry.Source,
                entry.OutputFormat,
                entry.TotalCount,
                entry.ProcessedCount,
                entry.SuccessCount,
                entry.FailureCount,
                TimeSpan.FromMilliseconds(Math.Max(0, entry.DurationMilliseconds)),
                entry.WasCanceled,
                entry.OriginalTotalBytes,
                entry.EstimatedMinBytes,
                entry.EstimatedMaxBytes,
                entry.FailureLogPath);
        }

        private static bool ShouldShowGifPdfFrameProgress(ConversionProgress progress, ConversionOptions options)
        {
            return options.OutputFormat == OutputImageFormat.Pdf &&
                   progress.CurrentFileTotalCount > 1 &&
                   Path.GetExtension(progress.FileName).Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }
    }
}
