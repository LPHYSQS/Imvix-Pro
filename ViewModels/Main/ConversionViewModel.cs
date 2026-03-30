using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly ImageAnalysisService _imageAnalysisService;
        private readonly ConversionPipelineService _conversionPipelineService;
        private readonly ConversionHistoryService _conversionHistoryService;
        private readonly ConversionLogService _conversionLogService;
        private readonly FolderWatchService _folderWatchService;
        private readonly ConversionPauseController _manualPauseController = new();
        private readonly SystemIntegrationService _systemIntegrationService;
        private readonly SemaphoreSlim _watchConfigurationGate = new(1, 1);
        private readonly SemaphoreSlim _watchProcessingGate = new(1, 1);

        private CancellationTokenSource? _manualConversionCancellationSource;
        private CancellationTokenSource? _watchProcessingCancellationSource;
        private int _maxParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4);

        public Func<string, string, string, string, Task<bool>>? ConfirmDialogAsync { get; set; }
        public string HistoryTitleText => T("HistoryTitle");
        public string HistoryEmptyText => T("HistoryEmpty");
        public string PauseText => T("Pause");
        public string ResumeText => T("Resume");
        public string CancelTaskText => T("CancelTask");
        public string FailureLogLabelText => T("FailureLogLabel");
        public string ContinueActionText => T("ContinueAction");
        public string CancelActionText => T("CancelAction");
        public string ClearRecentConversionsText => T("ClearRecentConversions");

        [ObservableProperty]
        private bool isConversionPaused;

        public void LoadRecentConversionHistory()
        {
            HistoryState.Load(T);
        }

        private async Task StartManualConversionCoreAsync()
        {
            if (Images.Count == 0)
            {
                return;
            }

            var warnings = await BuildPreflightWarningsAsync();
            if (warnings.Count > 0 && ConfirmDialogAsync is not null)
            {
                var proceed = await ConfirmDialogAsync(
                    T("ConfirmConversionTitle"),
                    string.Join(Environment.NewLine + Environment.NewLine, warnings),
                    ContinueActionText,
                    CancelActionText);

                if (!proceed)
                {
                    ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateReadyRuntimeStatus());
                    return;
                }
            }

            _manualConversionCancellationSource?.Cancel();
            _manualConversionCancellationSource?.Dispose();
            _manualConversionCancellationSource = new CancellationTokenSource();
            _manualPauseController.Resume();
            IsConversionPaused = false;
            IsConverting = true;
            NotificationState.ResetFailureLog();
            FailedConversions.Clear();
            ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreatePendingRuntimeStatus(Images.Count));

            var snapshot = Images.ToList();
            var options = BuildCurrentConversionOptions();
            var estimate = _imageAnalysisService.Estimate(snapshot, options);

            try
            {
                var progress = new Progress<ConversionProgress>(p =>
                {
                    var nextPercent = p.TotalCount == 0 ? 0 : 100d * p.ProcessedCount / p.TotalCount;
                    var nextRemainingCount = Math.Min(RemainingCount, Math.Max(0, p.TotalFileCount - p.ProcessedFileCount));

                    ApplyManualRuntimeStatus(
                        _conversionStatusSummaryService.CreateProgressRuntimeStatus(
                            p,
                            options,
                            nextRemainingCount,
                            Math.Max(ProgressPercent, nextPercent)));
                });

                var summary = await _conversionPipelineService.ConvertAsync(
                    snapshot,
                    options,
                    progress,
                    _manualPauseController,
                    _manualConversionCancellationSource.Token);

                foreach (var failure in summary.Failures)
                {
                    FailedConversions.Add(failure);
                }

                var logPath = _conversionLogService.WriteFailureLog(summary, options, snapshot, ConversionTriggerSource.Manual);
                var completionFlow = _conversionSummaryCoordinator.BuildCompletionFlow(
                    summary,
                    ConversionTriggerSource.Manual,
                    options.OutputFormat,
                    estimate,
                    logPath,
                    T,
                    includeDialog: true);

                ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateCompletionRuntimeStatus(completionFlow.Summary));
                NotificationState.ApplyCompletionFlow(completionFlow);
                HistoryState.Append(completionFlow.HistoryEntry, T);
                TryOpenOutputFolder(summary.OutputDirectories, summary.SuccessCount > 0);
            }
            catch (OperationCanceledException)
            {
                ApplyManualRuntimeStatus(_manualRuntimeStatus with
                {
                    StatusKey = "StatusCanceled",
                    CurrentItemName = string.Empty,
                    CurrentSubItemIndex = null,
                    CurrentSubItemCount = null
                });
            }
            catch (Exception ex)
            {
                FailedConversions.Add(new ConversionFailure("System", ex.Message));
                ApplyManualRuntimeStatus(_manualRuntimeStatus with
                {
                    StatusKey = "StatusUnexpectedError",
                    CurrentItemName = string.Empty,
                    CurrentSubItemIndex = null,
                    CurrentSubItemCount = null
                });
            }
            finally
            {
                _manualPauseController.Resume();
                _manualConversionCancellationSource?.Dispose();
                _manualConversionCancellationSource = null;
                IsConversionPaused = false;
                IsConverting = false;
                RefreshConversionInsights();
            }
        }

        private ConversionOptions BuildCurrentConversionOptions(bool forWatch = false)
        {
            return _configurationCoordinator.BuildConversionOptions(CaptureConfigurationSnapshot(), forWatch);
        }

        private async Task<List<string>> BuildPreflightWarningsAsync()
        {
            var snapshot = Images.ToList();
            var options = BuildCurrentConversionOptions();
            var plan = await Task.Run(() => _conversionPlanningService.BuildPlan(snapshot, options));
            return EnumeratePlanWarningTexts(plan)
                .Concat(EnumerateRuntimeWarningTexts(includePerformanceHint: false))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private void RefreshConversionInsights()
        {
            var snapshot = Images.ToList();
            var options = BuildCurrentConversionOptions();
            var taskAnalysisSnapshot = BuildTaskAnalysisSnapshot(snapshot, options);
            TaskAnalysisState.Apply(taskAnalysisSnapshot, T);
        }

        private TaskAnalysisSnapshot BuildTaskAnalysisSnapshot(
            IReadOnlyList<ImageItemViewModel> snapshot,
            ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(options);

            var plan = _conversionPlanningService.BuildPlan(snapshot, options);
            var (formatRecommendationText, formatRecommendationReasonText) = BuildFormatRecommendationTexts();
            var (originalSizeSummaryText, estimatedSizeSummaryText) = BuildSizeEstimateSummaryTexts(snapshot, options);

            if (plan.TotalInputCount <= 0)
            {
                return new TaskAnalysisSnapshot(
                    BuildActiveWarnings(plan, options),
                    [],
                    formatRecommendationText,
                    formatRecommendationReasonText,
                    originalSizeSummaryText,
                    estimatedSizeSummaryText,
                    string.Empty);
            }

            var highlights = BuildConversionPlanHighlights(plan, options);

            return new TaskAnalysisSnapshot(
                BuildActiveWarnings(plan, options),
                highlights,
                formatRecommendationText,
                formatRecommendationReasonText,
                originalSizeSummaryText,
                estimatedSizeSummaryText,
                BuildEstimateDisclaimerText(plan.Diagnostics));
        }

        private (string FormatRecommendationText, string FormatRecommendationReasonText) BuildFormatRecommendationTexts()
        {
            if (SelectedImage is null || SelectedImage.IsPdfDocument)
            {
                return (string.Empty, string.Empty);
            }

            var analysis = _imageAnalysisService.Analyze(SelectedImage);
            var recommendedFormats = BuildRecommendationFormatsText(analysis);
            return (
                string.Format(CultureInfo.CurrentCulture, T("RecommendationFormatsTemplate"), recommendedFormats),
                T(GetRecommendationReasonKey(analysis.ContentKind)));
        }

        private (string OriginalSizeSummaryText, string EstimatedSizeSummaryText) BuildSizeEstimateSummaryTexts(
            IReadOnlyList<ImageItemViewModel> snapshot,
            ConversionOptions options)
        {
            var estimate = _imageAnalysisService.Estimate(snapshot, options);
            if (!estimate.IsAvailable)
            {
                return (string.Empty, string.Empty);
            }

            return (
                string.Format(CultureInfo.CurrentCulture, T("OriginalSizeTemplate"), FormatBytes(estimate.OriginalTotalBytes)),
                string.Format(CultureInfo.CurrentCulture, T("EstimatedSizeTemplate"), FormatBytesRange(estimate.EstimatedMinBytes, estimate.EstimatedMaxBytes)));
        }

        private IReadOnlyList<string> BuildConversionPlanHighlights(ConversionPlan plan, ConversionOptions options)
        {
            List<string> highlights = [BuildPipelineSummaryText(plan.RuleSummary, options)];

            if (BuildForcedBackgroundFillText(plan.RuleSummary) is { } backgroundFillSummary)
            {
                highlights.Add(backgroundFillSummary);
            }

            if (BuildAiRuleText(plan.RuleSummary) is { } aiRuleSummary)
            {
                highlights.Add(aiRuleSummary);
            }

            if (BuildGifExpansionSummaryText(plan.RuleSummary) is { } gifExpansionSummary)
            {
                highlights.Add(gifExpansionSummary);
            }

            if (BuildPdfExpansionSummaryText(plan.RuleSummary) is { } pdfExpansionSummary)
            {
                highlights.Add(pdfExpansionSummary);
            }

            highlights.Add(plan.TotalEstimatedWorkItems == plan.TotalEstimatedOutputItems
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisWorkloadTemplate"),
                    plan.TotalEstimatedWorkItems)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisWorkloadWithStagesTemplate"),
                    plan.TotalEstimatedOutputItems,
                    plan.TotalEstimatedWorkItems));

            return highlights;
        }

        private IReadOnlyList<string> BuildActiveWarnings(ConversionPlan currentPlan, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(currentPlan);
            ArgumentNullException.ThrowIfNull(options);

            var warningPlan = SelectedImage is null
                ? currentPlan
                : _conversionPlanningService.BuildPlan([SelectedImage], options);

            return EnumeratePlanWarningTexts(warningPlan)
                .Concat(EnumerateRuntimeWarningTexts(includePerformanceHint: true))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private void ApplyManualRuntimeStatus(RuntimeStatusSummary status)
        {
            ArgumentNullException.ThrowIfNull(status);

            _manualRuntimeStatus = status;
            _statusKey = status.StatusKey;
            StatusText = T(status.StatusKey);
            CurrentFile = _conversionTextPresenter.BuildRuntimeCurrentItemText(status, T);
            RemainingCount = Math.Max(0, status.RemainingCount);
            ProgressPercent = Math.Clamp(status.ProgressPercent, 0d, 100d);
        }

        private string BuildRecommendationFormatsText(ImageAnalysisResult analysis)
        {
            var primary = FormatOutputFormat(analysis.PrimaryRecommendation);
            if (analysis.SecondaryRecommendation is null)
            {
                return primary;
            }

            return $"{primary} / {FormatOutputFormat(analysis.SecondaryRecommendation.Value)}";
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

        private static string GetRecommendationReasonKey(ImageContentKind kind)
        {
            return kind switch
            {
                ImageContentKind.Photo => "RecommendationReasonPhoto",
                ImageContentKind.TransparentGraphic => "RecommendationReasonTransparent",
                ImageContentKind.Icon => "RecommendationReasonIcon",
                ImageContentKind.Vector => "RecommendationReasonVector",
                _ => "RecommendationReasonGeneric"
            };
        }

        private void RefreshLocalizedPropertiesV3()
        {
            OnPropertyChanged(nameof(HistoryTitleText));
            OnPropertyChanged(nameof(HistoryEmptyText));
            OnPropertyChanged(nameof(ClearRecentConversionsText));
            OnPropertyChanged(nameof(WatchModeTitleText));
            OnPropertyChanged(nameof(TraySettingsTitleText));
            OnPropertyChanged(nameof(KeepRunningInTrayText));
            OnPropertyChanged(nameof(TrayHintText));
            OnPropertyChanged(nameof(TrayRestoreText));
            OnPropertyChanged(nameof(TrayExitText));
            OnPropertyChanged(nameof(StartupSettingsTitleText));
            OnPropertyChanged(nameof(RunOnStartupText));
            OnPropertyChanged(nameof(CreateDesktopShortcutText));
            OnPropertyChanged(nameof(SaveCurrentWatchProfileText));
            OnPropertyChanged(nameof(WatchModeEnabledText));
            OnPropertyChanged(nameof(WatchInputFolderText));
            OnPropertyChanged(nameof(WatchOutputFolderText));
            OnPropertyChanged(nameof(WatchStatusLabelText));
            OnPropertyChanged(nameof(WatchIncludeSubfoldersText));
            OnPropertyChanged(nameof(WatchHintText));
            OnPropertyChanged(nameof(WatchProfileSummaryText));
            OnPropertyChanged(nameof(HasWatchProfileSummary));
            OnPropertyChanged(nameof(SelectWatchInputFolderDialogTitle));
            OnPropertyChanged(nameof(SelectWatchOutputFolderDialogTitle));
            OnPropertyChanged(nameof(PauseText));
            OnPropertyChanged(nameof(ResumeText));
            OnPropertyChanged(nameof(GifSpecificFramePlaybackButtonText));
            OnPropertyChanged(nameof(CancelTaskText));
            OnPropertyChanged(nameof(FailureLogLabelText));
            OnPropertyChanged(nameof(ContinueActionText));
            OnPropertyChanged(nameof(CancelActionText));
            OnPropertyChanged(nameof(WatchMetricsText));
            RefreshConversionInsights();
            HistoryState.RefreshPresentation(T);
            RefreshWatchStatus();
        }

        [RelayCommand(CanExecute = nameof(CanPauseConversion))]
        private void PauseConversion()
        {
            _manualPauseController.Pause();
            IsConversionPaused = true;
            ApplyManualRuntimeStatus(_manualRuntimeStatus with { StatusKey = "StatusPaused" });
        }

        [RelayCommand(CanExecute = nameof(CanResumeConversion))]
        private void ResumeConversion()
        {
            _manualPauseController.Resume();
            IsConversionPaused = false;
            ApplyManualRuntimeStatus(_manualRuntimeStatus with { StatusKey = "StatusConverting" });
        }

        [RelayCommand(CanExecute = nameof(CanCancelConversion))]
        private void CancelConversion()
        {
            _manualConversionCancellationSource?.Cancel();
        }

        private bool CanPauseConversion()
        {
            return IsConverting && !IsConversionPaused;
        }

        private bool CanResumeConversion()
        {
            return IsConverting && IsConversionPaused;
        }

        private bool CanCancelConversion()
        {
            return IsConverting;
        }

        partial void OnIsConversionPausedChanged(bool value)
        {
            PauseConversionCommand.NotifyCanExecuteChanged();
            ResumeConversionCommand.NotifyCanExecuteChanged();
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
    }
}


