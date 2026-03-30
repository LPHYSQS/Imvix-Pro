using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImvixPro.Models;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public bool IsWatchModeRunning => _folderWatchService.IsRunning;

        public string WatchModeTitleText => T("WatchModeTitle");

        public string WatchModeEnabledText => T("WatchModeEnabled");

        public string WatchInputFolderText => T("WatchInputFolder");

        public string WatchOutputFolderText => T("WatchOutputFolder");

        public string WatchStatusLabelText => T("WatchStatusLabel");

        public string WatchIncludeSubfoldersText => T("WatchIncludeSubfolders");

        public string WatchHintText => BuildWatchHintText();

        public string SelectWatchInputFolderDialogTitle => T("SelectWatchInputFolderDialogTitle");

        public string SelectWatchOutputFolderDialogTitle => T("SelectWatchOutputFolderDialogTitle");

        public string WatchMetricsText => _conversionTextPresenter.BuildWatchMetricsText(_watchRuntimeStatus, T);

        public string TraySettingsTitleText => T("TraySettingsTitle");

        public string KeepRunningInTrayText => T("KeepRunningInTray");

        public string TrayHintText => T("TrayHint");

        public string TrayRestoreText => T("TrayRestore");

        public string TrayExitText => T("TrayExit");

        public string StartupSettingsTitleText => T("StartupSettingsTitle");

        public string RunOnStartupText => T("RunOnStartup");

        public string CreateDesktopShortcutText => T("CreateDesktopShortcut");

        public string SaveCurrentWatchProfileText => T("SaveCurrentWatchProfile");

        public bool HasDesktopShortcutStatus => !string.IsNullOrWhiteSpace(DesktopShortcutStatusText);

        public string WatchProfileSummaryText => BuildWatchProfileSummaryText();

        public bool HasWatchProfileSummary => !string.IsNullOrWhiteSpace(WatchProfileSummaryText);

        [ObservableProperty]
        private bool watchModeEnabled;

        [ObservableProperty]
        private string watchInputDirectory = string.Empty;

        [ObservableProperty]
        private string watchOutputDirectory = string.Empty;

        [ObservableProperty]
        private bool watchIncludeSubfolders = true;

        [ObservableProperty]
        private bool keepRunningInTray;

        [ObservableProperty]
        private bool runOnStartup;

        [ObservableProperty]
        private string desktopShortcutStatusText = string.Empty;

        partial void OnDesktopShortcutStatusTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasDesktopShortcutStatus));
        }

        [ObservableProperty]
        private string watchStatusText = string.Empty;

        [ObservableProperty]
        private int watchProcessedCount;

        [ObservableProperty]
        private int watchFailureCount;

        [ObservableProperty]
        private string watchCurrentFile = string.Empty;

        [ObservableProperty]
        private bool isWatchProcessing;

        private void InitializeVersion3Features(ApplicationPreferences preferences, WatchProfile watchProfile)
        {
            _folderWatchService.FileReady += OnWatchedFileReady;

            _maxParallelism = Math.Clamp(Math.Max(1, preferences.MaxParallelism), 1, 4);
            _watchJobDefinitionSnapshot = watchProfile.JobDefinition?.Clone();
            WatchModeEnabled = watchProfile.IsEnabled;
            WatchInputDirectory = watchProfile.InputDirectory;
            WatchOutputDirectory = watchProfile.OutputDirectory;
            WatchIncludeSubfolders = watchProfile.IncludeSubfolders;
            KeepRunningInTray = preferences.KeepRunningInTray;
            RunOnStartup = preferences.RunOnStartup;
            ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateStoppedWatchRuntimeStatus(0, 0));

            LoadRecentConversionHistory();
            RefreshConversionInsights();
            RefreshWatchProfileSummary();
        }

        private async Task CompleteVersion3InitializationAsync()
        {
            RefreshWatchStatus(resetTerminalState: true);
            RefreshConversionInsights();

            if (WatchModeEnabled)
            {
                await ReconfigureWatchModeAsync();
            }
        }

        private void EnsureStartupState()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _systemIntegrationService.SetRunOnStartup(RunOnStartup);
        }

        private void ApplyWatchRuntimeStatus(WatchRuntimeStatusSummary status)
        {
            ArgumentNullException.ThrowIfNull(status);

            _watchRuntimeStatus = status;
            WatchProcessedCount = status.ProcessedCount;
            WatchFailureCount = status.FailureCount;
            WatchCurrentFile = status.CurrentItemName;
            IsWatchProcessing = status.IsProcessing;
            WatchStatusText = _conversionTextPresenter.BuildWatchStatusText(status, T);
            OnPropertyChanged(nameof(WatchMetricsText));
        }

        private void RefreshWatchStatus(bool resetTerminalState = false)
        {
            if (!resetTerminalState &&
                _watchRuntimeStatus.State is WatchRuntimeState.Processing or WatchRuntimeState.LastCompletion or WatchRuntimeState.Error)
            {
                ApplyWatchRuntimeStatus(_watchRuntimeStatus with { WatchDirectory = WatchInputDirectory });
                OnPropertyChanged(nameof(IsWatchModeRunning));
                return;
            }

            ApplyWatchRuntimeStatus(BuildBaselineWatchRuntimeStatus());
            OnPropertyChanged(nameof(IsWatchModeRunning));
        }

        private WatchRuntimeStatusSummary BuildBaselineWatchRuntimeStatus()
        {
            if (!WatchModeEnabled)
            {
                return _conversionStatusSummaryService.CreateStoppedWatchRuntimeStatus(
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount);
            }

            if (!TryValidateWatchConfiguration(out var validationMessage))
            {
                return _conversionStatusSummaryService.CreateWatchValidationErrorStatus(
                    validationMessage,
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount);
            }

            return _folderWatchService.IsRunning
                ? _conversionStatusSummaryService.CreateRunningWatchRuntimeStatus(
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount)
                : _conversionStatusSummaryService.CreateWaitingWatchRuntimeStatus(
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount);
        }

        private async Task ReconfigureWatchModeAsync()
        {
            await _watchConfigurationGate.WaitAsync();
            try
            {
                _folderWatchService.Stop();
                _watchProcessingCancellationSource?.Cancel();
                _watchProcessingCancellationSource?.Dispose();
                _watchProcessingCancellationSource = null;

                if (!WatchModeEnabled)
                {
                    RefreshWatchStatus(resetTerminalState: true);
                    return;
                }

                if (!TryValidateWatchConfiguration(out var validationMessage))
                {
                    ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchValidationErrorStatus(
                        validationMessage,
                        WatchInputDirectory,
                        _watchRuntimeStatus.ProcessedCount,
                        _watchRuntimeStatus.FailureCount));
                    OnPropertyChanged(nameof(IsWatchModeRunning));
                    return;
                }

                if (_watchJobDefinitionSnapshot is null)
                {
                    _watchJobDefinitionSnapshot = BuildCurrentJobDefinition(forWatch: true);
                }

                Directory.CreateDirectory(WatchOutputDirectory);
                _watchProcessingCancellationSource = new CancellationTokenSource();
                _folderWatchService.Start(WatchInputDirectory, WatchIncludeSubfolders);
                PersistSettings();
                RefreshWatchProfileSummary();
                RefreshWatchStatus(resetTerminalState: true);
            }
            catch (Exception ex)
            {
                ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchErrorStatus(
                    ex.Message,
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount));
                OnPropertyChanged(nameof(IsWatchModeRunning));
            }
            finally
            {
                _watchConfigurationGate.Release();
            }
        }

        private bool TryValidateWatchConfiguration(out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(WatchInputDirectory) || !Directory.Exists(WatchInputDirectory))
            {
                message = T("WatchStatusInvalidInput");
                return false;
            }

            if (string.IsNullOrWhiteSpace(WatchOutputDirectory))
            {
                message = T("WatchStatusInvalidOutput");
                return false;
            }

            if (PathsOverlap(WatchInputDirectory, WatchOutputDirectory))
            {
                message = T("WatchStatusOverlap");
                return false;
            }

            return true;
        }

        private static bool PathsOverlap(string firstPath, string secondPath)
        {
            var first = EnsureTrailingSeparator(Path.GetFullPath(firstPath));
            var second = EnsureTrailingSeparator(Path.GetFullPath(secondPath));
            return first.StartsWith(second, StringComparison.OrdinalIgnoreCase) || second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private void OnWatchedFileReady(object? sender, string path)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _ = ProcessWatchedFileAsync(path);
            });
        }

        private async Task ProcessWatchedFileAsync(string path)
        {
            var cancellation = _watchProcessingCancellationSource;
            if (cancellation is null || !WatchModeEnabled)
            {
                return;
            }

            await _watchProcessingGate.WaitAsync();
            ImageItemViewModel? item = null;

            try
            {
                cancellation.Token.ThrowIfCancellationRequested();

                while (IsConverting)
                {
                    await Task.Delay(350, cancellation.Token);
                }

                if (!TryCreateInputItem(path, out item, out var error, generateThumbnail: false) || item is null)
                {
                    var nextFailureCount = _watchRuntimeStatus.FailureCount + 1;
                    ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchErrorStatus(
                        TranslateInputError(error),
                        WatchInputDirectory,
                        _watchRuntimeStatus.ProcessedCount,
                        nextFailureCount,
                        Path.GetFileName(path)));
                    return;
                }

                ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchProcessingStatus(
                    new RuntimeStatusSummary("StatusConverting", item.FileName, 0, 0),
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount));

                var snapshot = new System.Collections.Generic.List<ImageItemViewModel> { item };
                var watchProfile = BuildConfiguredWatchProfile();
                var options = _watchProfilePlanningService.BuildExecutionOptions(watchProfile, item);
                var estimate = _imageAnalysisService.Estimate(snapshot, options);
                var progress = new Progress<ConversionProgress>(p =>
                {
                    var progressPercent = p.TotalCount == 0
                        ? 0
                        : 100d * p.ProcessedCount / p.TotalCount;
                    var runtimeStatus = _conversionStatusSummaryService.CreateProgressRuntimeStatus(
                        p,
                        options,
                        Math.Max(0, p.TotalFileCount - p.ProcessedFileCount),
                        progressPercent);
                    ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchProcessingStatus(
                        runtimeStatus,
                        WatchInputDirectory,
                        _watchRuntimeStatus.ProcessedCount,
                        _watchRuntimeStatus.FailureCount));
                });

                var summary = await _conversionPipelineService.ConvertAsync(snapshot, options, progress, pauseController: null, cancellationToken: cancellation.Token);

                var logPath = _conversionLogService.WriteFailureLog(summary, options, snapshot, ConversionTriggerSource.Watch);
                var completionFlow = _conversionSummaryCoordinator.BuildCompletionFlow(
                    summary,
                    ConversionTriggerSource.Watch,
                    options.OutputFormat,
                    estimate,
                    logPath,
                    T,
                    includeDialog: false);
                HistoryState.Append(completionFlow.HistoryEntry, T);

                ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchCompletionStatus(
                    completionFlow.Summary,
                    item.FileName,
                    summary.FailureCount > 0 ? summary.Failures[0].Reason : string.Empty,
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount + summary.SuccessCount,
                    _watchRuntimeStatus.FailureCount + summary.FailureCount));
            }
            catch (OperationCanceledException)
            {
                RefreshWatchStatus(resetTerminalState: true);
            }
            catch (Exception ex)
            {
                ApplyWatchRuntimeStatus(_conversionStatusSummaryService.CreateWatchErrorStatus(
                    ex.Message,
                    WatchInputDirectory,
                    _watchRuntimeStatus.ProcessedCount,
                    _watchRuntimeStatus.FailureCount + 1,
                    item?.FileName ?? Path.GetFileName(path)));
            }
            finally
            {
                item?.Dispose();
                _watchProcessingGate.Release();

                if (!WatchModeEnabled || cancellation.IsCancellationRequested)
                {
                    RefreshWatchStatus(resetTerminalState: true);
                }
                else
                {
                    OnPropertyChanged(nameof(IsWatchModeRunning));
                }
            }
        }

        public void SetWatchInputDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                WatchInputDirectory = path;
            }
        }

        public void SetWatchOutputDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                WatchOutputDirectory = path;
            }
        }

        partial void OnWatchModeEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(IsWatchModeRunning));
            if (_isLoadingSettings)
            {
                return;
            }

            if (value && _watchJobDefinitionSnapshot is null)
            {
                _watchJobDefinitionSnapshot = BuildCurrentJobDefinition(forWatch: true);
            }

            _ = ReconfigureWatchModeAsync();
            PersistSettings();
            RefreshWatchProfileSummary();
        }

        partial void OnWatchInputDirectoryChanged(string value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _ = ReconfigureWatchModeAsync();
            PersistSettings();
            RefreshWatchProfileSummary();
        }

        partial void OnWatchOutputDirectoryChanged(string value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _ = ReconfigureWatchModeAsync();
            PersistSettings();
            RefreshWatchProfileSummary();
        }

        partial void OnWatchIncludeSubfoldersChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _ = ReconfigureWatchModeAsync();
            PersistSettings();
            RefreshWatchProfileSummary();
        }

        partial void OnKeepRunningInTrayChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            PersistSettings();
        }

        partial void OnRunOnStartupChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _systemIntegrationService.SetRunOnStartup(value);
            PersistSettings();
        }

        partial void OnWatchProcessedCountChanged(int value)
        {
            OnPropertyChanged(nameof(WatchMetricsText));
        }

        partial void OnWatchFailureCountChanged(int value)
        {
            OnPropertyChanged(nameof(WatchMetricsText));
        }

        [RelayCommand]
        private void CreateDesktopShortcut()
        {
            if (_systemIntegrationService.DesktopShortcutExists())
            {
                DesktopShortcutStatusText = T("DesktopShortcutAlreadyExists");
                return;
            }

            var created = _systemIntegrationService.CreateDesktopShortcut();
            DesktopShortcutStatusText = created
                ? T("DesktopShortcutCreated")
                : T("DesktopShortcutCreateFailed");
        }

        [RelayCommand]
        private async Task SaveCurrentWatchProfileAsync()
        {
            _watchJobDefinitionSnapshot = BuildCurrentJobDefinition(forWatch: true);
            PersistSettings();

            if (WatchModeEnabled)
            {
                await ReconfigureWatchModeAsync();
                return;
            }

            RefreshWatchStatus(resetTerminalState: true);
        }

        private string BuildWatchHintText()
        {
            return T("WatchHintSavedProfile");
        }

        private string BuildWatchProfileSummaryText()
        {
            var summary = _watchProfilePlanningService.BuildSummary(BuildConfiguredWatchProfile());
            if (!summary.HasOutputDirectory)
            {
                return string.Empty;
            }

            var parts = new System.Collections.Generic.List<string>
            {
                $"{OutputFormatText}: {FormatOutputFormat(summary.OutputFormat)}",
                $"{WatchOutputFolderText}: {summary.OutputDirectory}"
            };

            if (summary.AiEnhancementEnabled)
            {
                parts.Add($"{AiEnhancementTabText}: {summary.AiEnhancementScale}x");

                if (BuildAiRuleText(summary.RuleSummary) is { } aiRuleSummary)
                {
                    parts.Add(aiRuleSummary);
                }
            }

            if (summary.UsesResize)
            {
                parts.Add($"{ResizeModeText}: {T($"ResizeMode_{summary.ResizeMode}")}");
            }

            if (BuildGifExpansionSummaryText(summary.RuleSummary) is { } gifExpansionSummary)
            {
                parts.Add(gifExpansionSummary);
            }
            else if (summary.UsesConfiguredGifHandling)
            {
                parts.Add($"{GifHandlingText}: {T($"GifHandlingMode_{summary.GifHandlingMode}")}");
            }

            if (BuildPdfExpansionSummaryText(summary.RuleSummary) is { } pdfExpansionSummary)
            {
                parts.Add(pdfExpansionSummary);
            }

            if (summary.AllowOverwrite)
            {
                parts.Add(AllowOverwriteText);
            }

            return string.Join(" | ", parts);
        }

        private void RefreshWatchProfileSummary()
        {
            OnPropertyChanged(nameof(WatchHintText));
            OnPropertyChanged(nameof(WatchProfileSummaryText));
            OnPropertyChanged(nameof(HasWatchProfileSummary));
        }
    }
}
