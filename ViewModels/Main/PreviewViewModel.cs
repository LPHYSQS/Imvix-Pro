using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PsdModule;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private int _previewAiBusyCount;

        public string PreviewAiBusyText => T("StatusAiPreviewEnhancing");

        [ObservableProperty]
        private bool isPreviewAiBusy;

        partial void OnSelectedPreviewChanged(Bitmap? value)
        {
            RefreshPreviewSelectionState();
        }

        public PreviewSessionState CreatePreviewSessionState()
        {
            return new PreviewSessionState
            {
                JobDefinition = BuildCurrentJobDefinition(),
                PreviewToolState = BuildCurrentPreviewToolState()
            };
        }

        public void SetPreviewAiBusy(bool isBusy)
        {
            void Apply()
            {
                if (isBusy)
                {
                    _previewAiBusyCount++;
                }
                else
                {
                    _previewAiBusyCount = Math.Max(0, _previewAiBusyCount - 1);
                }

                IsPreviewAiBusy = _previewAiBusyCount > 0;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
                return;
            }

            Dispatcher.UIThread.Post(Apply);
        }

        private void RefreshLocalizedPropertiesPreviewAi()
        {
            OnPropertyChanged(nameof(PreviewAiBusyText));
            RefreshPreviewSelectionState();
        }

        private void RefreshGifLabels()
        {
            foreach (var image in Images)
            {
                UpdateGifLabels(image);
            }
        }

        private void UpdateGifLabels(ImageItemViewModel image)
        {
            if (!image.IsAnimatedGif)
            {
                image.GifBadgeText = string.Empty;
                image.GifFrameCountText = string.Empty;
                return;
            }

            image.GifBadgeText = GifAnimatedLabelText;
            image.GifFrameCountText = string.Format(CultureInfo.CurrentCulture, GifFrameCountTemplateText, image.GifFrameCount);
        }

        private void WarmGifPreviewIfNeeded(ImageItemViewModel image)
        {
            if (!image.IsAnimatedGif)
            {
                return;
            }

            if (SelectedOutputFormat == OutputImageFormat.Gif)
            {
                ImageConversionService.WarmGifPreview(image.FilePath, 760);
                return;
            }

            if (SelectedGifHandlingMode is not (GifHandlingMode.AllFrames or GifHandlingMode.SpecificFrame))
            {
                return;
            }

            ImageConversionService.WarmGifPreview(image.FilePath, 760);
        }

        private void WarmAllGifPreviewsIfNeeded()
        {
            if (SelectedOutputFormat != OutputImageFormat.Gif &&
                SelectedGifHandlingMode is not (GifHandlingMode.AllFrames or GifHandlingMode.SpecificFrame))
            {
                return;
            }

            foreach (var image in Images)
            {
                if (image.IsAnimatedGif)
                {
                    ImageConversionService.WarmGifPreview(image.FilePath, 760);
                }
            }
        }

        private bool ShouldAnimateGifPreview()
        {
            return SelectedOutputFormat == OutputImageFormat.Gif || SelectedGifHandlingMode == GifHandlingMode.AllFrames;
        }

        private void RefreshSelectedAnimatedGifPreview()
        {
            if (SelectedImage is null || !SelectedImage.IsAnimatedGif)
            {
                RefreshGifSpecificFrameUiState();
                RefreshGifTrimUiState();
                return;
            }

            ClearSelectedPreview();
            SelectedPreview = ImageConversionService.TryCreatePreview(SelectedImage.FilePath, 760, SvgUseBackground, EffectiveSvgBackgroundColor);

            if (ShouldLoadGifPreviewFrames())
            {
                _ = LoadGifPreviewAsync(SelectedImage.FilePath);
            }
            else
            {
                Interlocked.Increment(ref _gifPreviewRequestId);
            }

            RefreshGifSpecificFrameUiState();
            RefreshGifTrimUiState();
        }

        private async Task LoadGifPreviewAsync(string filePath)
        {
            var requestId = Interlocked.Increment(ref _gifPreviewRequestId);

            var handle = await ImageConversionService.GetOrLoadGifPreviewAsync(filePath, 760);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (requestId != _gifPreviewRequestId ||
                    SelectedImage is null ||
                    !SelectedImage.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                {
                    handle?.Dispose();
                    return;
                }

                if (handle is null || handle.Frames.Count == 0 || handle.Frames.Count != handle.Durations.Count)
                {
                    handle?.Dispose();
                    return;
                }

                ApplyLoadedGifPreviewHandle(handle);
            });
        }

        private void StartGifPreview(ImageConversionService.GifPreviewHandle handle)
        {
            ResetGifPreviewState(resetSpecificFramePlaybackState: true);

            var frames = handle.Frames;
            var durations = handle.Durations;
            if (frames.Count == 0 || frames.Count != durations.Count)
            {
                handle.Dispose();
                return;
            }

            _gifPreviewHandle = handle;
            _gifPreviewFrames = frames;
            _gifPreviewDurations = durations;
            _gifPreviewIndex = IsGifTrimRangeVisible
                ? GetCurrentGifTrimSelection().StartIndex
                : 0;
            SelectedPreview?.Dispose();
            SelectedPreview = frames[_gifPreviewIndex];
            _gifPreviewTimer.Interval = ClampGifDuration(durations[_gifPreviewIndex]);
            _gifPreviewTimer.Start();
        }

        private void StopGifPreview()
        {
            ResetGifPreviewState(resetSpecificFramePlaybackState: true);
        }

        private void ResetGifPreviewState(bool resetSpecificFramePlaybackState)
        {
            if (_gifPreviewTimer.IsEnabled)
            {
                _gifPreviewTimer.Stop();
            }

            _gifPreviewHandle?.Dispose();
            _gifPreviewHandle = null;
            _gifPreviewFrames = null;
            _gifPreviewDurations = null;
            _gifPreviewIndex = 0;

            if (resetSpecificFramePlaybackState && IsGifSpecificFramePlaying)
            {
                IsGifSpecificFramePlaying = false;
            }
        }

        private void ClearSelectedPreview()
        {
            if (_gifPreviewFrames is not null)
            {
                StopGifPreview();
                SelectedPreview = null;
                return;
            }

            SelectedPreview?.Dispose();
            SelectedPreview = null;
        }

        private void OnGifPreviewTick(object? sender, EventArgs e)
        {
            if (_gifPreviewFrames is null || _gifPreviewDurations is null || _gifPreviewFrames.Count == 0)
            {
                StopGifPreview();
                SelectedPreview = null;
                return;
            }

            if (IsGifTrimRangeVisible)
            {
                var selection = GetCurrentGifTrimSelection();
                if (_gifPreviewIndex < selection.StartIndex || _gifPreviewIndex > selection.EndIndex)
                {
                    _gifPreviewIndex = selection.StartIndex;
                }
                else
                {
                    _gifPreviewIndex = _gifPreviewIndex >= selection.EndIndex
                        ? selection.StartIndex
                        : _gifPreviewIndex + 1;
                }
            }
            else
            {
                _gifPreviewIndex = (_gifPreviewIndex + 1) % _gifPreviewFrames.Count;
            }

            SelectedPreview = _gifPreviewFrames[_gifPreviewIndex];

            if (SelectedGifHandlingMode == GifHandlingMode.SpecificFrame && IsGifSpecificFrameControlsVisible)
            {
                SetGifSpecificFrameIndex(_gifPreviewIndex, persist: false, refreshPreview: false);
            }

            if (_gifPreviewIndex < _gifPreviewDurations.Count)
            {
                _gifPreviewTimer.Interval = ClampGifDuration(_gifPreviewDurations[_gifPreviewIndex]);
            }
        }

        private static TimeSpan ClampGifDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero || duration.TotalMilliseconds <= 20)
            {
                return TimeSpan.FromMilliseconds(100);
            }

            return duration;
        }

        public (bool UseBackground, string BackgroundColor) GetPreviewRenderSettings(string filePath)
        {
            if (IsBackgroundFillSource(filePath))
            {
                var useBackground = ShouldForceBackgroundColorEditor(filePath) || SvgUseBackground;
                return (useBackground, EffectiveSvgBackgroundColor);
            }

            if (IsIconSource(filePath))
            {
                var useBackground = !ImageConversionService.OutputFormatSupportsTransparency(SelectedOutputFormat) || !IconUseTransparency;
                return (useBackground, EffectiveIconBackgroundColor);
            }

            return (false, "#FFFFFFFF");
        }

        private Bitmap? CreatePreviewBitmap(string filePath, int maxWidth)
        {
            var renderSettings = GetPreviewRenderSettings(filePath);
            return ImageConversionService.TryCreatePreview(filePath, maxWidth, renderSettings.UseBackground, renderSettings.BackgroundColor);
        }

        private bool ShouldRefreshSelectedConfigurablePreview(ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (image.IsPdfDocument || image.IsAnimatedGif)
            {
                return false;
            }

            return IsBackgroundFillSource(image) || IsIconSource(image.FilePath);
        }

        private void RefreshSelectedConfigurablePreview()
        {
            if (SelectedImage is null || !ShouldRefreshSelectedConfigurablePreview(SelectedImage))
            {
                return;
            }

            if (ShouldLoadSelectedPsdPreviewAsync(SelectedImage))
            {
                RefreshSelectedPsdPreviewAsync(preferImmediatePreview: false, useThumbnailPlaceholder: false);
                return;
            }

            SelectedPreview?.Dispose();
            SelectedPreview = CreatePreviewBitmap(SelectedImage.FilePath, 760);
        }

        private void RefreshPreviewSelectionState()
        {
            PreviewSelectionState.Apply(BuildPreviewSelectionSnapshot());
        }

        private PreviewSelectionSnapshot BuildPreviewSelectionSnapshot()
        {
            return new PreviewSelectionSnapshot
            {
                SelectedPreview = SelectedPreview,
                PreviewSelectionFileText = SelectedImage?.FileName ?? T("NoCurrentFile"),
                IsPreviewWindowHintVisible = SelectedImage is not null,
                IsPdfSelected = IsPdfSelected,
                IsPdfNavigationVisible = IsPdfNavigationVisible,
                CanGoToPreviousPdfPage = CanGoToPreviousPdfPage,
                CanGoToNextPdfPage = CanGoToNextPdfPage,
                IsSelectedPdfLocked = IsSelectedPdfLocked,
                IsSelectedPdfUnlocked = IsSelectedPdfUnlocked,
                PdfPageIndicatorText = PdfPageIndicatorText,
                PdfPageRangeIndicatorText = PdfPageRangeIndicatorText,
                PdfLockedPreviewTitleText = PdfLockedPreviewTitleText,
                PdfLockedPreviewDescriptionText = PdfLockedPreviewDescriptionText,
                PdfLockedActionHintText = PdfLockedActionHintText,
                IsPdfImageExportVisible = IsPdfImageExportVisible,
                IsPdfDocumentExportVisible = IsPdfDocumentExportVisible,
                IsPdfImageModeSelectorVisible = IsPdfImageModeSelectorVisible,
                IsPdfDocumentModeSelectorVisible = IsPdfDocumentModeSelectorVisible,
                IsPdfImagePageSliderVisible = IsPdfImagePageSliderVisible,
                IsPdfDocumentPageSliderVisible = IsPdfDocumentPageSliderVisible,
                IsPdfDocumentRangeSliderVisible = IsPdfDocumentRangeSliderVisible,
                IsSinglePagePdf = IsSinglePagePdf,
                IsPdfImageAllPagesMode = IsPdfImageAllPagesMode,
                IsPdfImageCurrentPageMode = IsPdfImageCurrentPageMode,
                IsPdfDocumentAllPagesMode = IsPdfDocumentAllPagesMode,
                IsPdfDocumentCurrentPageMode = IsPdfDocumentCurrentPageMode,
                IsPdfDocumentPageRangeMode = IsPdfDocumentPageRangeMode,
                IsPdfDocumentSplitSinglePagesMode = IsPdfDocumentSplitSinglePagesMode,
                PdfPageMinimum = PdfPageMinimum,
                PdfPageMaximum = PdfPageMaximum,
                PdfRangeMinimum = PdfRangeMinimum,
                PdfRangeMaximum = PdfRangeMaximum,
                SelectedPdfPageIndex = SelectedPdfPageIndex,
                SelectedPdfRangeStartIndex = SelectedPdfRangeStartIndex,
                SelectedPdfRangeEndIndex = SelectedPdfRangeEndIndex,
                IsGifPreviewVisible = IsGifPreviewVisible,
                IsGifHandlingSelectorVisible = IsGifHandlingSelectorVisible,
                IsGifPdfModeSelectorVisible = IsGifPdfModeSelectorVisible,
                IsGifPdfExportAllFramesMode = IsGifPdfExportAllFramesMode,
                IsGifPdfExportCurrentFrameMode = IsGifPdfExportCurrentFrameMode,
                IsGifSpecificFrameControlsVisible = IsGifSpecificFrameControlsVisible,
                GifSpecificFrameMaximum = GifSpecificFrameMaximum,
                GifSpecificFrameSliderValue = GifSpecificFrameSliderValue,
                CanAdjustGifSpecificFrame = CanAdjustGifSpecificFrame,
                IsGifSpecificFramePlaybackPaused = IsGifSpecificFramePlaybackPaused,
                IsGifSpecificFramePlaying = IsGifSpecificFramePlaying,
                GifSpecificFrameCountdownText = GifSpecificFrameCountdownText,
                IsGifTrimRangeVisible = IsGifTrimRangeVisible,
                GifTrimRangeMinimum = GifTrimRangeMinimum,
                GifTrimRangeMaximum = GifTrimRangeMaximum,
                SelectedGifTrimStartIndex = SelectedGifTrimStartIndex,
                SelectedGifTrimEndIndex = SelectedGifTrimEndIndex,
                IsSvgPreviewVisible = IsSvgPreviewVisible,
                IsSvgBackgroundToggleVisible = IsSvgBackgroundToggleVisible,
                IsSvgBackgroundToggleEnabled = IsSvgBackgroundToggleEnabled,
                IsSvgBackgroundRequiredHintVisible = IsSvgBackgroundRequiredHintVisible,
                IsSvgBackgroundColorVisible = IsSvgBackgroundColorVisible,
                IsIconPreviewVisible = IsIconPreviewVisible,
                IsIconTransparencyToggleVisible = IsIconTransparencyToggleVisible,
                IsIconBackgroundColorVisible = IsIconBackgroundColorVisible
            };
        }
    }
}
