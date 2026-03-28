using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.ViewModels;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class FileDetailWindow : Window
    {
        private readonly FileDetailWindowServices _services;
        private bool _hasLoaded;

        public FileDetailWindow()
            : this(AppServices.CreateFileDetailWindowServices())
        {
        }

        internal FileDetailWindow(FileDetailWindowServices services)
        {
            _services = services ?? throw new System.ArgumentNullException(nameof(services));
            InitializeComponent();
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
        }

        public FileDetailWindow(ImageItemViewModel item, string languageCode)
            : this(item, languageCode, AppServices.CreateFileDetailWindowServices())
        {
        }

        internal FileDetailWindow(ImageItemViewModel item, string languageCode, FileDetailWindowServices services)
            : this(services)
        {
            DataContext = _services.CreateViewModel(item, languageCode);
            Title = item.FileName;
        }

        private async void OnWindowOpened(object? sender, System.EventArgs e)
        {
            if (_hasLoaded || DataContext is not FileDetailViewModel vm)
            {
                return;
            }

            _hasLoaded = true;
            await vm.LoadAsync();
            Title = vm.FileName;
        }

        private void OnWindowClosed(object? sender, System.EventArgs e)
        {
            if (DataContext is FileDetailViewModel vm)
            {
                vm.Dispose();
            }
        }

        private async void OnCopyInfoClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not FileDetailViewModel vm)
            {
                return;
            }

            var text = vm.BuildCopyText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            await clipboard.SetTextAsync(text);
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
