using System;

namespace ImvixPro.Models
{
    public sealed record CompletionSummaryModel(
        ConversionTriggerSource Source,
        OutputImageFormat OutputFormat,
        int TotalCount,
        int ProcessedCount,
        int SuccessCount,
        int FailureCount,
        TimeSpan Duration,
        bool WasCanceled,
        long OriginalTotalBytes,
        long EstimatedMinBytes,
        long EstimatedMaxBytes,
        string FailureLogPath)
    {
        public int RemainingCount => Math.Max(0, TotalCount - ProcessedCount);

        public bool HasFailureLog => !string.IsNullOrWhiteSpace(FailureLogPath);
    }
}
