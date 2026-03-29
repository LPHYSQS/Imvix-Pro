namespace ImvixPro.Models
{
    public sealed record RuntimeStatusSummary(
        string StatusKey,
        string CurrentItemName,
        int RemainingCount,
        double ProgressPercent,
        int? CurrentSubItemIndex = null,
        int? CurrentSubItemCount = null)
    {
        public bool HasCurrentItem => !string.IsNullOrWhiteSpace(CurrentItemName);

        public bool HasCurrentSubItemProgress =>
            CurrentSubItemIndex.HasValue &&
            CurrentSubItemCount.HasValue &&
            CurrentSubItemCount.Value > 1;
    }
}
