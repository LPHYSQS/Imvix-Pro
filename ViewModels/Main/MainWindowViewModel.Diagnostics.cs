using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private IEnumerable<string> EnumeratePlanWarningTexts(ConversionPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);

            if (BuildForcedBackgroundFillText(plan.RuleSummary) is { } backgroundFillWarning)
            {
                yield return backgroundFillWarning;
            }

            if (plan.HasHighCompressionRisk)
            {
                yield return T("WarningHighCompression");
            }

            if (plan.HasLargeGifPdfFrameRisk)
            {
                yield return string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningGifFramesTooManyTemplate"),
                    plan.LargeGifPdfFrameCount);
            }

            if (BuildAiUnsupportedWarningText(plan.RuleSummary) is { } aiUnsupportedWarning)
            {
                yield return aiUnsupportedWarning;
            }

            if (plan.HasLockedPdfInputs)
            {
                yield return string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningPdfLockedSkipTemplate"),
                    plan.LockedPdfInputCount);
            }
        }

        private IEnumerable<string> EnumerateRuntimeWarningTexts(bool includePerformanceHint)
        {
            if (AiEnhancementEnabled && !string.IsNullOrWhiteSpace(AiEnhancementModelFallbackWarningText))
            {
                yield return AiEnhancementModelFallbackWarningText;
            }

            if (includePerformanceHint && HasAiEnhancementPerformanceHint)
            {
                yield return AiEnhancementPerformanceHintText;
            }
        }

        private static void ReplaceTextCollection(ObservableCollection<string> target, IEnumerable<string> values)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(values);

            target.Clear();

            foreach (var value in values
                         .Where(static value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
            {
                target.Add(value);
            }
        }

        private string BuildEstimateDisclaimerText(ConversionDiagnosticsSummary diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostics);

            if (!diagnostics.HasEstimateDisclaimer)
            {
                return string.Empty;
            }

            List<string> lines = [];

            if (diagnostics.EstimateDisclaimer.IncludesAiScaleAdjustment)
            {
                lines.Add(T("EstimateDisclaimerAiScale"));
            }

            if (diagnostics.EstimateDisclaimer.IncludesExpandedOutputs)
            {
                lines.Add(T("EstimateDisclaimerExpandedOutputs"));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
