using ImvixPro.Models;
using System;
using System.Globalization;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private string? BuildForcedBackgroundFillText(ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);

            var forcedInputCount = ruleSummary.ForcedBackgroundFillInputCount;
            if (!forcedInputCount.HasValue || forcedInputCount.Value <= 0)
            {
                return null;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("BackgroundFillPlanTemplate"),
                forcedInputCount.Value);
        }

        private string BuildPipelineSummaryText(ConversionRuleSummary ruleSummary, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);
            ArgumentNullException.ThrowIfNull(options);

            var outputFormat = FormatOutputFormat(options.OutputFormat);

            if (ruleSummary.Ai.UsesAiPreprocessing)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisPipelineAiTemplate"),
                    outputFormat);
            }

            if (ruleSummary.Ai.IsEnabled)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisPipelineAiBypassedTemplate"),
                    outputFormat);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("TaskAnalysisPipelineStandardTemplate"),
                outputFormat);
        }

        private string? BuildAiRuleText(ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);

            var ai = ruleSummary.Ai;
            if (!ai.IsEnabled)
            {
                return null;
            }

            if (!ai.HasKnownCoverage)
            {
                return ai.SkipsUnsupportedInputs
                    ? T("RuleSummaryAiUnsupportedFallback")
                    : null;
            }

            if (ai.EligibleInputCount.GetValueOrDefault() <= 0)
            {
                return T("TaskAnalysisAiNoneEligibleTemplate");
            }

            if (!ai.HasBypassedKnownInputs)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisAiAllEligibleTemplate"),
                    ai.TotalInputCount.GetValueOrDefault());
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("TaskAnalysisAiMixedTemplate"),
                ai.EligibleInputCount.GetValueOrDefault(),
                ai.TotalInputCount.GetValueOrDefault(),
                ai.BypassedInputCount.GetValueOrDefault());
        }

        private string? BuildAiUnsupportedWarningText(ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);

            var bypassedInputCount = ruleSummary.Ai.BypassedInputCount;
            if (!bypassedInputCount.HasValue || bypassedInputCount.Value <= 0)
            {
                return null;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("WarningAiUnsupportedInputsTemplate"),
                bypassedInputCount.Value);
        }

        private string? BuildGifExpansionSummaryText(ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);

            var expansion = ruleSummary.Expansion;
            if (!expansion.ExpandsGifFrames)
            {
                return null;
            }

            return expansion.GifFrameExpansionInputCount.HasValue
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisGifExpansionTemplate"),
                    expansion.GifFrameExpansionInputCount.Value)
                : T("WatchProfileSummaryGifAutoExpand");
        }

        private string? BuildPdfExpansionSummaryText(ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(ruleSummary);

            var expansion = ruleSummary.Expansion;
            if (!expansion.ExpandsPdfPages)
            {
                return null;
            }

            return expansion.PdfPageExpansionInputCount.HasValue
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisPdfExpansionTemplate"),
                    expansion.PdfPageExpansionInputCount.Value)
                : T("WatchProfileSummaryPdfAutoExpand");
        }
    }
}
