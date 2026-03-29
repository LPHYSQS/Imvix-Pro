using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        private readonly List<ConversionHistoryEntry> _historyCache = [];

        private CancellationTokenSource? _manualConversionCancellationSource;
        private CancellationTokenSource? _watchProcessingCancellationSource;
        private int _maxParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4);

        public Func<string, string, string, string, Task<bool>>? ConfirmDialogAsync { get; set; }

        public ObservableCollection<string> ActiveWarnings { get; } = [];

        public ObservableCollection<string> ConversionPlanHighlights { get; } = [];

        public ObservableCollection<RecentConversionItem> RecentConversions { get; } = [];

        public bool HasActiveWarnings => ActiveWarnings.Count > 0;

        public bool HasConversionPlanHighlights => ConversionPlanHighlights.Count > 0;

        public bool HasRecentConversions => RecentConversions.Count > 0;

        public bool IsHistoryEmpty => !HasRecentConversions;

        public bool HasFormatRecommendation => !string.IsNullOrWhiteSpace(FormatRecommendationText);

        public bool HasSizeEstimate => !string.IsNullOrWhiteSpace(OriginalSizeSummaryText) || !string.IsNullOrWhiteSpace(EstimatedSizeSummaryText);

        public bool HasEstimateDisclaimer => !string.IsNullOrWhiteSpace(EstimateDisclaimerText);

        public bool HasFailureLog => !string.IsNullOrWhiteSpace(LastFailureLogPath);

        public string FormatRecommendationTitleText => T("FormatRecommendationTitle");
        public string TaskAnalysisTitleText => T("TaskAnalysisTitle");
        public string EstimatedSizeTitleText => T("EstimatedSizeTitle");
        public string WarningsTitleText => T("WarningsTitle");
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
        private string formatRecommendationText = string.Empty;

        [ObservableProperty]
        private string formatRecommendationReasonText = string.Empty;

        [ObservableProperty]
        private string originalSizeSummaryText = string.Empty;

        [ObservableProperty]
        private string estimatedSizeSummaryText = string.Empty;

        [ObservableProperty]
        private string estimateDisclaimerText = string.Empty;

        [ObservableProperty]
        private bool isConversionPaused;

        [ObservableProperty]
        private string lastFailureLogPath = string.Empty;

        public void LoadRecentConversionHistory()
        {
            ReplaceHistory(_conversionHistoryService.Load());
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
            LastFailureLogPath = string.Empty;
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
                var completionSummary = _conversionStatusSummaryService.CreateCompletionSummary(
                    summary,
                    ConversionTriggerSource.Manual,
                    options.OutputFormat,
                    estimate,
                    logPath);

                ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateCompletionRuntimeStatus(completionSummary));
                LastFailureLogPath = completionSummary.FailureLogPath;
                AppendHistory(completionSummary);

                ConversionCompleted?.Invoke(this, completionSummary);
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
            return new ConversionOptions
            {
                OutputFormat = SelectedOutputFormat,
                CompressionMode = SelectedCompressionMode,
                Quality = Quality,
                ResizeMode = SelectedResizeMode,
                ResizeWidth = ResizeWidth,
                ResizeHeight = ResizeHeight,
                ResizePercent = ResizePercent,
                RenameMode = SelectedRenameMode,
                RenamePrefix = RenamePrefix,
                RenameSuffix = RenameSuffix,
                RenameStartNumber = RenameStartNumber,
                RenameNumberDigits = RenameNumberDigits,
                OutputDirectoryRule = forWatch
                    ? OutputDirectoryRule.SpecificFolder
                    : UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                OutputDirectory = forWatch ? WatchOutputDirectory : OutputDirectory,
                AllowOverwrite = AllowOverwrite,
                SvgUseBackground = SvgUseBackground,
                SvgBackgroundColor = EffectiveSvgBackgroundColor,
                IconUseTransparency = IconUseTransparency,
                IconBackgroundColor = EffectiveIconBackgroundColor,
                GifHandlingMode = SelectedGifHandlingMode,
                GifSpecificFrameIndex = SelectedGifSpecificFrameIndex,
                AiEnhancementEnabled = AiEnhancementEnabled,
                AiEnhancementScale = AiEnhancementScale,
                AiEnhancementModel = SelectedAiEnhancementModel,
                AiExecutionMode = SelectedAiExecutionMode,
                LanguageCode = _localizationService.CurrentLanguageCode,
                GifSpecificFrameSelections = new Dictionary<string, int>(_gifSpecificFrameSelections, StringComparer.OrdinalIgnoreCase),
                GifFrameRanges = new Dictionary<string, GifFrameRangeSelection>(_gifTrimSelections, StringComparer.OrdinalIgnoreCase),
                PdfImageExportMode = SelectedPdfImageExportMode,
                PdfDocumentExportMode = SelectedPdfDocumentExportMode,
                PdfPageIndex = SelectedPdfPageIndex,
                PdfPageSelections = new Dictionary<string, int>(_pdfPageSelections, StringComparer.OrdinalIgnoreCase),
                PdfPageRanges = new Dictionary<string, PdfPageRangeSelection>(_pdfPageRanges, StringComparer.OrdinalIgnoreCase),
                PdfUnlockStates = Images
                    .Where(static image => image.IsPdfDocument)
                    .ToDictionary(image => image.FilePath, image => image.IsUnlocked, StringComparer.OrdinalIgnoreCase),
                MaxDegreeOfParallelism = _maxParallelism
            };
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
            var plan = _conversionPlanningService.BuildPlan(snapshot, options);

            if (SelectedImage is null || SelectedImage.IsPdfDocument)
            {
                FormatRecommendationText = string.Empty;
                FormatRecommendationReasonText = string.Empty;
            }
            else
            {
                var analysis = _imageAnalysisService.Analyze(SelectedImage);
                var recommendedFormats = BuildRecommendationFormatsText(analysis);
                FormatRecommendationText = string.Format(CultureInfo.CurrentCulture, T("RecommendationFormatsTemplate"), recommendedFormats);
                FormatRecommendationReasonText = T(GetRecommendationReasonKey(analysis.ContentKind));
            }

            RefreshConversionPlanInsights(plan, options);

            var estimate = _imageAnalysisService.Estimate(snapshot, options);
            if (!estimate.IsAvailable)
            {
                OriginalSizeSummaryText = string.Empty;
                EstimatedSizeSummaryText = string.Empty;
            }
            else
            {
                OriginalSizeSummaryText = string.Format(CultureInfo.CurrentCulture, T("OriginalSizeTemplate"), FormatBytes(estimate.OriginalTotalBytes));
                EstimatedSizeSummaryText = string.Format(CultureInfo.CurrentCulture, T("EstimatedSizeTemplate"), FormatBytesRange(estimate.EstimatedMinBytes, estimate.EstimatedMaxBytes));
            }

            RefreshActiveWarnings(plan, options);
            OnPropertyChanged(nameof(HasFormatRecommendation));
            OnPropertyChanged(nameof(HasSizeEstimate));
        }

        private void RefreshConversionPlanInsights(ConversionPlan plan, ConversionOptions options)
        {
            ConversionPlanHighlights.Clear();

            if (plan.TotalInputCount <= 0)
            {
                EstimateDisclaimerText = string.Empty;
                return;
            }

            ConversionPlanHighlights.Add(BuildPipelineSummaryText(plan.RuleSummary, options));

            if (BuildForcedBackgroundFillText(plan.RuleSummary) is { } backgroundFillSummary)
            {
                ConversionPlanHighlights.Add(backgroundFillSummary);
            }

            if (BuildAiRuleText(plan.RuleSummary) is { } aiRuleSummary)
            {
                ConversionPlanHighlights.Add(aiRuleSummary);
            }

            if (BuildGifExpansionSummaryText(plan.RuleSummary) is { } gifExpansionSummary)
            {
                ConversionPlanHighlights.Add(gifExpansionSummary);
            }

            if (BuildPdfExpansionSummaryText(plan.RuleSummary) is { } pdfExpansionSummary)
            {
                ConversionPlanHighlights.Add(pdfExpansionSummary);
            }

            ConversionPlanHighlights.Add(plan.TotalEstimatedWorkItems == plan.TotalEstimatedOutputItems
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisWorkloadTemplate"),
                    plan.TotalEstimatedWorkItems)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T("TaskAnalysisWorkloadWithStagesTemplate"),
                    plan.TotalEstimatedOutputItems,
                    plan.TotalEstimatedWorkItems));

            EstimateDisclaimerText = BuildEstimateDisclaimerText(plan.Diagnostics);
        }

        private void RefreshActiveWarnings(ConversionPlan currentPlan, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(currentPlan);
            ArgumentNullException.ThrowIfNull(options);

            var warningPlan = SelectedImage is null
                ? currentPlan
                : _conversionPlanningService.BuildPlan([SelectedImage], options);

            ReplaceTextCollection(
                ActiveWarnings,
                EnumeratePlanWarningTexts(warningPlan)
                    .Concat(EnumerateRuntimeWarningTexts(includePerformanceHint: true)));
        }

        private void ApplyManualRuntimeStatus(RuntimeStatusSummary status)
        {
            ArgumentNullException.ThrowIfNull(status);

            _manualRuntimeStatus = status;
            _statusKey = status.StatusKey;
            StatusText = T(status.StatusKey);
            CurrentFile = FormatRuntimeCurrentItemText(status);
            RemainingCount = Math.Max(0, status.RemainingCount);
            ProgressPercent = Math.Clamp(status.ProgressPercent, 0d, 100d);
        }

        private string FormatRuntimeCurrentItemText(RuntimeStatusSummary status)
        {
            if (!status.HasCurrentItem)
            {
                return T("NoCurrentFile");
            }

            if (!status.HasCurrentSubItemProgress)
            {
                return status.CurrentItemName;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("GifPdfProgressTemplate"),
                status.CurrentItemName,
                status.CurrentSubItemIndex.GetValueOrDefault(),
                status.CurrentSubItemCount.GetValueOrDefault());
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

        private void ReplaceHistory(IReadOnlyList<ConversionHistoryEntry> entries)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ReplaceHistoryCore(entries);
                return;
            }

            Dispatcher.UIThread.Post(() => ReplaceHistoryCore(entries));
        }

        private void ReplaceHistoryCore(IReadOnlyList<ConversionHistoryEntry> entries)
        {
            _historyCache.Clear();
            _historyCache.AddRange(entries.OrderByDescending(entry => entry.Timestamp));

            RecentConversions.Clear();
            foreach (var entry in _historyCache)
            {
                RecentConversions.Add(BuildHistoryItem(entry));
            }
        }

        private RecentConversionItem BuildHistoryItem(ConversionHistoryEntry entry)
        {
            var summary = _conversionStatusSummaryService.CreateCompletionSummary(entry);
            var timestampText = entry.Timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
            var sourceText = summary.Source == ConversionTriggerSource.Manual ? T("HistorySourceManual") : T("HistorySourceWatch");
            var formatText = FormatOutputFormat(summary.OutputFormat);
            var summaryText = summary.WasCanceled
                ? string.Format(CultureInfo.CurrentCulture, T("HistorySummaryCanceledTemplate"), sourceText, formatText, summary.ProcessedCount, summary.TotalCount)
                : string.Format(CultureInfo.CurrentCulture, T("HistorySummaryTemplate"), sourceText, formatText, summary.TotalCount, summary.SuccessCount, summary.FailureCount);
            var detailText = string.Format(CultureInfo.CurrentCulture, T("HistoryDetailTemplate"), FormatBytes(summary.OriginalTotalBytes), FormatBytesRange(summary.EstimatedMinBytes, summary.EstimatedMaxBytes), FormatDuration(summary.Duration));

            return new RecentConversionItem
            {
                TimestampText = timestampText,
                SummaryText = summaryText,
                DetailText = detailText,
                FailureLogPath = summary.FailureLogPath
            };
        }

        private void AppendHistory(CompletionSummaryModel summary)
        {
            var updated = _conversionHistoryService.Append(new ConversionHistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
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
            });

            ReplaceHistory(updated);
        }

        private void RefreshLocalizedPropertiesV3()
        {
            OnPropertyChanged(nameof(FormatRecommendationTitleText));
            OnPropertyChanged(nameof(TaskAnalysisTitleText));
            OnPropertyChanged(nameof(EstimatedSizeTitleText));
            OnPropertyChanged(nameof(WarningsTitleText));
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
            ReplaceHistory(_historyCache);
            RefreshWatchStatus();
        }

        [RelayCommand(CanExecute = nameof(CanClearRecentConversions))]
        private void ClearRecentConversions()
        {
            ReplaceHistory(_conversionHistoryService.Clear());
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

        private bool CanClearRecentConversions()
        {
            return RecentConversions.Count > 0;
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

        partial void OnLastFailureLogPathChanged(string value)
        {
            OnPropertyChanged(nameof(HasFailureLog));
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

        private void OnRecentConversionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasRecentConversions));
            OnPropertyChanged(nameof(IsHistoryEmpty));
            ClearRecentConversionsCommand.NotifyCanExecuteChanged();
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


