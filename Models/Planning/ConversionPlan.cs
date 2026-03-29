namespace ImvixPro.Models
{
    public sealed class ConversionPlan
    {
        public ConversionPlan(
            ConversionRuleSummary ruleSummary,
            ConversionDiagnosticsSummary diagnostics,
            int totalEstimatedOutputItems,
            int totalEstimatedWorkItems)
        {
            RuleSummary = ruleSummary ?? throw new System.ArgumentNullException(nameof(ruleSummary));
            Diagnostics = diagnostics ?? throw new System.ArgumentNullException(nameof(diagnostics));
            TotalEstimatedOutputItems = totalEstimatedOutputItems;
            TotalEstimatedWorkItems = totalEstimatedWorkItems;
        }

        public ConversionRuleSummary RuleSummary { get; }

        public ConversionDiagnosticsSummary Diagnostics { get; }

        public bool IsAiRequested => RuleSummary.Ai.IsEnabled;

        public bool UsesAiPreprocessing => RuleSummary.Ai.UsesAiPreprocessing;

        public int TotalInputCount => RuleSummary.Ai.TotalInputCount.GetValueOrDefault();

        public int AiEligibleInputCount => RuleSummary.Ai.EligibleInputCount.GetValueOrDefault();

        public int AiBypassedInputCount => RuleSummary.Ai.BypassedInputCount.GetValueOrDefault();

        public int GifFrameExpansionInputCount => RuleSummary.Expansion.GifFrameExpansionInputCount.GetValueOrDefault();

        public int PdfPageExpansionInputCount => RuleSummary.Expansion.PdfPageExpansionInputCount.GetValueOrDefault();

        public bool ExpandsGifFrames => RuleSummary.Expansion.ExpandsGifFrames;

        public bool ExpandsPdfPages => RuleSummary.Expansion.ExpandsPdfPages;

        public bool HasExpandedOutputs => RuleSummary.Expansion.HasExpandedOutputs;

        public int ForcedBackgroundFillInputCount => RuleSummary.ForcedBackgroundFillInputCount.GetValueOrDefault();

        public bool HasForcedBackgroundFillInputs => RuleSummary.HasForcedBackgroundFillInputs;

        public bool HasHighCompressionRisk => Diagnostics.HasHighCompressionRisk;

        public int LargeGifPdfFrameCount => Diagnostics.LargeGifPdfFrameCount;

        public bool HasLargeGifPdfFrameRisk => Diagnostics.HasLargeGifPdfFrameRisk;

        public int LockedPdfInputCount => Diagnostics.LockedPdfInputCount;

        public bool HasLockedPdfInputs => Diagnostics.HasLockedPdfInputs;

        public int TotalEstimatedOutputItems { get; }

        public int TotalEstimatedWorkItems { get; }

        public bool HasEstimateDisclaimer => Diagnostics.HasEstimateDisclaimer;
    }
}
