using System;

namespace ImvixPro.Models
{
    public sealed class ConversionRuleSummary
    {
        public ConversionRuleSummary(
            AiRuleSummary ai,
            ExpansionRuleSummary expansion,
            int? forcedBackgroundFillInputCount)
        {
            Ai = ai ?? throw new ArgumentNullException(nameof(ai));
            Expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
            ForcedBackgroundFillInputCount = forcedBackgroundFillInputCount;
        }

        public AiRuleSummary Ai { get; }

        public ExpansionRuleSummary Expansion { get; }

        public int? ForcedBackgroundFillInputCount { get; }

        public bool HasForcedBackgroundFillInputs => ForcedBackgroundFillInputCount.GetValueOrDefault() > 0;
    }

    public sealed class AiRuleSummary
    {
        public AiRuleSummary(
            bool isEnabled,
            bool skipsUnsupportedInputs,
            int? totalInputCount = null,
            int? eligibleInputCount = null)
        {
            IsEnabled = isEnabled;
            SkipsUnsupportedInputs = skipsUnsupportedInputs;
            TotalInputCount = totalInputCount;
            EligibleInputCount = eligibleInputCount;
        }

        public bool IsEnabled { get; }

        public bool SkipsUnsupportedInputs { get; }

        public int? TotalInputCount { get; }

        public int? EligibleInputCount { get; }

        public bool HasKnownCoverage => TotalInputCount.HasValue && EligibleInputCount.HasValue;

        public int? BypassedInputCount => HasKnownCoverage
            ? Math.Max(0, TotalInputCount.GetValueOrDefault() - EligibleInputCount.GetValueOrDefault())
            : null;

        public bool UsesAiPreprocessing => IsEnabled && EligibleInputCount.GetValueOrDefault() > 0;

        public bool HasBypassedKnownInputs => BypassedInputCount.GetValueOrDefault() > 0;
    }

    public sealed class ExpansionRuleSummary
    {
        public ExpansionRuleSummary(
            bool expandsGifFrames,
            int? gifFrameExpansionInputCount,
            bool expandsPdfPages,
            int? pdfPageExpansionInputCount)
        {
            ExpandsGifFrames = expandsGifFrames;
            GifFrameExpansionInputCount = gifFrameExpansionInputCount;
            ExpandsPdfPages = expandsPdfPages;
            PdfPageExpansionInputCount = pdfPageExpansionInputCount;
        }

        public bool ExpandsGifFrames { get; }

        public int? GifFrameExpansionInputCount { get; }

        public bool ExpandsPdfPages { get; }

        public int? PdfPageExpansionInputCount { get; }

        public bool HasExpandedOutputs => ExpandsGifFrames || ExpandsPdfPages;
    }
}
