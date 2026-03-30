using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ImvixPro.Services;
using ImvixPro.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    internal sealed class MainWindowHostController
    {
        private readonly SettingsService _settingsService;
        private readonly AppLogger _logger;
        private bool _windowPlacementRestored;
        private bool _startupPathsHandled;
        private IReadOnlyList<string> _pendingStartupPaths = [];
        private TrayIcon? _trayIcon;
        private NativeMenuItem? _restoreTrayMenuItem;
        private NativeMenuItem? _exitTrayMenuItem;
        private EventHandler? _trayIconClickedHandler;
        private EventHandler? _trayRestoreClickedHandler;
        private EventHandler? _trayExitClickedHandler;

        public MainWindowHostController(SettingsService settingsService, AppLogger logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsWindowPlacementRestored => _windowPlacementRestored;

        public void InitializeTrayIcon(EventHandler trayIconClicked, EventHandler trayRestoreClicked, EventHandler trayExitClicked)
        {
            ArgumentNullException.ThrowIfNull(trayIconClicked);
            ArgumentNullException.ThrowIfNull(trayRestoreClicked);
            ArgumentNullException.ThrowIfNull(trayExitClicked);

            if (Application.Current is null)
            {
                return;
            }

            try
            {
                using var stream = AssetLoader.Open(AppIdentity.GetAssetUri("Assets/logo.ico"));

                _trayIconClickedHandler = trayIconClicked;
                _trayRestoreClickedHandler = trayRestoreClicked;
                _trayExitClickedHandler = trayExitClicked;

                _restoreTrayMenuItem = new NativeMenuItem();
                _restoreTrayMenuItem.Click += trayRestoreClicked;

                _exitTrayMenuItem = new NativeMenuItem();
                _exitTrayMenuItem.Click += trayExitClicked;

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

                _trayIcon.Clicked += trayIconClicked;

                var icons = new TrayIcons();
                icons.Add(_trayIcon);
                TrayIcon.SetIcons(Application.Current, icons);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(MainWindowHostController), "Failed to initialize tray icon. Continuing without tray integration.", ex);
                _trayIcon = null;
                _restoreTrayMenuItem = null;
                _exitTrayMenuItem = null;
                _trayIconClickedHandler = null;
                _trayRestoreClickedHandler = null;
                _trayExitClickedHandler = null;
            }
        }

        public void UpdateTrayIconState(MainWindowViewModel? viewModel)
        {
            if (_trayIcon is null)
            {
                return;
            }

            _trayIcon.ToolTipText = viewModel?.WindowTitle ?? AppIdentity.DisplayName;
            _trayIcon.IsVisible = viewModel?.KeepRunningInTray == true;

            if (_restoreTrayMenuItem is not null)
            {
                _restoreTrayMenuItem.Header = viewModel?.TrayRestoreText ?? "Show Main Window";
            }

            if (_exitTrayMenuItem is not null)
            {
                _exitTrayMenuItem.Header = viewModel?.TrayExitText ?? $"Exit {AppIdentity.DisplayName}";
            }
        }

        public void CleanupTrayIcon()
        {
            if (_restoreTrayMenuItem is not null && _trayRestoreClickedHandler is not null)
            {
                _restoreTrayMenuItem.Click -= _trayRestoreClickedHandler;
            }

            if (_exitTrayMenuItem is not null && _trayExitClickedHandler is not null)
            {
                _exitTrayMenuItem.Click -= _trayExitClickedHandler;
            }

            _restoreTrayMenuItem = null;
            _exitTrayMenuItem = null;

            if (_trayIcon is not null)
            {
                if (_trayIconClickedHandler is not null)
                {
                    _trayIcon.Clicked -= _trayIconClickedHandler;
                }

                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayIconClickedHandler = null;
            _trayRestoreClickedHandler = null;
            _trayExitClickedHandler = null;

            if (Application.Current is not null)
            {
                TrayIcon.SetIcons(Application.Current, new TrayIcons());
            }
        }

        public void SetStartupPaths(IEnumerable<string>? paths)
        {
            _pendingStartupPaths = paths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToArray()
                ?? [];
            _startupPathsHandled = false;
        }

        public void ApplyStartupPaths(MainWindowViewModel? viewModel)
        {
            if (_startupPathsHandled || _pendingStartupPaths.Count == 0 || viewModel is null)
            {
                return;
            }

            _startupPathsHandled = true;
            viewModel.AddFiles(_pendingStartupPaths);
        }

        public void RestoreWindowPlacementIfNeeded(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (_windowPlacementRestored)
            {
                return;
            }

            _windowPlacementRestored = true;
            RestoreWindowPlacement(owner);
        }

        public void SaveWindowPlacement(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (owner.WindowState == WindowState.Minimized)
            {
                return;
            }

            var settings = _settingsService.Load();
            settings.HasWindowPlacement = true;
            settings.WindowPositionX = owner.Position.X;
            settings.WindowPositionY = owner.Position.Y;

            if (owner.WindowState == WindowState.Normal)
            {
                if (owner.Bounds.Width > 0)
                {
                    settings.WindowWidth = owner.Bounds.Width;
                }

                if (owner.Bounds.Height > 0)
                {
                    settings.WindowHeight = owner.Bounds.Height;
                }
            }

            _settingsService.Save(settings);
        }

        public async Task ImportFilesAsync(Window owner, MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(viewModel);

            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = viewModel.ImportDialogTitle,
                FileTypeFilter = BuildImageFileTypes(viewModel)
            });

            var paths = files
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>();

            viewModel.AddFiles(paths);
        }

        public async Task ImportFoldersAsync(Window owner, MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(viewModel);

            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = true,
                Title = viewModel.ImportFolderDialogTitle
            });

            var paths = folders
                .Select(folder => folder.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>();

            viewModel.AddFiles(paths);
        }

        public Task SelectOutputFolderAsync(Window owner, MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(viewModel);

            return SelectSingleFolderAsync(
                owner,
                viewModel.SelectFolderDialogTitle,
                viewModel.SetOutputDirectory);
        }

        public Task SelectWatchInputFolderAsync(Window owner, MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(viewModel);

            return SelectSingleFolderAsync(
                owner,
                viewModel.SelectWatchInputFolderDialogTitle,
                viewModel.SetWatchInputDirectory);
        }

        public Task SelectWatchOutputFolderAsync(Window owner, MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(viewModel);

            return SelectSingleFolderAsync(
                owner,
                viewModel.SelectWatchOutputFolderDialogTitle,
                viewModel.SetWatchOutputDirectory);
        }

        private static IReadOnlyList<FilePickerFileType> BuildImageFileTypes(MainWindowViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            return
            [
                new FilePickerFileType(viewModel.ImportSupportedFilesText)
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.ico", "*.svg", "*.pdf", "*.psd", "*.exe", "*.lnk"]
                },
                new FilePickerFileType(viewModel.ImportPsdText)
                {
                    Patterns = ["*.psd"]
                }
            ];
        }

        private async Task SelectSingleFolderAsync(Window owner, string title, Action<string> applySelection)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(applySelection);

            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = title
            });

            var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            applySelection(folderPath);
        }

        private void RestoreWindowPlacement(Window owner)
        {
            var settings = _settingsService.Load();

            if (settings.HasWindowPlacement)
            {
                if (settings.WindowWidth > 0)
                {
                    owner.Width = Math.Max(owner.MinWidth, settings.WindowWidth);
                }

                if (settings.WindowHeight > 0)
                {
                    owner.Height = Math.Max(owner.MinHeight, settings.WindowHeight);
                }

                var savedPosition = new PixelPoint(settings.WindowPositionX, settings.WindowPositionY);
                if (WindowScreenBoundsHelper.TryPrepareSavedPlacement(owner, savedPosition))
                {
                    return;
                }
            }

            WindowScreenBoundsHelper.PrepareForPrimaryScreen(owner);
        }
    }
}
