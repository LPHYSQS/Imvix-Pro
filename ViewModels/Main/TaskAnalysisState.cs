using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class TaskAnalysisState : ObservableObject
    {
        public TaskAnalysisState()
        {
            ActiveWarnings.CollectionChanged += OnActiveWarningsCollectionChanged;
            ConversionPlanHighlights.CollectionChanged += OnConversionPlanHighlightsCollectionChanged;
        }

        public ObservableCollection<string> ActiveWarnings { get; } = [];

        public ObservableCollection<string> ConversionPlanHighlights { get; } = [];

        public bool HasActiveWarnings => ActiveWarnings.Count > 0;

        public bool HasConversionPlanHighlights => ConversionPlanHighlights.Count > 0;

        public bool HasFormatRecommendation => !string.IsNullOrWhiteSpace(FormatRecommendationText);

        public bool HasSizeEstimate => !string.IsNullOrWhiteSpace(OriginalSizeSummaryText) || !string.IsNullOrWhiteSpace(EstimatedSizeSummaryText);

        public bool HasEstimateDisclaimer => !string.IsNullOrWhiteSpace(EstimateDisclaimerText);

        [ObservableProperty]
        private string warningsTitleText = string.Empty;

        [ObservableProperty]
        private string taskAnalysisTitleText = string.Empty;

        [ObservableProperty]
        private string formatRecommendationTitleText = string.Empty;

        [ObservableProperty]
        private string estimatedSizeTitleText = string.Empty;

        [ObservableProperty]
        private string formatRecommendationText = string.Empty;

        [ObservableProperty]
        private string formatRecommendationReasonText = string.Empty;

        [ObservableProperty]
        private string originalSizeSummaryText = string.Empty;

        [ObservableProperty]
        private string estimatedSizeSummaryText = string.Empty;

        [ObservableProperty]
        private string estimateDisclaimerText = string.Empty;

        public void Apply(TaskAnalysisSnapshot snapshot, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(translate);

            WarningsTitleText = translate("WarningsTitle");
            TaskAnalysisTitleText = translate("TaskAnalysisTitle");
            FormatRecommendationTitleText = translate("FormatRecommendationTitle");
            EstimatedSizeTitleText = translate("EstimatedSizeTitle");

            ReplaceTextCollection(ActiveWarnings, snapshot.ActiveWarnings);
            ReplaceTextCollection(ConversionPlanHighlights, snapshot.ConversionPlanHighlights);

            FormatRecommendationText = snapshot.FormatRecommendationText;
            FormatRecommendationReasonText = snapshot.FormatRecommendationReasonText;
            OriginalSizeSummaryText = snapshot.OriginalSizeSummaryText;
            EstimatedSizeSummaryText = snapshot.EstimatedSizeSummaryText;
            EstimateDisclaimerText = snapshot.EstimateDisclaimerText;
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

        partial void OnFormatRecommendationTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasFormatRecommendation));
        }

        partial void OnOriginalSizeSummaryTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasSizeEstimate));
        }

        partial void OnEstimatedSizeSummaryTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasSizeEstimate));
        }

        partial void OnEstimateDisclaimerTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasEstimateDisclaimer));
        }

        private void OnActiveWarningsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasActiveWarnings));
        }

        private void OnConversionPlanHighlightsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasConversionPlanHighlights));
        }
    }
}
