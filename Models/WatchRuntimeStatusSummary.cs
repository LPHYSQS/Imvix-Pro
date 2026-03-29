namespace ImvixPro.Models
{
    public enum WatchRuntimeState
    {
        Stopped,
        Waiting,
        Running,
        Processing,
        LastCompletion,
        ValidationError,
        Error
    }

    public sealed record WatchRuntimeStatusSummary(
        WatchRuntimeState State,
        int ProcessedCount,
        int FailureCount,
        string WatchDirectory,
        RuntimeStatusSummary? ActiveConversion = null,
        CompletionSummaryModel? LastCompletion = null,
        string LastItemName = "",
        string DetailMessage = "")
    {
        public bool IsProcessing => State == WatchRuntimeState.Processing && ActiveConversion is not null;

        public string CurrentItemName => ActiveConversion?.CurrentItemName ?? string.Empty;

        public bool HasLastCompletion => State == WatchRuntimeState.LastCompletion && LastCompletion is not null;
    }
}
