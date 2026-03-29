using System;

namespace ImvixPro.Models
{
    public sealed class ConversionDiagnosticsSummary
    {
        public ConversionDiagnosticsSummary(
            bool hasHighCompressionRisk,
            int largeGifPdfFrameCount,
            int lockedPdfInputCount,
            EstimateDisclaimerSummary estimateDisclaimer)
        {
            HasHighCompressionRisk = hasHighCompressionRisk;
            LargeGifPdfFrameCount = Math.Max(0, largeGifPdfFrameCount);
            LockedPdfInputCount = Math.Max(0, lockedPdfInputCount);
            EstimateDisclaimer = estimateDisclaimer ?? throw new ArgumentNullException(nameof(estimateDisclaimer));
        }

        public bool HasHighCompressionRisk { get; }

        public int LargeGifPdfFrameCount { get; }

        public bool HasLargeGifPdfFrameRisk => LargeGifPdfFrameCount > 0;

        public int LockedPdfInputCount { get; }

        public bool HasLockedPdfInputs => LockedPdfInputCount > 0;

        public EstimateDisclaimerSummary EstimateDisclaimer { get; }

        public bool HasEstimateDisclaimer => EstimateDisclaimer.HasAny;
    }

    public sealed class EstimateDisclaimerSummary
    {
        public EstimateDisclaimerSummary(
            bool includesAiScaleAdjustment,
            bool includesExpandedOutputs)
        {
            IncludesAiScaleAdjustment = includesAiScaleAdjustment;
            IncludesExpandedOutputs = includesExpandedOutputs;
        }

        public bool IncludesAiScaleAdjustment { get; }

        public bool IncludesExpandedOutputs { get; }

        public bool HasAny => IncludesAiScaleAdjustment || IncludesExpandedOutputs;
    }
}
