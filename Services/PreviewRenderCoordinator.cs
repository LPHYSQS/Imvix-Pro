using Avalonia.Media.Imaging;
using ImvixPro.Models;
using System;
using System.Threading.Tasks;

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

        void RefreshPdfUiState();

        void RefreshGifPdfUiState();

        void RefreshGifSpecificFrameUiState();

        void RefreshGifTrimUiState();

        void WarmAllGifPreviewsIfNeeded();

        void RefreshSelectedPdfPreview(bool preferImmediatePreview);

        bool ShouldLoadSelectedPsdPreviewAsync(ImageItemViewModel image);

        void RefreshSelectedPsdPreviewAsync(bool preferImmediatePreview, bool useThumbnailPlaceholder);

        Bitmap? CreatePreviewBitmap(string filePath, int maxWidth);

        void SetSelectedPreview(Bitmap? preview);

        bool ShouldLoadGifPreviewFrames();

        Task LoadGifPreviewAsync(string filePath);

        void IncrementGifPreviewRequestId();

        void PrepareSelectedAnimatedGifPreview(string filePath);

        bool TryApplySelectedGifTrimPreviewRange();

        bool TryApplySelectedGifSpecificFramePreview();

        bool IsGifSpecificFramePlaybackActive();

        bool CanToggleGifSpecificFramePlayback();

        bool HasReadyGifSpecificFramePlaybackFrames();

        void SetGifSpecificFramePlaybackActive(bool isPlaying);

        void StartGifSpecificFramePlayback();

        void PauseGifSpecificFramePlayback();

        bool ShouldRefreshSelectedConfigurablePreview(ImageItemViewModel image);

        void RefreshSelectedConfigurablePreview();
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
            else if (image.IsAnimatedGif)
            {
                RefreshSelectedAnimatedGifPreview(image, context);
            }
            else if (context.ShouldLoadSelectedPsdPreviewAsync(image))
            {
                context.RefreshSelectedPsdPreviewAsync(preferImmediatePreview: true, useThumbnailPlaceholder: true);
            }
            else
            {
                context.SetSelectedPreview(context.CreatePreviewBitmap(image.FilePath, SelectedPreviewWidth));
            }
        }

        public void HandleOutputFormatChanged(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.RefreshGifHandlingModeOptions();
            context.RestoreGifTrimSelection(image);
            context.RefreshPdfUiState();

            RefreshPreviewForCurrentConfiguration(
                image,
                context,
                preferImmediatePdfPreview: true);
        }

        public void HandleGifHandlingModeChanged(
            ImageItemViewModel? image,
            IPreviewRenderContext context,
            GifHandlingMode mode)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.RefreshGifPdfUiState();
            context.RefreshGifSpecificFrameUiState();
            context.RefreshGifTrimUiState();

            if (image is null || !image.IsAnimatedGif)
            {
                return;
            }

            if (mode is GifHandlingMode.AllFrames or GifHandlingMode.SpecificFrame)
            {
                context.WarmAllGifPreviewsIfNeeded();
            }

            RefreshSelectedAnimatedGifPreview(image, context);
        }

        public void HandleBackgroundSettingsChanged(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            RefreshConfigurablePreview(image, context);
        }

        public void HandlePdfPageSelectionChanged(
            ImageItemViewModel? image,
            IPreviewRenderContext context,
            bool preferImmediatePreview)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (image is null || !image.IsPdfDocument)
            {
                return;
            }

            context.RefreshSelectedPdfPreview(preferImmediatePreview);
        }

        public void HandleGifTrimSelectionChanged(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (image is null || !image.IsAnimatedGif)
            {
                return;
            }

            if (!context.TryApplySelectedGifTrimPreviewRange())
            {
                _ = context.LoadGifPreviewAsync(image.FilePath);
            }
        }

        public void HandleGifSpecificFrameSelectionChanged(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (image is null || !image.IsAnimatedGif)
            {
                return;
            }

            if (!context.TryApplySelectedGifSpecificFramePreview())
            {
                _ = context.LoadGifPreviewAsync(image.FilePath);
            }
        }

        public async Task HandleGifSpecificFramePlaybackToggleAsync(
            ImageItemViewModel? image,
            IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.IsGifSpecificFramePlaybackActive())
            {
                context.PauseGifSpecificFramePlayback();
                return;
            }

            if (image is null || !image.IsAnimatedGif || !context.CanToggleGifSpecificFramePlayback())
            {
                return;
            }

            context.SetGifSpecificFramePlaybackActive(true);

            if (context.HasReadyGifSpecificFramePlaybackFrames())
            {
                context.StartGifSpecificFramePlayback();
                return;
            }

            await context.LoadGifPreviewAsync(image.FilePath).ConfigureAwait(false);

            if (!context.HasReadyGifSpecificFramePlaybackFrames())
            {
                context.SetGifSpecificFramePlaybackActive(false);
            }
        }

        public void HandleGifSpecificFrameRestoreCompleted(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (image is null || !image.IsAnimatedGif)
            {
                return;
            }

            context.TryApplySelectedGifSpecificFramePreview();
        }

        private static void RefreshPreviewForCurrentConfiguration(
            ImageItemViewModel? image,
            IPreviewRenderContext context,
            bool preferImmediatePdfPreview)
        {
            if (image is null)
            {
                return;
            }

            if (image.IsAnimatedGif)
            {
                RefreshSelectedAnimatedGifPreview(image, context);
                return;
            }

            if (image.IsPdfDocument)
            {
                context.RefreshSelectedPdfPreview(preferImmediatePdfPreview);
                return;
            }

            RefreshConfigurablePreview(image, context);
        }

        private static void RefreshConfigurablePreview(ImageItemViewModel? image, IPreviewRenderContext context)
        {
            if (image is null || !context.ShouldRefreshSelectedConfigurablePreview(image))
            {
                return;
            }

            context.RefreshSelectedConfigurablePreview();
        }

        private static void RefreshSelectedAnimatedGifPreview(ImageItemViewModel image, IPreviewRenderContext context)
        {
            context.PrepareSelectedAnimatedGifPreview(image.FilePath);

            if (context.ShouldLoadGifPreviewFrames())
            {
                _ = context.LoadGifPreviewAsync(image.FilePath);
            }
            else
            {
                context.IncrementGifPreviewRequestId();
            }
        }
    }
}
