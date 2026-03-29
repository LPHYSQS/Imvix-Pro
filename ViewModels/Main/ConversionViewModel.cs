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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private const int GifPdfLargeFrameWarningThreshold = 500;
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

        public ObservableCollection<RecentConversionItem> RecentConversions { get; } = [];

        public bool HasActiveWarnings => ActiveWarnings.Count > 0;

        public bool HasRecentConversions => RecentConversions.Count > 0;

        public bool IsHistoryEmpty => !HasRecentConversions;

        public bool HasFormatRecommendation => !string.IsNullOrWhiteSpace(FormatRecommendationText);

        public bool HasSizeEstimate => !string.IsNullOrWhiteSpace(OriginalSizeSummaryText) || !string.IsNullOrWhiteSpace(EstimatedSizeSummaryText);

        public bool HasFailureLog => !string.IsNullOrWhiteSpace(LastFailureLogPath);

        public string FormatRecommendationTitleText => T("FormatRecommendationTitle");
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
                    SetStatus("StatusReady");
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
            ProgressPercent = 0;
            RemainingCount = Images.Count;
            CurrentFile = T("NoCurrentFile");
            SetStatus("StatusConverting");

            var snapshot = Images.ToList();
            var options = BuildCurrentConversionOptions();
            var estimate = _imageAnalysisService.Estimate(snapshot, options);

            try
            {
                var progress = new Progress<ConversionProgress>(p =>
                {
                    CurrentFile = BuildProgressFileText(p, options);
                    StatusText = p.Stage == ConversionStage.AiEnhancement
                        ? T("StatusAiEnhancing")
                        : ShouldShowGifPdfFrameProgress(p, options)
                        ? T("StatusProcessingGifFrames")
                        : T(_statusKey);
                    RemainingCount = Math.Min(RemainingCount, Math.Max(0, p.TotalFileCount - p.ProcessedFileCount));
                    var nextPercent = p.TotalCount == 0 ? 0 : 100d * p.ProcessedCount / p.TotalCount;
                    ProgressPercent = Math.Max(ProgressPercent, nextPercent);
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

                CurrentFile = summary.WasCanceled
                    ? string.Format(CultureInfo.InvariantCulture, T("TaskSummaryCanceledInlineTemplate"), summary.ProcessedCount, summary.SuccessCount, summary.FailureCount)
                    : string.Format(CultureInfo.InvariantCulture, T("TaskSummaryInlineTemplate"), summary.SuccessCount, summary.FailureCount, FormatDuration(summary.Duration));

                RemainingCount = Math.Max(0, summary.TotalCount - summary.ProcessedCount);

                if (summary.WasCanceled)
                {
                    SetStatus("StatusCanceled");
                }
                else if (summary.FailureCount > 0)
                {
                    SetStatus("StatusCompletedWithFailures");
                }
                else
                {
                    SetStatus("StatusCompleted");
                }

                var logPath = _conversionLogService.WriteFailureLog(summary, options, snapshot, ConversionTriggerSource.Manual);
                LastFailureLogPath = logPath ?? string.Empty;
                AppendHistory(summary, options, estimate, logPath, ConversionTriggerSource.Manual);

                ConversionCompleted?.Invoke(this, summary);
                TryOpenOutputFolder(summary.OutputDirectories, summary.SuccessCount > 0);
            }
            catch (OperationCanceledException)
            {
                SetStatus("StatusCanceled");
            }
            catch (Exception ex)
            {
                FailedConversions.Add(new ConversionFailure("System", ex.Message));
                SetStatus("StatusUnexpectedError");
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

        private static void ApplyWatchContentAwareOptions(ImageItemViewModel item, ConversionOptions options)
        {
            if (item.IsAnimatedGif &&
                item.GifFrameCount > 1 &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                options.GifHandlingMode = GifHandlingMode.AllFrames;
                options.GifSpecificFrameIndex = 0;
                options.GifSpecificFrameSelections.Clear();
            }

            if (item.IsPdfDocument &&
                item.PdfPageCount > 1 &&
                options.OutputFormat != OutputImageFormat.Pdf)
            {
                options.PdfImageExportMode = PdfImageExportMode.AllPages;
                options.PdfPageIndex = 0;
                options.PdfPageSelections.Clear();
            }
        }

        private async Task<List<string>> BuildPreflightWarningsAsync()
        {
            var warnings = new List<string>();
            var options = BuildCurrentConversionOptions();
            var plan = _conversionPlanningService.BuildPlan(Images.ToList(), options);

            if (SelectedOutputFormat == OutputImageFormat.Jpeg)
            {
                var hasTransparency = await Task.Run(() => _imageAnalysisService.ContainsTransparency(Images.ToList()));
                if (hasTransparency)
                {
                    warnings.Add(T("WarningTransparencyLoss"));
                }
            }

            if (IsHighCompressionSelection())
            {
                warnings.Add(T("WarningHighCompression"));
            }

            var gifPdfFrameCount = GetLargeGifPdfFrameCount(Images.ToList());
            if (gifPdfFrameCount > 0)
            {
                warnings.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningGifFramesTooManyTemplate"),
                    gifPdfFrameCount));
            }

            var unsupportedAiInputCount = plan.AiBypassedInputCount;
            if (unsupportedAiInputCount > 0)
            {
                warnings.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningAiUnsupportedInputsTemplate"),
                    unsupportedAiInputCount));
            }

            if (AiEnhancementEnabled && !string.IsNullOrWhiteSpace(AiEnhancementModelFallbackWarningText))
            {
                warnings.Add(AiEnhancementModelFallbackWarningText);
            }

            var lockedPdfCount = Images.Count(image => image.NeedsPdfUnlock);
            if (lockedPdfCount > 0)
            {
                warnings.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningPdfLockedSkipTemplate"),
                    lockedPdfCount));
            }

            return warnings.Distinct(StringComparer.Ordinal).ToList();
        }

        private void RefreshConversionInsights()
        {
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

            var estimate = _imageAnalysisService.Estimate(Images.ToList(), BuildCurrentConversionOptions());
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

            RefreshActiveWarnings();
            OnPropertyChanged(nameof(HasFormatRecommendation));
            OnPropertyChanged(nameof(HasSizeEstimate));
        }

        private void RefreshActiveWarnings()
        {
            ActiveWarnings.Clear();

            if (SelectedImage is not null &&
                SelectedOutputFormat == OutputImageFormat.Jpeg &&
                _imageAnalysisService.HasTransparency(SelectedImage))
            {
                ActiveWarnings.Add(T("WarningTransparencyLoss"));
            }

            if (IsHighCompressionSelection())
            {
                ActiveWarnings.Add(T("WarningHighCompression"));
            }

            var gifPdfFrameCount = GetLargeGifPdfFrameCount(SelectedImage is null
                ? []
                : [SelectedImage]);
            if (gifPdfFrameCount > 0)
            {
                ActiveWarnings.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    T("WarningGifFramesTooManyTemplate"),
                    gifPdfFrameCount));
            }

            if (AiEnhancementEnabled && !string.IsNullOrWhiteSpace(AiEnhancementModelFallbackWarningText))
            {
                ActiveWarnings.Add(AiEnhancementModelFallbackWarningText);
            }

            if (HasAiEnhancementPerformanceHint)
            {
                ActiveWarnings.Add(AiEnhancementPerformanceHintText);
            }

        }

        private int GetLargeGifPdfFrameCount(IReadOnlyList<ImageItemViewModel> images)
        {
            if (SelectedOutputFormat != OutputImageFormat.Pdf ||
                SelectedGifHandlingMode != GifHandlingMode.AllFrames)
            {
                return 0;
            }

            return images
                .Where(image => image.IsAnimatedGif && image.GifFrameCount > GifPdfLargeFrameWarningThreshold)
                .Select(image => image.GifFrameCount)
                .DefaultIfEmpty(0)
                .Max();
        }

        private bool ShouldShowGifPdfFrameProgress(ConversionProgress progress, ConversionOptions options)
        {
            return options.OutputFormat == OutputImageFormat.Pdf &&
                   progress.CurrentFileTotalCount > 1 &&
                   Path.GetExtension(progress.FileName).Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildProgressFileText(ConversionProgress progress, ConversionOptions options)
        {
            if (!ShouldShowGifPdfFrameProgress(progress, options))
            {
                return progress.FileName;
            }

            var frameNumber = progress.CurrentFileProcessedCount <= 0
                ? 1
                : Math.Min(progress.CurrentFileProcessedCount, progress.CurrentFileTotalCount);

            return string.Format(
                CultureInfo.CurrentCulture,
                T("GifPdfProgressTemplate"),
                progress.FileName,
                frameNumber,
                progress.CurrentFileTotalCount);
        }

        private bool IsHighCompressionSelection()
        {
            return SelectedOutputFormat is OutputImageFormat.Jpeg or OutputImageFormat.Webp &&
                   (SelectedCompressionMode == CompressionMode.HighCompression ||
                    (SelectedCompressionMode == CompressionMode.Custom && Quality <= 45));
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
            var timestampText = entry.Timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
            var sourceText = entry.Source == ConversionTriggerSource.Manual ? T("HistorySourceManual") : T("HistorySourceWatch");
            var formatText = FormatOutputFormat(entry.OutputFormat);
            var duration = TimeSpan.FromMilliseconds(Math.Max(0, entry.DurationMilliseconds));
            var summaryText = entry.WasCanceled
                ? string.Format(CultureInfo.CurrentCulture, T("HistorySummaryCanceledTemplate"), sourceText, formatText, entry.ProcessedCount, entry.TotalCount)
                : string.Format(CultureInfo.CurrentCulture, T("HistorySummaryTemplate"), sourceText, formatText, entry.TotalCount, entry.SuccessCount, entry.FailureCount);
            var detailText = string.Format(CultureInfo.CurrentCulture, T("HistoryDetailTemplate"), FormatBytes(entry.OriginalTotalBytes), FormatBytesRange(entry.EstimatedMinBytes, entry.EstimatedMaxBytes), FormatDuration(duration));

            return new RecentConversionItem
            {
                TimestampText = timestampText,
                SummaryText = summaryText,
                DetailText = detailText,
                FailureLogPath = entry.FailureLogPath
            };
        }

        private void AppendHistory(
            ConversionSummary summary,
            ConversionOptions options,
            SizeEstimateResult estimate,
            string? failureLogPath,
            ConversionTriggerSource source)
        {
            var updated = _conversionHistoryService.Append(new ConversionHistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
                Source = source,
                OutputFormat = options.OutputFormat,
                TotalCount = summary.TotalCount,
                ProcessedCount = summary.ProcessedCount,
                SuccessCount = summary.SuccessCount,
                FailureCount = summary.FailureCount,
                OriginalTotalBytes = estimate.OriginalTotalBytes,
                EstimatedMinBytes = estimate.EstimatedMinBytes,
                EstimatedMaxBytes = estimate.EstimatedMaxBytes,
                DurationMilliseconds = summary.Duration.TotalMilliseconds,
                WasCanceled = summary.WasCanceled,
                FailureLogPath = failureLogPath ?? string.Empty
            });

            ReplaceHistory(updated);
        }

        private void RefreshLocalizedPropertiesV3()
        {
            OnPropertyChanged(nameof(FormatRecommendationTitleText));
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
            OnPropertyChanged(nameof(WatchModeEnabledText));
            OnPropertyChanged(nameof(WatchInputFolderText));
            OnPropertyChanged(nameof(WatchOutputFolderText));
            OnPropertyChanged(nameof(WatchStatusLabelText));
            OnPropertyChanged(nameof(WatchIncludeSubfoldersText));
            OnPropertyChanged(nameof(WatchHintText));
            OnPropertyChanged(nameof(WatchProfileSummaryText));
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
            SetStatus("StatusPaused");
        }

        [RelayCommand(CanExecute = nameof(CanResumeConversion))]
        private void ResumeConversion()
        {
            _manualPauseController.Resume();
            IsConversionPaused = false;
            SetStatus("StatusConverting");
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

        private void OnActiveWarningsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasActiveWarnings));
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


