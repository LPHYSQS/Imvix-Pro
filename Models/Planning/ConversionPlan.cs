namespace ImvixPro.Models
{
    public sealed class ConversionPlan
    {
        public ConversionPlan(
            bool isAiRequested,
            bool usesAiPreprocessing,
            int totalInputCount,
            int aiEligibleInputCount,
            bool expandsGifFrames,
            bool expandsPdfPages,
            int totalEstimatedWorkItems)
        {
            IsAiRequested = isAiRequested;
            UsesAiPreprocessing = usesAiPreprocessing;
            TotalInputCount = totalInputCount;
            AiEligibleInputCount = aiEligibleInputCount;
            ExpandsGifFrames = expandsGifFrames;
            ExpandsPdfPages = expandsPdfPages;
            TotalEstimatedWorkItems = totalEstimatedWorkItems;
        }

        public bool IsAiRequested { get; }

        public bool UsesAiPreprocessing { get; }

        public int TotalInputCount { get; }

        public int AiEligibleInputCount { get; }

        public int AiBypassedInputCount => IsAiRequested
            ? TotalInputCount - AiEligibleInputCount
            : 0;

        public bool ExpandsGifFrames { get; }

        public bool ExpandsPdfPages { get; }

        public int TotalEstimatedWorkItems { get; }
    }
}
