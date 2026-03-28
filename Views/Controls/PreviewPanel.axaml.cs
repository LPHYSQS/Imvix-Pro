using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImvixPro.Views;

namespace ImvixPro.Views.Controls
{
    public partial class PreviewPanel : UserControl
    {
        public PreviewPanel()
        {
            InitializeComponent();
        }

        private MainWindow? HostWindow => TopLevel.GetTopLevel(this) as MainWindow;

        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e) => HostWindow?.OnPreviewPointerPressed(sender, e);
        private void OnPdfImageAllPagesChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfImageAllPagesChecked(sender, e);
        private void OnPdfImageCurrentPageChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfImageCurrentPageChecked(sender, e);
        private void OnPdfPageSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => HostWindow?.OnPdfPageSliderValueChanged(sender, e);
        private void OnPdfRangeChanged(object? sender, PageRangeChangedEventArgs e) => HostWindow?.OnPdfRangeChanged(sender, e);
        private void OnPdfDocumentAllPagesChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfDocumentAllPagesChecked(sender, e);
        private void OnPdfDocumentCurrentPageChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfDocumentCurrentPageChecked(sender, e);
        private void OnPdfDocumentRangeChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfDocumentRangeChecked(sender, e);
        private void OnPdfDocumentSplitSinglePagesChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnPdfDocumentSplitSinglePagesChecked(sender, e);
        private void OnGifPdfAllFramesChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnGifPdfAllFramesChecked(sender, e);
        private void OnGifPdfCurrentFrameChecked(object? sender, RoutedEventArgs e) => HostWindow?.OnGifPdfCurrentFrameChecked(sender, e);
        private void OnGifSpecificFrameSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => HostWindow?.OnGifSpecificFrameSliderValueChanged(sender, e);
        private void OnGifTrimRangeChanged(object? sender, GifFrameRangeChangedEventArgs e) => HostWindow?.OnGifTrimRangeChanged(sender, e);
    }
}
