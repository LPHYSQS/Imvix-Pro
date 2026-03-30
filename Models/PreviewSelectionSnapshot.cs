using Avalonia.Media.Imaging;

namespace ImvixPro.Models
{
    public sealed class PreviewSelectionSnapshot
    {
        public Bitmap? SelectedPreview { get; init; }

        public string PreviewSelectionFileText { get; init; } = string.Empty;

        public bool IsPreviewWindowHintVisible { get; init; }

        public bool IsPdfSelected { get; init; }

        public bool IsPdfNavigationVisible { get; init; }

        public bool CanGoToPreviousPdfPage { get; init; }

        public bool CanGoToNextPdfPage { get; init; }

        public bool IsSelectedPdfLocked { get; init; }

        public bool IsSelectedPdfUnlocked { get; init; }

        public string PdfPageIndicatorText { get; init; } = string.Empty;

        public string PdfPageRangeIndicatorText { get; init; } = string.Empty;

        public string PdfLockedPreviewTitleText { get; init; } = string.Empty;

        public string PdfLockedPreviewDescriptionText { get; init; } = string.Empty;

        public string PdfLockedActionHintText { get; init; } = string.Empty;

        public bool IsPdfImageExportVisible { get; init; }

        public bool IsPdfDocumentExportVisible { get; init; }

        public bool IsPdfImageModeSelectorVisible { get; init; }

        public bool IsPdfDocumentModeSelectorVisible { get; init; }

        public bool IsPdfImagePageSliderVisible { get; init; }

        public bool IsPdfDocumentPageSliderVisible { get; init; }

        public bool IsPdfDocumentRangeSliderVisible { get; init; }

        public bool IsSinglePagePdf { get; init; }

        public bool IsPdfImageAllPagesMode { get; init; }

        public bool IsPdfImageCurrentPageMode { get; init; }

        public bool IsPdfDocumentAllPagesMode { get; init; }

        public bool IsPdfDocumentCurrentPageMode { get; init; }

        public bool IsPdfDocumentPageRangeMode { get; init; }

        public bool IsPdfDocumentSplitSinglePagesMode { get; init; }

        public int PdfPageMinimum { get; init; }

        public int PdfPageMaximum { get; init; }

        public int PdfRangeMinimum { get; init; }

        public int PdfRangeMaximum { get; init; }

        public int SelectedPdfPageIndex { get; init; }

        public int SelectedPdfRangeStartIndex { get; init; }

        public int SelectedPdfRangeEndIndex { get; init; }

        public bool IsGifPreviewVisible { get; init; }

        public bool IsGifHandlingSelectorVisible { get; init; }

        public bool IsGifPdfModeSelectorVisible { get; init; }

        public bool IsGifPdfExportAllFramesMode { get; init; }

        public bool IsGifPdfExportCurrentFrameMode { get; init; }

        public bool IsGifSpecificFrameControlsVisible { get; init; }

        public int GifSpecificFrameMaximum { get; init; }

        public double GifSpecificFrameSliderValue { get; init; }

        public bool CanAdjustGifSpecificFrame { get; init; }

        public bool IsGifSpecificFramePlaybackPaused { get; init; }

        public bool IsGifSpecificFramePlaying { get; init; }

        public string GifSpecificFrameCountdownText { get; init; } = string.Empty;

        public bool IsGifTrimRangeVisible { get; init; }

        public int GifTrimRangeMinimum { get; init; }

        public int GifTrimRangeMaximum { get; init; }

        public int SelectedGifTrimStartIndex { get; init; }

        public int SelectedGifTrimEndIndex { get; init; }

        public bool IsSvgPreviewVisible { get; init; }

        public bool IsSvgBackgroundToggleVisible { get; init; }

        public bool IsSvgBackgroundToggleEnabled { get; init; }

        public bool IsSvgBackgroundRequiredHintVisible { get; init; }

        public bool IsSvgBackgroundColorVisible { get; init; }

        public bool IsIconPreviewVisible { get; init; }

        public bool IsIconTransparencyToggleVisible { get; init; }

        public bool IsIconBackgroundColorVisible { get; init; }
    }
}
