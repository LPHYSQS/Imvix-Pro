using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Models;
using System;

namespace ImvixPro.ViewModels
{
    public partial class PreviewSelectionState : ObservableObject
    {
        [ObservableProperty]
        private Bitmap? selectedPreview;

        [ObservableProperty]
        private string previewSelectionFileText = string.Empty;

        [ObservableProperty]
        private bool isPreviewWindowHintVisible;

        [ObservableProperty]
        private bool isPdfSelected;

        [ObservableProperty]
        private bool isPdfNavigationVisible;

        [ObservableProperty]
        private bool canGoToPreviousPdfPage;

        [ObservableProperty]
        private bool canGoToNextPdfPage;

        [ObservableProperty]
        private bool isSelectedPdfLocked;

        [ObservableProperty]
        private bool isSelectedPdfUnlocked;

        [ObservableProperty]
        private string pdfPageIndicatorText = string.Empty;

        [ObservableProperty]
        private string pdfPageRangeIndicatorText = string.Empty;

        [ObservableProperty]
        private string pdfLockedPreviewTitleText = string.Empty;

        [ObservableProperty]
        private string pdfLockedPreviewDescriptionText = string.Empty;

        [ObservableProperty]
        private string pdfLockedActionHintText = string.Empty;

        [ObservableProperty]
        private bool isPdfImageExportVisible;

        [ObservableProperty]
        private bool isPdfDocumentExportVisible;

        [ObservableProperty]
        private bool isPdfImageModeSelectorVisible;

        [ObservableProperty]
        private bool isPdfDocumentModeSelectorVisible;

        [ObservableProperty]
        private bool isPdfImagePageSliderVisible;

        [ObservableProperty]
        private bool isPdfDocumentPageSliderVisible;

        [ObservableProperty]
        private bool isPdfDocumentRangeSliderVisible;

        [ObservableProperty]
        private bool isSinglePagePdf;

        [ObservableProperty]
        private bool isPdfImageAllPagesMode;

        [ObservableProperty]
        private bool isPdfImageCurrentPageMode;

        [ObservableProperty]
        private bool isPdfDocumentAllPagesMode;

        [ObservableProperty]
        private bool isPdfDocumentCurrentPageMode;

        [ObservableProperty]
        private bool isPdfDocumentPageRangeMode;

        [ObservableProperty]
        private bool isPdfDocumentSplitSinglePagesMode;

        [ObservableProperty]
        private int pdfPageMinimum;

        [ObservableProperty]
        private int pdfPageMaximum;

        [ObservableProperty]
        private int pdfRangeMinimum;

        [ObservableProperty]
        private int pdfRangeMaximum;

        [ObservableProperty]
        private int selectedPdfPageIndex;

        [ObservableProperty]
        private int selectedPdfRangeStartIndex;

        [ObservableProperty]
        private int selectedPdfRangeEndIndex;

        [ObservableProperty]
        private bool isGifPreviewVisible;

        [ObservableProperty]
        private bool isGifHandlingSelectorVisible;

        [ObservableProperty]
        private bool isGifPdfModeSelectorVisible;

        [ObservableProperty]
        private bool isGifPdfExportAllFramesMode;

        [ObservableProperty]
        private bool isGifPdfExportCurrentFrameMode;

        [ObservableProperty]
        private bool isGifSpecificFrameControlsVisible;

        [ObservableProperty]
        private int gifSpecificFrameMaximum;

        [ObservableProperty]
        private double gifSpecificFrameSliderValue;

        [ObservableProperty]
        private bool canAdjustGifSpecificFrame;

        [ObservableProperty]
        private bool isGifSpecificFramePlaybackPaused;

        [ObservableProperty]
        private bool isGifSpecificFramePlaying;

        [ObservableProperty]
        private string gifSpecificFrameCountdownText = string.Empty;

        [ObservableProperty]
        private bool isGifTrimRangeVisible;

        [ObservableProperty]
        private int gifTrimRangeMinimum;

        [ObservableProperty]
        private int gifTrimRangeMaximum;

        [ObservableProperty]
        private int selectedGifTrimStartIndex;

        [ObservableProperty]
        private int selectedGifTrimEndIndex;

        [ObservableProperty]
        private bool isSvgPreviewVisible;

        [ObservableProperty]
        private bool isSvgBackgroundToggleVisible;

        [ObservableProperty]
        private bool isSvgBackgroundToggleEnabled;

        [ObservableProperty]
        private bool isSvgBackgroundRequiredHintVisible;

        [ObservableProperty]
        private bool isSvgBackgroundColorVisible;

        [ObservableProperty]
        private bool isIconPreviewVisible;

        [ObservableProperty]
        private bool isIconTransparencyToggleVisible;

        [ObservableProperty]
        private bool isIconBackgroundColorVisible;

