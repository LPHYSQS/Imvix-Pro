using System;
using System.Collections.Generic;

namespace ImvixPro.Models
{
    public sealed class TaskAnalysisSnapshot
    {
        public TaskAnalysisSnapshot(
            IReadOnlyList<string> activeWarnings,
            IReadOnlyList<string> conversionPlanHighlights,
            string formatRecommendationText,
            string formatRecommendationReasonText,
            string originalSizeSummaryText,
            string estimatedSizeSummaryText,
            string estimateDisclaimerText)
        {
            ActiveWarnings = activeWarnings ?? throw new ArgumentNullException(nameof(activeWarnings));
            ConversionPlanHighlights = conversionPlanHighlights ?? throw new ArgumentNullException(nameof(conversionPlanHighlights));
            FormatRecommendationText = formatRecommendationText ?? string.Empty;
            FormatRecommendationReasonText = formatRecommendationReasonText ?? string.Empty;
            OriginalSizeSummaryText = originalSizeSummaryText ?? string.Empty;
            EstimatedSizeSummaryText = estimatedSizeSummaryText ?? string.Empty;
            EstimateDisclaimerText = estimateDisclaimerText ?? string.Empty;
        }

        public IReadOnlyList<string> ActiveWarnings { get; }

        public IReadOnlyList<string> ConversionPlanHighlights { get; }

        public string FormatRecommendationText { get; }

        public string FormatRecommendationReasonText { get; }

        public string OriginalSizeSummaryText { get; }

        public string EstimatedSizeSummaryText { get; }

        public string EstimateDisclaimerText { get; }
    }
}
