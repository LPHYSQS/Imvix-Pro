using Avalonia.Media.Imaging;
using ImvixPro.Models;
using System;

namespace ImvixPro.Services
{
    internal interface IPreviewRenderContext
    {
        void CancelPendingPdfPreviewRender();

        void CancelPendingSelectedPsdPreviewRender();

        void ClearSelectedPreview();

        void RefreshGifHandlingModeOptions();

        void RestoreGifSpecificFrameSelection(ImageItemViewModel? image);

        void RestoreGifTrimSelection(ImageItemViewModel? image);

        void RestorePdfSelection(ImageItemViewModel? image);

        void RefreshSelectedPdfPreview(bool preferImmediatePreview);

        bool ShouldLoadSelectedPsdPreviewAsync(ImageItemViewModel image);

        void RefreshSelectedPsdPreviewAsync(bool preferImmediatePreview, bool useThumbnailPlaceholder);

        Bitmap? CreatePreviewBitmap(string filePath, int maxWidth);

        void SetSelectedPreview(Bitmap? preview);

        bool ShouldLoadGifPreviewFrames();

        void LoadGifPreview(string filePath);

        void IncrementGifPreviewRequestId();
    }

    internal sealed class PreviewRenderCoordinator
    {
        private const int SelectedPreviewWidth = 760;

        public void HandleSelectedImageChanged(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.CancelPendingPdfPreviewRender();
            context.CancelPendingSelectedPsdPreviewRender();
            context.ClearSelectedPreview();
            context.RefreshGifHandlingModeOptions();
            context.RestoreGifSpecificFrameSelection(image);
            context.RestoreGifTrimSelection(image);
            context.RestorePdfSelection(image);

            if (image is null)
            {
                return;
            }

            if (image.IsPdfDocument)
            {
                context.RefreshSelectedPdfPreview(preferImmediatePreview: true);
            }
            else if (context.ShouldLoadSelectedPsdPreviewAsync(image))
            {
                context.RefreshSelectedPsdPreviewAsync(preferImmediatePreview: true, useThumbnailPlaceholder: true);
            }
            else
            {
                context.SetSelectedPreview(context.CreatePreviewBitmap(image.FilePath, SelectedPreviewWidth));
            }

            if (!image.IsAnimatedGif)
            {
                return;
            }

            if (context.ShouldLoadGifPreviewFrames())
            {
                context.LoadGifPreview(image.FilePath);
            }
            else
            {
                context.IncrementGifPreviewRequestId();
            }
        }
    }
}
