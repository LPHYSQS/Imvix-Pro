namespace ImvixPro.Models
{
    public sealed class ConversionPlan
    {
        public ConversionPlan(
            bool isAiRequested,
            bool usesAiPreprocessing,
            int totalInputCount,
            int aiEligibleInputCount,
            int gifFrameExpansionInputCount,
            int pdfPageExpansionInputCount,
            int forcedBackgroundFillInputCount,
            int totalEstimatedOutputItems,
            int totalEstimatedWorkItems)
        {
            IsAiRequested = isAiRequested;
            UsesAiPreprocessing = usesAiPreprocessing;
            TotalInputCount = totalInputCount;
            AiEligibleInputCount = aiEligibleInputCount;
            GifFrameExpansionInputCount = gifFrameExpansionInputCount;
            PdfPageExpansionInputCount = pdfPageExpansionInputCount;
            ForcedBackgroundFillInputCount = forcedBackgroundFillInputCount;
            TotalEstimatedOutputItems = totalEstimatedOutputItems;
            TotalEstimatedWorkItems = totalEstimatedWorkItems;
        }

        public bool IsAiRequested { get; }

        public bool UsesAiPreprocessing { get; }

        public int TotalInputCount { get; }

        public int AiEligibleInputCount { get; }

        public int AiBypassedInputCount => IsAiRequested
            ? System.Math.Max(0, TotalInputCount - AiEligibleInputCount)
            : 0;

        public int GifFrameExpansionInputCount { get; }

        public int PdfPageExpansionInputCount { get; }

        public bool ExpandsGifFrames => GifFrameExpansionInputCount > 0;

        public bool ExpandsPdfPages => PdfPageExpansionInputCount > 0;

        public bool HasExpandedOutputs => ExpandsGifFrames || ExpandsPdfPages;

        public int ForcedBackgroundFillInputCount { get; }

        public bool HasForcedBackgroundFillInputs => ForcedBackgroundFillInputCount > 0;

        public int TotalEstimatedOutputItems { get; }

        public int TotalEstimatedWorkItems { get; }

        public bool HasEstimateDisclaimer => UsesAiPreprocessing || HasExpandedOutputs;
    }
}
