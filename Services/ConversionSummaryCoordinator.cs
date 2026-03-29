using ImvixPro.Models;
using System;
using System.Globalization;

namespace ImvixPro.Services
{
    public sealed class ConversionSummaryCoordinator
    {
        private readonly ConversionStatusSummaryService _statusSummaryService;
        private readonly ConversionTextPresenter _textPresenter;

        public ConversionSummaryCoordinator(
            ConversionStatusSummaryService statusSummaryService,
            ConversionTextPresenter textPresenter)
        {
            _statusSummaryService = statusSummaryService ?? throw new ArgumentNullException(nameof(statusSummaryService));
            _textPresenter = textPresenter ?? throw new ArgumentNullException(nameof(textPresenter));
        }

        public ConversionSummaryFlowResult BuildCompletionFlow(
            ConversionSummary summary,
            ConversionTriggerSource source,
            OutputImageFormat outputFormat,
            SizeEstimateResult estimate,
            string? failureLogPath,
            Func<string, string> translate,
            bool includeDialog,
            DateTimeOffset? timestamp = null)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(estimate);

            var completionSummary = _statusSummaryService.CreateCompletionSummary(
                summary,
                source,
                outputFormat,
                estimate,
                failureLogPath);

            return BuildCompletionFlow(completionSummary, translate, includeDialog, timestamp);
        }

        public ConversionSummaryFlowResult BuildCompletionFlow(
            CompletionSummaryModel summary,
            Func<string, string> translate,
            bool includeDialog,
            DateTimeOffset? timestamp = null)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(translate);

            var historyEntry = CreateHistoryEntry(summary, timestamp ?? DateTimeOffset.Now);
            var dialogRequest = includeDialog
                ? CreateDialogRequest(summary, translate)
                : null;

            return new ConversionSummaryFlowResult(summary, historyEntry, dialogRequest);
        }

        public RecentConversionItem CreateHistoryItem(ConversionHistoryEntry entry, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(translate);

            var summary = _statusSummaryService.CreateCompletionSummary(entry);
            var timestampText = entry.Timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

            return new RecentConversionItem
            {
                TimestampText = timestampText,
                SummaryText = _textPresenter.BuildHistorySummaryText(summary, translate),
                DetailText = _textPresenter.BuildHistoryDetailText(summary, translate),
                FailureLogPath = summary.FailureLogPath
            };
        }

        private ConversionHistoryEntry CreateHistoryEntry(CompletionSummaryModel summary, DateTimeOffset timestamp)
        {
            return new ConversionHistoryEntry
            {
                Timestamp = timestamp,
                Source = summary.Source,
                OutputFormat = summary.OutputFormat,
                TotalCount = summary.TotalCount,
                ProcessedCount = summary.ProcessedCount,
                SuccessCount = summary.SuccessCount,
                FailureCount = summary.FailureCount,
                OriginalTotalBytes = summary.OriginalTotalBytes,
                EstimatedMinBytes = summary.EstimatedMinBytes,
                EstimatedMaxBytes = summary.EstimatedMaxBytes,
                DurationMilliseconds = summary.Duration.TotalMilliseconds,
                WasCanceled = summary.WasCanceled,
                FailureLogPath = summary.FailureLogPath
            };
        }

        private ConversionSummaryDialogRequest CreateDialogRequest(
            CompletionSummaryModel summary,
            Func<string, string> translate)
        {
            return new ConversionSummaryDialogRequest(
                translate("ConversionSummaryTitle"),
                _textPresenter.BuildCompletionSummaryText(summary, translate),
                translate("Close"));
        }
    }
}
