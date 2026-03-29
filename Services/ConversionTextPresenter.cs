using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ImvixPro.Services
{
    public sealed class ConversionTextPresenter
    {
        public string BuildRuntimeCurrentItemText(RuntimeStatusSummary status, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(translate);

            if (!status.HasCurrentItem)
            {
                return translate("NoCurrentFile");
            }

            if (!status.HasCurrentSubItemProgress)
            {
                return status.CurrentItemName;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                translate("GifPdfProgressTemplate"),
                status.CurrentItemName,
                status.CurrentSubItemIndex.GetValueOrDefault(),
                status.CurrentSubItemCount.GetValueOrDefault());
        }

        public string BuildCompletionSummaryText(CompletionSummaryModel summary, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(translate);

            var lines = new List<string>
            {
                $"{translate("SummaryTotal")}: {summary.TotalCount}"
            };

            if (summary.WasCanceled)
            {
                lines.Add($"{translate("SummaryProcessed")}: {summary.ProcessedCount}");
            }

            lines.Add($"{translate("SummarySuccess")}: {summary.SuccessCount}");
            lines.Add($"{translate("SummaryFailed")}: {summary.FailureCount}");

            if (summary.WasCanceled)
            {
                lines.Add($"{translate("SummaryCanceled")}: {translate("YesText")}");
            }

            lines.Add($"{translate("SummaryDuration")}: {FormatDuration(summary.Duration)}");
            return string.Join(Environment.NewLine, lines);
        }

        public string BuildHistorySummaryText(CompletionSummaryModel summary, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(translate);

            var sourceText = summary.Source == ConversionTriggerSource.Manual
                ? translate("HistorySourceManual")
                : translate("HistorySourceWatch");
            var formatText = FormatOutputFormat(summary.OutputFormat);

            return summary.WasCanceled
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    translate("HistorySummaryCanceledTemplate"),
                    sourceText,
                    formatText,
                    summary.ProcessedCount,
                    summary.TotalCount)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    translate("HistorySummaryTemplate"),
                    sourceText,
                    formatText,
                    summary.TotalCount,
                    summary.SuccessCount,
                    summary.FailureCount);
        }

        public string BuildHistoryDetailText(CompletionSummaryModel summary, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(translate);

            return string.Format(
                CultureInfo.CurrentCulture,
                translate("HistoryDetailTemplate"),
                FormatBytes(summary.OriginalTotalBytes),
                FormatBytesRange(summary.EstimatedMinBytes, summary.EstimatedMaxBytes),
                FormatDuration(summary.Duration));
        }

        public string BuildWatchStatusText(WatchRuntimeStatusSummary status, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(translate);

            return status.State switch
            {
                WatchRuntimeState.Stopped => translate("WatchStatusStopped"),
                WatchRuntimeState.Waiting => translate("WatchStatusWaiting"),
                WatchRuntimeState.Running => string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusRunning"),
                    status.WatchDirectory),
                WatchRuntimeState.Processing when status.ActiveConversion is not null => string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusProcessing"),
                    BuildRuntimeCurrentItemText(status.ActiveConversion, translate)),
                WatchRuntimeState.LastCompletion when status.LastCompletion is not null => BuildWatchCompletionText(status, translate),
                WatchRuntimeState.ValidationError => status.DetailMessage,
                WatchRuntimeState.Error when !string.IsNullOrWhiteSpace(status.LastItemName) => string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusSingleFailureTemplate"),
                    status.LastItemName,
                    status.DetailMessage),
                WatchRuntimeState.Error => string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusErrorTemplate"),
                    status.DetailMessage),
                _ => translate("WatchStatusStopped")
            };
        }

        public string BuildWatchMetricsText(WatchRuntimeStatusSummary status, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(translate);

            return string.Format(
                CultureInfo.CurrentCulture,
                translate("WatchMetricsTemplate"),
                status.ProcessedCount,
                status.FailureCount);
        }

        private string BuildWatchCompletionText(WatchRuntimeStatusSummary status, Func<string, string> translate)
        {
            var summary = status.LastCompletion!;

            if (summary.FailureCount > 0)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusSingleFailureTemplate"),
                    status.LastItemName,
                    string.IsNullOrWhiteSpace(status.DetailMessage)
                        ? translate("StatusCompletedWithFailures")
                        : status.DetailMessage);
            }

            if (summary.WasCanceled)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    translate("WatchStatusErrorTemplate"),
                    translate("StatusCanceled"));
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                translate("WatchStatusProcessedTemplate"),
                status.LastItemName);
        }

        private static string FormatOutputFormat(OutputImageFormat format)
        {
            return format switch
            {
                OutputImageFormat.Jpeg => "JPEG",
                OutputImageFormat.Webp => "WEBP",
                OutputImageFormat.Png => "PNG",
                OutputImageFormat.Bmp => "BMP",
                OutputImageFormat.Gif => "GIF",
                OutputImageFormat.Tiff => "TIFF",
                OutputImageFormat.Ico => "ICO",
                OutputImageFormat.Svg => "SVG",
                _ => format.ToString().ToUpperInvariant()
            };
        }

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;

            if (bytes < kb)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, bytes)} B");
            }

            if (bytes < mb)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{bytes / kb:0.0} KB");
            }

            return string.Create(CultureInfo.InvariantCulture, $"{bytes / mb:0.0} MB");
        }

        private static string FormatBytesRange(long minBytes, long maxBytes)
        {
            return $"{FormatBytes(minBytes)} - {FormatBytes(maxBytes)}";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}");
            }

            return string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds / 10:00}");
        }
    }
}
