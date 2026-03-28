using Avalonia.Controls;
using Avalonia.Interactivity;
using ImvixPro.Views;

namespace ImvixPro.Views.Controls
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
        }

        private MainWindow? HostWindow => TopLevel.GetTopLevel(this) as MainWindow;

        private void OnSelectWatchInputFolderClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnSelectWatchInputFolderClick(sender, e);
        }

        private void OnSelectWatchOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnSelectWatchOutputFolderClick(sender, e);
        }

        private void OnSelectOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            HostWindow?.OnSelectOutputFolderClick(sender, e);
        }
    }
}
