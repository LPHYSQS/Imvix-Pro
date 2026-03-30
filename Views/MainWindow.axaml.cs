using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.ViewModels;
using ImvixPro.Views.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowShellCoordinator _shellCoordinator;
        private readonly MainWindowHostController _hostController;
        private MainWindowViewModel? _subscribedViewModel;
        private NotificationState? _subscribedNotificationState;

        public MainWindow()
            : this(AppServices.CreateMainWindowServices())
        {
        }

        internal MainWindow(MainWindowServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            _shellCoordinator = services.ShellCoordinator ?? throw new ArgumentNullException(nameof(services.ShellCoordinator));
            _hostController = services.HostController ?? throw new ArgumentNullException(nameof(services.HostController));
            InitializeComponent();
            _hostController.InitializeTrayIcon(OnTrayIconClicked, OnTrayRestoreClick, OnTrayExitClick);
            Opened += OnWindowOpened;
            Closing += OnWindowClosing;
            DataContextChanged += OnDataContextChanged;
            OnDataContextChanged(this, EventArgs.Empty);
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        public void SetStartupPaths(IEnumerable<string>? paths)
        {
            _hostController.SetStartupPaths(paths);
        }

        public async Task HandleSecondInstanceActivationAsync()
        {
            await _shellCoordinator.BringToFrontAsync(this);
            await _shellCoordinator.ShowRunningInstanceWarningAsync(this, ViewModel);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel.ConfirmDialogAsync = null;
            }

            if (_subscribedNotificationState is not null)
            {
                _subscribedNotificationState.PropertyChanged -= OnNotificationStatePropertyChanged;
            }

            _subscribedViewModel = ViewModel;
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
                _subscribedViewModel.ConfirmDialogAsync = ShowConfirmationDialogAsync;
            }

            _subscribedNotificationState = _subscribedViewModel?.NotificationState;
            if (_subscribedNotificationState is not null)
            {
                _subscribedNotificationState.PropertyChanged += OnNotificationStatePropertyChanged;
            }

            _hostController.UpdateTrayIconState(ViewModel);

            if (_hostController.IsWindowPlacementRestored)
            {
                ViewModel?.LoadRecentConversionHistory();
                _hostController.ApplyStartupPaths(ViewModel);
                _ = _shellCoordinator.ShowPendingCompletionDialogAsync(this, _subscribedNotificationState);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName) ||
                e.PropertyName is nameof(MainWindowViewModel.KeepRunningInTray) or
                    nameof(MainWindowViewModel.TrayRestoreText) or
                    nameof(MainWindowViewModel.TrayExitText) or
                    nameof(MainWindowViewModel.WindowTitle))
            {
                _hostController.UpdateTrayIconState(ViewModel);
            }
        }

        private void OnNotificationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName) ||
                e.PropertyName is nameof(NotificationState.PendingDialogRequest) or nameof(NotificationState.HasPendingDialogRequest))
            {
                _ = _shellCoordinator.ShowPendingCompletionDialogAsync(this, _subscribedNotificationState);
            }
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string message, string confirmText, string cancelText)
        {
            if (!IsVisible)
            {
                return false;
            }

            var dialog = new ConfirmationDialogWindow(title, message, confirmText, cancelText)
            {
                FlowDirection = this.FlowDirection
            };

            WindowScreenBoundsHelper.PrepareCenteredWindow(dialog, this);
            return await dialog.ShowDialog<bool>(this);
        }

        private async void OnShowAboutClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null || !IsVisible)
            {
                return;
            }

            var dialog = new AboutWindow(vm)
            {
                FlowDirection = this.FlowDirection
            };

            WindowScreenBoundsHelper.PrepareCenteredWindow(dialog, this);
            await dialog.ShowDialog(this);
        }

        private async void OnShowContactAuthorClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null || !IsVisible)
            {
                return;
            }

            var dialog = new ContactAuthorWindow(vm)
            {
                FlowDirection = this.FlowDirection
            };

            WindowScreenBoundsHelper.PrepareCenteredWindow(dialog, this);
            await dialog.ShowDialog(this);
        }

        private async void OnShowVersionNotesClick(object? sender, RoutedEventArgs e)
        {
            await ShowVersionNotesDialogAsync();
        }

        private async void OnVersionBadgePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            await ShowVersionNotesDialogAsync();
        }

        private void OnVersionBadgePointerEntered(object? sender, PointerEventArgs e)
        {
            ViewModel?.SetVersionBadgeHover(true);
        }

        private void OnVersionBadgePointerExited(object? sender, PointerEventArgs e)
        {
            ViewModel?.SetVersionBadgeHover(false);
        }

        private async Task ShowVersionNotesDialogAsync()
        {
            var vm = ViewModel;
            if (vm is null || !IsVisible)
            {
                return;
            }

            vm.SetVersionBadgeHover(false);

            var dialog = new UpdateNotesWindow(
                vm.VersionNotesWindowTitleText,
                vm.VersionNotesHeaderText,
                vm.VersionNotesSummaryText,
                vm.VersionNotesFixesTitleText,
                vm.VersionNotesFixesBodyText,
                vm.VersionNotesFeaturesTitleText,
                vm.VersionNotesFeaturesBodyText,
                vm.CloseText)
            {
                FlowDirection = this.FlowDirection
            };

            WindowScreenBoundsHelper.PrepareCenteredWindow(dialog, this);
            await dialog.ShowDialog(this);
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            _hostController.RestoreWindowPlacementIfNeeded(this);
            ViewModel?.LoadRecentConversionHistory();
            _hostController.ApplyStartupPaths(ViewModel);
            _hostController.UpdateTrayIconState(ViewModel);
            _ = _shellCoordinator.ShowPendingCompletionDialogAsync(this, _subscribedNotificationState);
        }

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            _hostController.SaveWindowPlacement(this);

            if (_shellCoordinator.ShouldHideToTray(ViewModel?.KeepRunningInTray == true, e.CloseReason))
            {
                e.Cancel = true;
                _shellCoordinator.HideToTray(this, () => _hostController.UpdateTrayIconState(ViewModel));
                return;
            }

            _hostController.CleanupTrayIcon();

            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel.ConfirmDialogAsync = null;
                _subscribedViewModel = null;
            }

            if (_subscribedNotificationState is not null)
            {
                _subscribedNotificationState.PropertyChanged -= OnNotificationStatePropertyChanged;
                _subscribedNotificationState = null;
            }
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            _ = _shellCoordinator.RestoreFromTrayAsync(this, _subscribedNotificationState);
        }

        private void OnTrayRestoreClick(object? sender, EventArgs e)
        {
            _ = _shellCoordinator.RestoreFromTrayAsync(this, _subscribedNotificationState);
        }

        private void OnTrayExitClick(object? sender, EventArgs e)
        {
            _shellCoordinator.ExitApplication(this);
        }

        internal async void OnImportClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            await _hostController.ImportFilesAsync(this, vm);
        }

        internal async void OnImportFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            await _hostController.ImportFoldersAsync(this, vm);
        }

        internal async void OnSelectOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            await _hostController.SelectOutputFolderAsync(this, vm);
        }

        internal async void OnSelectWatchInputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            await _hostController.SelectWatchInputFolderAsync(this, vm);
        }

        internal async void OnSelectWatchOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            await _hostController.SelectWatchOutputFolderAsync(this, vm);
        }

        internal void OnDropZoneDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;

            e.Handled = true;
        }

        internal void OnDropZoneDrop(object? sender, DragEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null || !e.DataTransfer.Contains(DataFormat.File))
            {
                return;
            }

            var files = e.DataTransfer.TryGetFiles();
            if (files is null)
            {
                return;
            }

            var paths = files
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToList();

            if (paths.Count > 0)
            {
                vm.AddFiles(paths);
            }
        }

        internal async void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.Source is Visual source &&
                source.GetSelfAndVisualAncestors().OfType<Button>().Any())
            {
                return;
            }

            await _shellCoordinator.OpenPreviewFromSourceAsync(
                this,
                ViewModel,
                sender,
                includeGifTrimRange: true,
                allowSelectedImageFallback: true,
                skipAnimatedGif: false);
            e.Handled = true;
        }

        internal void OnGifSpecificFrameSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            ViewModel?.HandleGifSpecificFrameSliderChanged(e.NewValue);
        }

        internal void OnGifTrimRangeChanged(object? sender, GifFrameRangeChangedEventArgs e)
        {
            ViewModel?.HandleGifTrimRangeChanged(e.StartValue, e.EndValue);
        }

        internal void OnGifPdfAllFramesChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetGifPdfExportMode(GifHandlingMode.AllFrames);
        }

        internal void OnGifPdfCurrentFrameChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetGifPdfExportMode(GifHandlingMode.SpecificFrame);
        }

        internal void OnPdfPageSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            ViewModel?.HandlePdfPageSliderChanged(e.NewValue);
        }

        internal void OnPdfRangeChanged(object? sender, PageRangeChangedEventArgs e)
        {
            ViewModel?.HandlePdfRangeChanged(e.StartValue, e.EndValue);
        }

        internal void OnPdfImageAllPagesChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfImageExportMode(PdfImageExportMode.AllPages);
        }

        internal void OnPdfImageCurrentPageChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfImageExportMode(PdfImageExportMode.CurrentPage);
        }

        internal void OnPdfDocumentAllPagesChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfDocumentExportMode(PdfDocumentExportMode.AllPages);
        }

        internal void OnPdfDocumentCurrentPageChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfDocumentExportMode(PdfDocumentExportMode.CurrentPage);
        }

        internal void OnPdfDocumentRangeChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfDocumentExportMode(PdfDocumentExportMode.PageRange);
        }

        internal void OnPdfDocumentSplitSinglePagesChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel?.SetPdfDocumentExportMode(PdfDocumentExportMode.SplitSinglePages);
        }

        internal async void OnImageItemDoubleTapped(object? sender, TappedEventArgs e)
        {
            await _shellCoordinator.OpenPreviewFromSourceAsync(
                this,
                ViewModel,
                sender,
                includeGifTrimRange: false,
                allowSelectedImageFallback: false,
                skipAnimatedGif: true);
            e.Handled = true;
        }

        internal void OnShowFileDetailClick(object? sender, RoutedEventArgs e)
        {
            _shellCoordinator.OpenFileDetailWindowFromSource(this, ViewModel, sender);
            e.Handled = true;
        }

        internal async void OnPdfUnlockClick(object? sender, RoutedEventArgs e)
        {
            await _shellCoordinator.ShowPdfUnlockDialogFromSourceAsync(this, ViewModel, sender);
            e.Handled = true;
        }
    }
}
