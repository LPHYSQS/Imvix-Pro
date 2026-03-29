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

        public string WatchMetricsText => string.Format(CultureInfo.CurrentCulture, T("WatchMetricsTemplate"), WatchProcessedCount, WatchFailureCount);

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
            ActiveWarnings.CollectionChanged += OnActiveWarningsCollectionChanged;
            ConversionPlanHighlights.CollectionChanged += OnConversionPlanHighlightsCollectionChanged;
            RecentConversions.CollectionChanged += OnRecentConversionsCollectionChanged;
            _folderWatchService.FileReady += OnWatchedFileReady;

            _maxParallelism = Math.Clamp(Math.Max(1, preferences.MaxParallelism), 1, 4);
            _watchJobDefinitionSnapshot = watchProfile.JobDefinition?.Clone();
            WatchModeEnabled = watchProfile.IsEnabled;
            WatchInputDirectory = watchProfile.InputDirectory;
            WatchOutputDirectory = watchProfile.OutputDirectory;
            WatchIncludeSubfolders = watchProfile.IncludeSubfolders;
            KeepRunningInTray = preferences.KeepRunningInTray;
            RunOnStartup = preferences.RunOnStartup;
            WatchStatusText = T("WatchStatusStopped");

            LoadRecentConversionHistory();
            RefreshConversionInsights();
            RefreshWatchProfileSummary();
        }

        private async Task CompleteVersion3InitializationAsync()
        {
            RefreshWatchStatus();
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

        private void RefreshWatchStatus()
        {
            if (!WatchModeEnabled)
            {
                WatchStatusText = T("WatchStatusStopped");
                OnPropertyChanged(nameof(IsWatchModeRunning));
                return;
            }

            if (IsWatchProcessing && !string.IsNullOrWhiteSpace(WatchCurrentFile))
            {
                WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusProcessing"), WatchCurrentFile);
                OnPropertyChanged(nameof(IsWatchModeRunning));
                return;
            }

            if (_folderWatchService.IsRunning)
            {
                WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusRunning"), WatchInputDirectory);
                OnPropertyChanged(nameof(IsWatchModeRunning));
                return;
            }

            if (!TryValidateWatchConfiguration(out var validationMessage))
            {
                WatchStatusText = validationMessage;
            }
            else
            {
                WatchStatusText = T("WatchStatusWaiting");
            }

            OnPropertyChanged(nameof(IsWatchModeRunning));
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
                    RefreshWatchStatus();
                    return;
                }

                if (!TryValidateWatchConfiguration(out var validationMessage))
                {
                    WatchStatusText = validationMessage;
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
                RefreshWatchStatus();
            }
            catch (Exception ex)
            {
                WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusErrorTemplate"), ex.Message);
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
                    WatchFailureCount++;
                    WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusSingleFailureTemplate"), Path.GetFileName(path), TranslateInputError(error));
                    return;
                }

                IsWatchProcessing = true;
                WatchCurrentFile = item.FileName;
                RefreshWatchStatus();

                var snapshot = new System.Collections.Generic.List<ImageItemViewModel> { item };
                var watchProfile = BuildConfiguredWatchProfile();
                var options = _watchProfilePlanningService.BuildExecutionOptions(watchProfile, item);
                var estimate = _imageAnalysisService.Estimate(snapshot, options);
                var progress = new Progress<ConversionProgress>(p =>
                {
                    WatchCurrentFile = p.FileName;
                    RefreshWatchStatus();
                });

                var summary = await _conversionPipelineService.ConvertAsync(snapshot, options, progress, pauseController: null, cancellationToken: cancellation.Token);

                var logPath = _conversionLogService.WriteFailureLog(summary, options, snapshot, ConversionTriggerSource.Watch);
                var completionSummary = _conversionStatusSummaryService.CreateCompletionSummary(
                    summary,
                    ConversionTriggerSource.Watch,
                    options.OutputFormat,
                    estimate,
                    logPath);
                AppendHistory(completionSummary);

                if (summary.FailureCount > 0)
                {
                    WatchFailureCount += summary.FailureCount;
                    WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusSingleFailureTemplate"), item.FileName, summary.Failures[0].Reason);
                }
                else
                {
                    WatchProcessedCount += summary.SuccessCount;
                    WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusProcessedTemplate"), item.FileName);
                }
            }
            catch (OperationCanceledException)
            {
                RefreshWatchStatus();
            }
            catch (Exception ex)
            {
                WatchFailureCount++;
                WatchStatusText = string.Format(CultureInfo.CurrentCulture, T("WatchStatusErrorTemplate"), ex.Message);
            }
            finally
            {
                item?.Dispose();
                IsWatchProcessing = false;
                WatchCurrentFile = string.Empty;
                _watchProcessingGate.Release();
                OnPropertyChanged(nameof(WatchMetricsText));

                if (!WatchModeEnabled || cancellation.IsCancellationRequested)
                {
                    RefreshWatchStatus();
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

            RefreshWatchStatus();
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
