using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ImvixPro.AI.Inpainting.Inference;
using ImvixPro.AI.Matting.Inference;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.ViewModels;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    internal sealed record PreviewWindowRequest(
        string FilePath,
        bool UseBackground,
        string BackgroundColor,
        GifFrameRangeSelection? GifFrameRange,
        int InitialPdfPageIndex,
        int PdfPageCount,
        string UiLanguageCode,
        Func<PreviewSessionState>? PreviewSessionStateProvider,
        Action<bool>? PreviewAiBusyChanged,
        bool IsSourceAiEnhancementEligible,
        bool IsSourceAiInpaintingEligible,
        bool IsSourceAiMattingEligible);

    internal sealed record FileDetailWindowRequest(
        ImageItemViewModel Image,
        string LanguageCode);

    internal sealed record PdfUnlockDialogRequest(
        string Title,
        string Message,
        string PasswordPlaceholder,
        string ConfirmText,
        string CancelText,
        Func<string, Task<PdfUnlockAttemptResult>> UnlockAsync);

    internal enum ShellImageRequestOrigin
    {
        SenderDataContext,
        SelectedImageFallback
    }

    internal readonly record struct ShellImageRequestSource(
        ImageItemViewModel Image,
        ShellImageRequestOrigin Origin);

    internal sealed class MainWindowShellCoordinator
    {
        private readonly ImagePreviewWindowServices _imagePreviewWindowServices;
        private readonly FileDetailWindowServices _fileDetailWindowServices;
        private bool _isExplicitExitRequested;
        private bool _isRunningInstanceWarningVisible;
        private bool _isCompletionDialogVisible;

        public MainWindowShellCoordinator(
            ImagePreviewWindowServices imagePreviewWindowServices,
            FileDetailWindowServices fileDetailWindowServices)
        {
            _imagePreviewWindowServices = imagePreviewWindowServices ?? throw new ArgumentNullException(nameof(imagePreviewWindowServices));
            _fileDetailWindowServices = fileDetailWindowServices ?? throw new ArgumentNullException(nameof(fileDetailWindowServices));
        }

        public async Task BringToFrontAsync(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            owner.ShowInTaskbar = true;

            if (!owner.IsVisible)
            {
                owner.Show();
            }

            if (owner.WindowState == WindowState.Minimized)
            {
                owner.WindowState = WindowState.Normal;
            }

            owner.Activate();

            var wasTopmost = owner.Topmost;
            owner.Topmost = true;
            owner.Activate();
            await Task.Delay(120);
            owner.Topmost = wasTopmost;
        }

        public async Task ShowRunningInstanceWarningAsync(Window owner, MainWindowViewModel? viewModel)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (viewModel is null || _isRunningInstanceWarningVisible)
            {
                return;
            }

            var dialog = new RunningInstanceWarningWindow(
                viewModel.AlreadyRunningTitleText,
                viewModel.AlreadyRunningMessageText,
                viewModel.CloseText)
            {
                FlowDirection = owner.FlowDirection
            };

            _isRunningInstanceWarningVisible = true;
            try
            {
                await dialog.ShowDialog(owner);
            }
            finally
            {
                _isRunningInstanceWarningVisible = false;
            }
        }

        public async Task ShowPendingCompletionDialogAsync(Window owner, NotificationState? notificationState)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (_isCompletionDialogVisible || !owner.IsVisible)
            {
                return;
            }

            var dialogRequest = notificationState?.PendingDialogRequest;
            if (dialogRequest is null)
            {
                return;
            }

            var dialog = new ConversionSummaryWindow(
                dialogRequest.Title,
                dialogRequest.SummaryText,
                dialogRequest.CloseButtonText)
            {
                FlowDirection = owner.FlowDirection
            };

            _isCompletionDialogVisible = true;
            try
            {
                await dialog.ShowDialog(owner);
            }
            finally
            {
                notificationState?.ClearPendingDialogRequest(dialogRequest);
                _isCompletionDialogVisible = false;

                if (notificationState?.HasPendingDialogRequest == true)
                {
                    _ = ShowPendingCompletionDialogAsync(owner, notificationState);
                }
            }
        }

        public bool ShouldHideToTray(bool keepRunningInTray, WindowCloseReason closeReason)
        {
            return keepRunningInTray &&
                   !_isExplicitExitRequested &&
                   closeReason is WindowCloseReason.WindowClosing or WindowCloseReason.Undefined;
        }

        public void HideToTray(Window owner, Action updateTrayIconState)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(updateTrayIconState);

            if (owner.WindowState == WindowState.Minimized)
            {
                owner.WindowState = WindowState.Normal;
            }

            owner.ShowInTaskbar = false;
            owner.Hide();
            updateTrayIconState();
        }

        public async Task RestoreFromTrayAsync(Window owner, NotificationState? notificationState)
        {
            ArgumentNullException.ThrowIfNull(owner);

            owner.ShowInTaskbar = true;

            if (!owner.IsVisible)
            {
                owner.Show();
            }

            owner.WindowState = WindowState.Normal;
            owner.Activate();

            await ShowPendingCompletionDialogAsync(owner, notificationState);
        }

        public void ExitApplication(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            _isExplicitExitRequested = true;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (!desktop.TryShutdown(0))
                {
                    _isExplicitExitRequested = false;
                }

                return;
            }

            owner.Close();
        }

        public async Task OpenPreviewAsync(
            Window owner,
            MainWindowViewModel? viewModel,
            ImageItemViewModel image,
            bool includeGifTrimRange)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(image);

            if (image.NeedsPdfUnlock)
            {
                await ShowPdfUnlockDialogAsync(owner, viewModel, image);
                return;
            }

            ShowPreviewWindow(owner, CreatePreviewRequest(viewModel, image, includeGifTrimRange));
        }

        public async Task OpenPreviewFromSourceAsync(
            Window owner,
            MainWindowViewModel? viewModel,
            object? source,
            bool includeGifTrimRange,
            bool allowSelectedImageFallback,
            bool skipAnimatedGif)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (!TryCreateImageRequestSource(
                    source,
                    allowSelectedImageFallback ? viewModel?.SelectedImage : null,
                    out var request))
            {
                return;
            }

            if (skipAnimatedGif && request.Image.IsAnimatedGif)
            {
                return;
            }

            await OpenPreviewAsync(owner, viewModel, request.Image, includeGifTrimRange);
        }

        public void OpenFileDetailWindow(Window owner, MainWindowViewModel? viewModel, ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(image);

            ShowFileDetailWindow(owner, CreateFileDetailRequest(viewModel, image));
        }

        public void OpenFileDetailWindowFromSource(
            Window owner,
            MainWindowViewModel? viewModel,
            object? source,
            bool allowSelectedImageFallback = false)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (!TryCreateImageRequestSource(
                    source,
                    allowSelectedImageFallback ? viewModel?.SelectedImage : null,
                    out var request))
            {
                return;
            }

            OpenFileDetailWindow(owner, viewModel, request.Image);
        }

        public async Task ShowPdfUnlockDialogAsync(Window owner, MainWindowViewModel? viewModel, ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(image);

            if (viewModel is null || !owner.IsVisible || !image.IsPdfDocument)
            {
                return;
            }

            if (!ReferenceEquals(viewModel.SelectedImage, image))
            {
                viewModel.SelectedImage = image;
            }

            var request = CreatePdfUnlockDialogRequest(viewModel, image);
            if (request is null)
            {
                return;
            }

            var dialog = new PdfUnlockWindow(
                request.Title,
                request.Message,
                request.PasswordPlaceholder,
                request.ConfirmText,
                request.CancelText,
                request.UnlockAsync)
            {
                FlowDirection = owner.FlowDirection
            };

            await dialog.ShowDialog<bool>(owner);
        }

        public async Task ShowPdfUnlockDialogFromSourceAsync(
            Window owner,
            MainWindowViewModel? viewModel,
            object? source,
            bool allowSelectedImageFallback = false)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (!TryCreateImageRequestSource(
                    source,
                    allowSelectedImageFallback ? viewModel?.SelectedImage : null,
                    out var request))
            {
                return;
            }

            await ShowPdfUnlockDialogAsync(owner, viewModel, request.Image);
        }

        internal static bool TryCreateImageRequestSource(
            object? source,
            ImageItemViewModel? selectedImageFallback,
            out ShellImageRequestSource request)
        {
            if (source is Control { DataContext: ImageItemViewModel image })
            {
                request = new ShellImageRequestSource(image, ShellImageRequestOrigin.SenderDataContext);
                return true;
            }

            if (selectedImageFallback is not null)
            {
                request = new ShellImageRequestSource(selectedImageFallback, ShellImageRequestOrigin.SelectedImageFallback);
                return true;
            }

            request = default;
            return false;
        }

        internal PreviewWindowRequest CreatePreviewRequest(
            MainWindowViewModel? viewModel,
            ImageItemViewModel image,
            bool includeGifTrimRange)
        {
            ArgumentNullException.ThrowIfNull(image);

            var isSelectedImage = viewModel?.SelectedImage?.FilePath.Equals(image.FilePath, StringComparison.OrdinalIgnoreCase) == true;
            GifFrameRangeSelection? gifTrimRange = null;
            if (includeGifTrimRange &&
                isSelectedImage &&
                viewModel?.IsGifTrimRangeVisible == true)
            {
                gifTrimRange = new GifFrameRangeSelection(
                    viewModel.SelectedGifTrimStartIndex,
                    viewModel.SelectedGifTrimEndIndex);
            }

            var previewSettings = viewModel?.GetPreviewRenderSettings(image.FilePath) ?? (false, "#FFFFFFFF");
            var pdfPageIndex = isSelectedImage ? viewModel!.SelectedPdfPageIndex : 0;

            return new PreviewWindowRequest(
                image.FilePath,
                previewSettings.Item1,
                previewSettings.Item2,
                gifTrimRange,
                image.IsPdfDocument ? pdfPageIndex : 0,
                image.IsPdfDocument ? image.PdfPageCount : 0,
                viewModel?.CurrentLanguageCode ?? "en-US",
                viewModel is null ? null : new Func<PreviewSessionState>(viewModel.CreatePreviewSessionState),
                viewModel is null ? null : new Action<bool>(viewModel.SetPreviewAiBusy),
                AiImageEnhancementService.IsEligible(image),
                AiInpaintingService.IsEligible(image),
                AiMattingService.IsEligible(image));
        }

        internal FileDetailWindowRequest CreateFileDetailRequest(MainWindowViewModel? viewModel, ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(image);

            return new FileDetailWindowRequest(
                image,
                viewModel?.CurrentLanguageCode ?? "en-US");
        }

        internal PdfUnlockDialogRequest? CreatePdfUnlockDialogRequest(MainWindowViewModel? viewModel, ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (viewModel is null || !image.IsPdfDocument || !image.NeedsPdfUnlock)
            {
                return null;
            }

            return new PdfUnlockDialogRequest(
                viewModel.TranslateText("PdfUnlockDialogTitle"),
                string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.TranslateText("PdfUnlockDialogMessageTemplate"),
                    image.FileName),
                viewModel.TranslateText("PdfUnlockPasswordPlaceholder"),
                viewModel.TranslateText("PdfUnlockConfirm"),
                viewModel.CancelActionText,
                password => viewModel.UnlockPdfAsync(image, password));
        }

        private void ShowPreviewWindow(Window owner, PreviewWindowRequest request)
        {
            var previewWindow = new ImagePreviewWindow(
                request.FilePath,
                request.UseBackground,
                request.BackgroundColor,
                request.GifFrameRange,
                request.InitialPdfPageIndex,
                request.PdfPageCount,
                uiLanguageCode: request.UiLanguageCode,
                ocrLanguageOption: PreviewOcrLanguageOption.Auto,
                previewSessionStateProvider: request.PreviewSessionStateProvider,
                previewAiBusyChanged: request.PreviewAiBusyChanged,
                isSourceAiEnhancementEligible: request.IsSourceAiEnhancementEligible,
                isSourceAiInpaintingEligible: request.IsSourceAiInpaintingEligible,
                isSourceAiMattingEligible: request.IsSourceAiMattingEligible,
                services: _imagePreviewWindowServices)
            {
                FlowDirection = owner.FlowDirection
            };

            previewWindow.Show(owner);
        }

        private void ShowFileDetailWindow(Window owner, FileDetailWindowRequest request)
        {
            var window = new FileDetailWindow(
                request.Image,
                request.LanguageCode,
                _fileDetailWindowServices)
            {
                FlowDirection = owner.FlowDirection
            };

            window.Show(owner);
        }
    }
}