        public void Apply(PreviewSelectionSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            SelectedPreview = snapshot.SelectedPreview;
            PreviewSelectionFileText = snapshot.PreviewSelectionFileText;
            IsPreviewWindowHintVisible = snapshot.IsPreviewWindowHintVisible;
            IsPdfSelected = snapshot.IsPdfSelected;
            IsPdfNavigationVisible = snapshot.IsPdfNavigationVisible;
            CanGoToPreviousPdfPage = snapshot.CanGoToPreviousPdfPage;
            CanGoToNextPdfPage = snapshot.CanGoToNextPdfPage;
            IsSelectedPdfLocked = snapshot.IsSelectedPdfLocked;
            IsSelectedPdfUnlocked = snapshot.IsSelectedPdfUnlocked;
            PdfPageIndicatorText = snapshot.PdfPageIndicatorText;
            PdfPageRangeIndicatorText = snapshot.PdfPageRangeIndicatorText;
            PdfLockedPreviewTitleText = snapshot.PdfLockedPreviewTitleText;
            PdfLockedPreviewDescriptionText = snapshot.PdfLockedPreviewDescriptionText;
            PdfLockedActionHintText = snapshot.PdfLockedActionHintText;
            IsPdfImageExportVisible = snapshot.IsPdfImageExportVisible;
            IsPdfDocumentExportVisible = snapshot.IsPdfDocumentExportVisible;
            IsPdfImageModeSelectorVisible = snapshot.IsPdfImageModeSelectorVisible;
            IsPdfDocumentModeSelectorVisible = snapshot.IsPdfDocumentModeSelectorVisible;
            IsPdfImagePageSliderVisible = snapshot.IsPdfImagePageSliderVisible;
            IsPdfDocumentPageSliderVisible = snapshot.IsPdfDocumentPageSliderVisible;
            IsPdfDocumentRangeSliderVisible = snapshot.IsPdfDocumentRangeSliderVisible;
            IsSinglePagePdf = snapshot.IsSinglePagePdf;
            IsPdfImageAllPagesMode = snapshot.IsPdfImageAllPagesMode;
            IsPdfImageCurrentPageMode = snapshot.IsPdfImageCurrentPageMode;
            IsPdfDocumentAllPagesMode = snapshot.IsPdfDocumentAllPagesMode;
            IsPdfDocumentCurrentPageMode = snapshot.IsPdfDocumentCurrentPageMode;
            IsPdfDocumentPageRangeMode = snapshot.IsPdfDocumentPageRangeMode;
            IsPdfDocumentSplitSinglePagesMode = snapshot.IsPdfDocumentSplitSinglePagesMode;
            PdfPageMinimum = snapshot.PdfPageMinimum;
            PdfPageMaximum = snapshot.PdfPageMaximum;
            PdfRangeMinimum = snapshot.PdfRangeMinimum;
            PdfRangeMaximum = snapshot.PdfRangeMaximum;
            SelectedPdfPageIndex = snapshot.SelectedPdfPageIndex;
            SelectedPdfRangeStartIndex = snapshot.SelectedPdfRangeStartIndex;
            SelectedPdfRangeEndIndex = snapshot.SelectedPdfRangeEndIndex;
            IsGifPreviewVisible = snapshot.IsGifPreviewVisible;
            IsGifHandlingSelectorVisible = snapshot.IsGifHandlingSelectorVisible;
            IsGifPdfModeSelectorVisible = snapshot.IsGifPdfModeSelectorVisible;
            IsGifPdfExportAllFramesMode = snapshot.IsGifPdfExportAllFramesMode;
            IsGifPdfExportCurrentFrameMode = snapshot.IsGifPdfExportCurrentFrameMode;
            IsGifSpecificFrameControlsVisible = snapshot.IsGifSpecificFrameControlsVisible;
            GifSpecificFrameMaximum = snapshot.GifSpecificFrameMaximum;
            GifSpecificFrameSliderValue = snapshot.GifSpecificFrameSliderValue;
            CanAdjustGifSpecificFrame = snapshot.CanAdjustGifSpecificFrame;
            IsGifSpecificFramePlaybackPaused = snapshot.IsGifSpecificFramePlaybackPaused;
            IsGifSpecificFramePlaying = snapshot.IsGifSpecificFramePlaying;
            GifSpecificFrameCountdownText = snapshot.GifSpecificFrameCountdownText;
            IsGifTrimRangeVisible = snapshot.IsGifTrimRangeVisible;
            GifTrimRangeMinimum = snapshot.GifTrimRangeMinimum;
            GifTrimRangeMaximum = snapshot.GifTrimRangeMaximum;
            SelectedGifTrimStartIndex = snapshot.SelectedGifTrimStartIndex;
            SelectedGifTrimEndIndex = snapshot.SelectedGifTrimEndIndex;
            IsSvgPreviewVisible = snapshot.IsSvgPreviewVisible;
            IsSvgBackgroundToggleVisible = snapshot.IsSvgBackgroundToggleVisible;
            IsSvgBackgroundToggleEnabled = snapshot.IsSvgBackgroundToggleEnabled;
            IsSvgBackgroundRequiredHintVisible = snapshot.IsSvgBackgroundRequiredHintVisible;
            IsSvgBackgroundColorVisible = snapshot.IsSvgBackgroundColorVisible;
            IsIconPreviewVisible = snapshot.IsIconPreviewVisible;
            IsIconTransparencyToggleVisible = snapshot.IsIconTransparencyToggleVisible;
            IsIconBackgroundColorVisible = snapshot.IsIconBackgroundColorVisible;
        }
    }
}
