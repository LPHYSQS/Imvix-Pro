using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImvixPro.Views;

namespace ImvixPro.Views.Controls
{
    public partial class FileListPanel : UserControl
    {
        public FileListPanel()
        {
            InitializeComponent();
        }

        private MainWindow? HostWindow => TopLevel.GetTopLevel(this) as MainWindow;

        private void OnDropZoneDragOver(object? sender, DragEventArgs e)
        {
            HostWindow?.OnDropZoneDragOver(sender, e);
        }

        private void OnDropZoneDrop(object? sender, DragEventArgs e)
        {
            HostWindow?.OnDropZoneDrop(sender, e);
        }

        private void OnImageItemDoubleTapped(object? sender, TappedEventArgs e)
        {
            HostWindow?.OnImageItemDoubleTapped(sender, e);
        }

        private void OnShowFileDetailClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnShowFileDetailClick(sender, e);
        }

        private void OnPdfUnlockClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnPdfUnlockClick(sender, e);
        }
    }
}
