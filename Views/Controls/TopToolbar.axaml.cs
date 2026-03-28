using Avalonia.Controls;
using Avalonia.Interactivity;
using ImvixPro.Views;

namespace ImvixPro.Views.Controls
{
    public partial class TopToolbar : UserControl
    {
        public TopToolbar()
        {
            InitializeComponent();
        }

        private MainWindow? HostWindow => TopLevel.GetTopLevel(this) as MainWindow;

        private void OnImportClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnImportClick(sender, e);
        }

        private void OnImportFolderClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnImportFolderClick(sender, e);
        }

        private void OnSelectOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnSelectOutputFolderClick(sender, e);
        }
    }
}
