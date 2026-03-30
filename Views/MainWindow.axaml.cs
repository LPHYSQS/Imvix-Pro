using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
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
        private readonly SettingsService _settingsService;
        private readonly AppLogger _logger;
        private readonly MainWindowShellCoordinator _shellCoordinator;
        private MainWindowViewModel? _subscribedViewModel;
        private NotificationState? _subscribedNotificationState;
        private bool _windowPlacementRestored;
        private bool _startupPathsHandled;
        private IReadOnlyList<string> _pendingStartupPaths = [];
        private TrayIcon? _trayIcon;
        private NativeMenuItem? _restoreTrayMenuItem;
        private NativeMenuItem? _exitTrayMenuItem;

        public MainWindow()
            : this(AppServices.CreateMainWindowServices())
        {
        }

        internal MainWindow(MainWindowServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            _settingsService = services.SettingsService ?? throw new ArgumentNullException(nameof(services.SettingsService));
            _logger = services.Logger ?? throw new ArgumentNullException(nameof(services.Logger));
            _shellCoordinator = services.ShellCoordinator ?? throw new ArgumentNullException(nameof(services.ShellCoordinator));
            InitializeComponent();
            InitializeTrayIcon();
            Opened += OnWindowOpened;
            Closing += OnWindowClosing;
            DataContextChanged += OnDataContextChanged;
            OnDataContextChanged(this, EventArgs.Empty);
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private static IReadOnlyList<FilePickerFileType> BuildImageFileTypes(MainWindowViewModel vm)
        {
            return
            [
                new FilePickerFileType(vm.ImportSupportedFilesText)
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.ico", "*.svg", "*.pdf", "*.psd", "*.exe", "*.lnk"]
                },
                new FilePickerFileType(vm.ImportPsdText)
                {
                    Patterns = ["*.psd"]
                }
            ];
        }

        public void SetStartupPaths(IEnumerable<string>? paths)
        {
            _pendingStartupPaths = paths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToArray()
                ?? [];
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

            UpdateTrayIconState();

            if (_windowPlacementRestored)
            {
                ViewModel?.LoadRecentConversionHistory();
                ApplyStartupPaths();
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
                UpdateTrayIconState();
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

            await dialog.ShowDialog(this);
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            if (!_windowPlacementRestored)
            {
                _windowPlacementRestored = true;
                RestoreWindowPlacement();
            }

            ViewModel?.LoadRecentConversionHistory();
            ApplyStartupPaths();
            UpdateTrayIconState();
            _ = _shellCoordinator.ShowPendingCompletionDialogAsync(this, _subscribedNotificationState);
        }

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            SaveWindowPlacement();

            if (_shellCoordinator.ShouldHideToTray(ViewModel?.KeepRunningInTray == true, e.CloseReason))
            {
                e.Cancel = true;
                _shellCoordinator.HideToTray(this, UpdateTrayIconState);
                return;
            }

            CleanupTrayIcon();

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

        private void InitializeTrayIcon()
        {
            if (Application.Current is null)
            {
                return;
            }

            try
            {
                using var stream = AssetLoader.Open(AppIdentity.GetAssetUri("Assets/logo.ico"));

                _restoreTrayMenuItem = new NativeMenuItem();
                _restoreTrayMenuItem.Click += OnTrayRestoreClick;

                _exitTrayMenuItem = new NativeMenuItem();
                _exitTrayMenuItem.Click += OnTrayExitClick;

                var menu = new NativeMenu();
                menu.Add(_restoreTrayMenuItem);
                menu.Add(new NativeMenuItemSeparator());
                menu.Add(_exitTrayMenuItem);

                _trayIcon = new TrayIcon
                {
                    Icon = new WindowIcon(stream),
                    Menu = menu,
                    IsVisible = false
                };

                _trayIcon.Clicked += OnTrayIconClicked;

                var icons = new TrayIcons();
                icons.Add(_trayIcon);
                TrayIcon.SetIcons(Application.Current, icons);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(MainWindow), "Failed to initialize tray icon. Continuing without tray integration.", ex);
                _trayIcon = null;
                _restoreTrayMenuItem = null;
                _exitTrayMenuItem = null;
            }
        }

        private void UpdateTrayIconState()
        {
            if (_trayIcon is null)
            {
                return;
            }

            var vm = ViewModel;
            _trayIcon.ToolTipText = vm?.WindowTitle ?? AppIdentity.DisplayName;
            _trayIcon.IsVisible = vm?.KeepRunningInTray == true;

            if (_restoreTrayMenuItem is not null)
            {
                _restoreTrayMenuItem.Header = vm?.TrayRestoreText ?? "Show Main Window";
            }

            if (_exitTrayMenuItem is not null)
            {
                _exitTrayMenuItem.Header = vm?.TrayExitText ?? $"Exit {AppIdentity.DisplayName}";
            }
        }

        private void CleanupTrayIcon()
        {
            if (_restoreTrayMenuItem is not null)
            {
                _restoreTrayMenuItem.Click -= OnTrayRestoreClick;
                _restoreTrayMenuItem = null;
            }

            if (_exitTrayMenuItem is not null)
            {
                _exitTrayMenuItem.Click -= OnTrayExitClick;
                _exitTrayMenuItem = null;
            }

            if (_trayIcon is null)
            {
                return;
            }

            _trayIcon.Clicked -= OnTrayIconClicked;
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;

            if (Application.Current is not null)
            {
                TrayIcon.SetIcons(Application.Current, new TrayIcons());
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

        private void ApplyStartupPaths()
        {
            if (_startupPathsHandled || _pendingStartupPaths.Count == 0)
            {
                return;
            }

            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            _startupPathsHandled = true;
            vm.AddFiles(_pendingStartupPaths);
        }

        private void RestoreWindowPlacement()
        {
            var settings = _settingsService.Load();

            if (settings.HasWindowPlacement)
            {
                if (settings.WindowWidth > 0)
                {
                    Width = Math.Max(MinWidth, settings.WindowWidth);
                }

                if (settings.WindowHeight > 0)
                {
                    Height = Math.Max(MinHeight, settings.WindowHeight);
                }

                var savedPosition = new PixelPoint(settings.WindowPositionX, settings.WindowPositionY);
                if (TryPlaceOnExistingScreen(savedPosition))
                {
                    return;
                }
            }

            CenterOnPrimaryScreen();
        }

        private bool TryPlaceOnExistingScreen(PixelPoint desiredPosition)
        {
            var screens = Screens;
            if (screens is null)
            {
                return false;
            }

            var screen = screens.ScreenFromPoint(desiredPosition);
            if (screen is null)
            {
                var fallbackScaling = screens.Primary?.Scaling ?? 1d;
                var estimatedSize = GetWindowPixelSize(fallbackScaling);
                var rect = new PixelRect(desiredPosition, estimatedSize);
                screen = screens.ScreenFromBounds(rect);
            }

            if (screen is null)
            {
                return false;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = ClampToWorkingArea(desiredPosition, screen);
            return true;
        }

        private void CenterOnPrimaryScreen()
        {
            var screens = Screens;
            var primary = screens?.Primary
                ?? screens?.All.FirstOrDefault(s => s.IsPrimary)
                ?? screens?.All.FirstOrDefault();

            if (primary is null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            var area = primary.WorkingArea;
            var size = GetWindowPixelSize(primary.Scaling);

            var x = area.X + Math.Max(0, (area.Width - size.Width) / 2);
            var y = area.Y + Math.Max(0, (area.Height - size.Height) / 2);

            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(x, y);
        }

        private PixelPoint ClampToWorkingArea(PixelPoint desiredPosition, Screen screen)
        {
            var area = screen.WorkingArea;
            var size = GetWindowPixelSize(screen.Scaling);

            var maxX = area.Right - size.Width;
            var maxY = area.Bottom - size.Height;

            var x = maxX < area.X
                ? area.X
                : Math.Clamp(desiredPosition.X, area.X, maxX);

            var y = maxY < area.Y
                ? area.Y
                : Math.Clamp(desiredPosition.Y, area.Y, maxY);

            return new PixelPoint(x, y);
        }

        private PixelSize GetWindowPixelSize(double scaling)
        {
            var safeScaling = scaling > 0 ? scaling : 1d;

            var widthDip = Bounds.Width > 0
                ? Bounds.Width
                : (double.IsNaN(Width) || Width <= 0 ? MinWidth : Width);

            var heightDip = Bounds.Height > 0
                ? Bounds.Height
                : (double.IsNaN(Height) || Height <= 0 ? MinHeight : Height);

            var widthPx = Math.Max(1, (int)Math.Round(widthDip * safeScaling));
            var heightPx = Math.Max(1, (int)Math.Round(heightDip * safeScaling));

            return new PixelSize(widthPx, heightPx);
        }

        private void SaveWindowPlacement()
        {
            if (WindowState == WindowState.Minimized)
            {
                return;
            }

            var settings = _settingsService.Load();
            settings.HasWindowPlacement = true;
            settings.WindowPositionX = Position.X;
            settings.WindowPositionY = Position.Y;

            if (WindowState == WindowState.Normal)
            {
                if (Bounds.Width > 0)
                {
                    settings.WindowWidth = Bounds.Width;
                }

                if (Bounds.Height > 0)
                {
                    settings.WindowHeight = Bounds.Height;
                }
            }

            _settingsService.Save(settings);
        }

        internal async void OnImportClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = vm.ImportDialogTitle,
                FileTypeFilter = BuildImageFileTypes(vm)
            });

            var paths = files
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>();

            vm.AddFiles(paths);
        }

        internal async void OnImportFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = true,
                Title = vm.ImportFolderDialogTitle
            });

            var paths = folders
                .Select(folder => folder.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>();

            vm.AddFiles(paths);
        }

        internal async void OnSelectOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = vm.SelectFolderDialogTitle
            });

            var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            vm.SetOutputDirectory(folderPath);
        }

        internal async void OnSelectWatchInputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = vm.SelectWatchInputFolderDialogTitle
            });

            var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            vm.SetWatchInputDirectory(folderPath);
        }

        internal async void OnSelectWatchOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = vm.SelectWatchOutputFolderDialogTitle
            });

            var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            vm.SetWatchOutputDirectory(folderPath);
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
