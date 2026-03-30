using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ImvixPro.AI.Inpainting.Inference;
using ImvixPro.AI.Matting.Inference;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow : Window
    {
        private const int PdfPreviewLowQualityWidth = 520;
        private const int PdfPreviewHighQualityWidth = 1400;
        private const int PdfOcrRenderWidth = 2200;
        private const int StaticPreviewLowQualityWidth = 520;
        private const int StaticPreviewHighQualityWidth = 1400;
        private const double OcrPanelMinWidth = 340d;
        private const double OcrPanelMaxWidth = 460d;
        private const int OcrAnimationDurationMilliseconds = 220;
        private const double DefaultPreviewCompareRefreshRate = 60d;
        private const double PreviewCompareFrameToleranceMilliseconds = 0.75d;

        private readonly PdfRenderService _pdfRenderService;
        private readonly PreviewOcrToolController _previewOcrToolController;
        private readonly PreviewQrToolController _previewQrToolController;
        private readonly PreviewBarcodeToolController _barcodeToolController;
        private readonly LocalizationService _localizationService;
        private readonly AiInpaintingService _aiInpaintingService;
        private readonly AiMattingService _aiMattingService;
        private readonly DisplayRefreshRateService _displayRefreshRateService;
        private readonly ImageConversionService _imageConversionService;
        private readonly AppLogger _logger;
        private readonly DispatcherTimer _gifPreviewTimer = new();
        private readonly DispatcherTimer _toastTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1.6)
        };
        private readonly Stopwatch _previewCompareFrameStopwatch = Stopwatch.StartNew();
        private readonly TranslateTransform _aiCompareSplitterTransform = new();
        private readonly TranslateTransform _aiCompareThumbTransform = new();
        private readonly TranslateTransform _aiEraserCompareSplitterTransform = new();
        private readonly TranslateTransform _aiEraserCompareThumbTransform = new();
        private readonly TranslateTransform _aiMattingCompareSplitterTransform = new();
        private readonly TranslateTransform _aiMattingCompareThumbTransform = new();

        private Bitmap? _previewBitmap;
        private ImageConversionService.GifPreviewHandle? _gifPreviewHandle;
        private IReadOnlyList<Bitmap>? _gifPreviewFrames;
        private IReadOnlyList<TimeSpan>? _gifPreviewDurations;
        private int _gifPreviewIndex;
        private long _gifPreviewRequestId;
        private readonly GifFrameRangeSelection? _gifFrameRange;
        private bool _isClosed;
        private bool _isGifPlaying;
        private bool _suppressGifSliderChange;

        private string? _pdfFilePath;
        private int _pdfPageIndex;
        private int _pdfPageCount;
        private bool _isPdfDocument;
        private CancellationTokenSource? _pdfPreviewCts;
        private long _pdfPreviewRequestId;
        private string? _staticPreviewFilePath;
        private CancellationTokenSource? _staticPreviewCts;
        private long _staticPreviewRequestId;
        private bool _isPreviewCompareFrameRequested;
        private bool _isAiComparePointPending;
        private Point _pendingAiComparePoint;
        private bool _isAiEraserComparePointPending;
        private Point _pendingAiEraserComparePoint;
        private bool _isAiMattingComparePointPending;
        private Point _pendingAiMattingComparePoint;
        private double _previewCompareRefreshRate = DefaultPreviewCompareRefreshRate;
        private TimeSpan _previewCompareFrameInterval = TimeSpan.FromSeconds(1d / DefaultPreviewCompareRefreshRate);
        private TimeSpan _lastPreviewCompareCommitTime = TimeSpan.MinValue;

        private CancellationTokenSource? _ocrCts;
        private CancellationTokenSource? _ocrAnimationCts;
        private bool _isOcrBusy;
        private bool _isOcrPanelVisible;
        private string _ocrText = string.Empty;
        private string _copyAllText = string.Empty;
        private PreviewOcrLanguageOption _ocrLanguageOption = PreviewOcrLanguageOption.Auto;

        public ImagePreviewWindow()
            : this(AppServices.CreateImagePreviewWindowServices())
        {
        }

        internal ImagePreviewWindow(ImagePreviewWindowServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            _pdfRenderService = services.PdfRenderService ?? throw new ArgumentNullException(nameof(services.PdfRenderService));
            _previewOcrToolController = services.PreviewOcrToolController ?? throw new ArgumentNullException(nameof(services.PreviewOcrToolController));
            _previewQrToolController = services.PreviewQrToolController ?? throw new ArgumentNullException(nameof(services.PreviewQrToolController));
            _barcodeToolController = services.PreviewBarcodeToolController ?? throw new ArgumentNullException(nameof(services.PreviewBarcodeToolController));
            _localizationService = services.CreateLocalizationService?.Invoke() ?? throw new ArgumentNullException(nameof(services.CreateLocalizationService));
            _aiImageEnhancementService = services.AiImageEnhancementService ?? throw new ArgumentNullException(nameof(services.AiImageEnhancementService));
            _aiInpaintingService = services.AiInpaintingService ?? throw new ArgumentNullException(nameof(services.AiInpaintingService));
            _aiMattingService = services.AiMattingService ?? throw new ArgumentNullException(nameof(services.AiMattingService));
            _aiPreviewComparisonService = services.AiPreviewComparisonService ?? throw new ArgumentNullException(nameof(services.AiPreviewComparisonService));
            _displayRefreshRateService = services.DisplayRefreshRateService ?? throw new ArgumentNullException(nameof(services.DisplayRefreshRateService));
            _imageConversionService = services.ImageConversionService ?? throw new ArgumentNullException(nameof(services.ImageConversionService));
            _logger = services.Logger ?? throw new ArgumentNullException(nameof(services.Logger));
            InitializeComponent();
            _gifPreviewTimer.Tick += OnGifPreviewTick;
            _toastTimer.Tick += OnToastTimerTick;
            SizeChanged += OnWindowSizeChanged;
            InitializePreviewCompareInteractionPipeline();
        }

        public ImagePreviewWindow(
            string filePath,
            bool useBackground,
            string backgroundColor,
            GifFrameRangeSelection? gifFrameRange = null,
            int initialPdfPageIndex = 0,
            int pdfPageCount = 0,
            string uiLanguageCode = "en-US",
            PreviewOcrLanguageOption ocrLanguageOption = PreviewOcrLanguageOption.Auto,
            Func<PreviewSessionState>? previewSessionStateProvider = null,
            Action<bool>? previewAiBusyChanged = null,
            Action<int>? previewAiEraserBrushSizeChanged = null,
            bool isSourceAiEnhancementEligible = false,
            bool isSourceAiInpaintingEligible = false,
            bool isSourceAiMattingEligible = false)
            : this(
                filePath,
                useBackground,
                backgroundColor,
                gifFrameRange,
                initialPdfPageIndex,
                pdfPageCount,
                uiLanguageCode,
                ocrLanguageOption,
                previewSessionStateProvider,
                previewAiBusyChanged,
                previewAiEraserBrushSizeChanged,
                isSourceAiEnhancementEligible,
                isSourceAiInpaintingEligible,
                isSourceAiMattingEligible,
                AppServices.CreateImagePreviewWindowServices())
        {
        }

        internal ImagePreviewWindow(
            string filePath,
            bool useBackground,
            string backgroundColor,
            GifFrameRangeSelection? gifFrameRange,
            int initialPdfPageIndex,
            int pdfPageCount,
            string uiLanguageCode,
            PreviewOcrLanguageOption ocrLanguageOption,
            Func<PreviewSessionState>? previewSessionStateProvider,
            Action<bool>? previewAiBusyChanged,
            Action<int>? previewAiEraserBrushSizeChanged,
            bool isSourceAiEnhancementEligible,
            bool isSourceAiInpaintingEligible,
            bool isSourceAiMattingEligible,
            ImagePreviewWindowServices services)
            : this(services)
        {
            _gifFrameRange = gifFrameRange;
            _ocrLanguageOption = ocrLanguageOption;
            _localizationService.SetLanguage(string.IsNullOrWhiteSpace(uiLanguageCode) ? "en-US" : uiLanguageCode);
            InitializeAiPreview(
                filePath,
                previewSessionStateProvider,
                previewAiBusyChanged,
                previewAiEraserBrushSizeChanged,
                isSourceAiEnhancementEligible,
                isSourceAiInpaintingEligible,
                isSourceAiMattingEligible);

            _isPdfDocument = Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || pdfPageCount > 0;
            _pdfFilePath = _isPdfDocument ? filePath : null;
            _pdfPageCount = Math.Max(0, pdfPageCount);
            _pdfPageIndex = Math.Max(0, initialPdfPageIndex);

            if (_isPdfDocument &&
                _pdfPageCount <= 0 &&
                _pdfRenderService.TryReadDocumentInfo(filePath, out var pdfInfo, out _))
            {
                _pdfPageCount = pdfInfo.PageCount;
            }

            var fileName = Path.GetFileName(filePath);
            Title = fileName;
            FileNameText.Text = fileName;

            RefreshLocalizedText();

            if (_isPdfDocument)
            {
                RefreshPdfUiState();
                RefreshPdfPreview(preferImmediatePreview: true);
            }
            else if (PsdImportService.IsPsdFile(filePath))
            {
                _staticPreviewFilePath = filePath;
                RefreshStaticPreview(filePath, useBackground, backgroundColor, preferImmediatePreview: true);
            }
            else
            {
                _previewBitmap = ImageConversionService.TryCreatePreview(filePath, 1400, useBackground, backgroundColor);
                PreviewImage.Source = _previewBitmap;
                _ = LoadGifPreviewAsync(filePath);
            }

            RefreshGifControlsState();
            RefreshOcrControls();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _isClosed = true;
            Opened -= OnPreviewWindowOpened;
            PositionChanged -= OnPreviewWindowPositionChanged;
            if (Screens is not null)
            {
                Screens.Changed -= OnPreviewWindowScreensChanged;
            }

            CancelPendingPdfPreview();
            CancelPendingStaticPreview();
            CancelPendingOcr();
            CancelOcrAnimation();
            CleanupAiPreview();
            CleanupAiEraser();
            CleanupAiMattingPreview();
            ClearPendingPreviewCompareUpdates();
            _toastTimer.Stop();

            _previewBitmap?.Dispose();
            _previewBitmap = null;
            StopGifPreview(resetPlaybackState: true);
        }

        private void InitializePreviewCompareInteractionPipeline()
        {
            AiCompareSplitterLine.RenderTransform = _aiCompareSplitterTransform;
            AiCompareThumb.RenderTransform = _aiCompareThumbTransform;
            Canvas.SetLeft(AiCompareSplitterLine, 0d);
            Canvas.SetTop(AiCompareSplitterLine, 0d);
            Canvas.SetLeft(AiCompareThumb, 0d);
            Canvas.SetTop(AiCompareThumb, 0d);
            ResetAiCompareOverlayVisuals();

            AiMattingCompareSplitterLine.RenderTransform = _aiMattingCompareSplitterTransform;
            AiMattingCompareThumb.RenderTransform = _aiMattingCompareThumbTransform;
            Canvas.SetLeft(AiMattingCompareSplitterLine, 0d);
            Canvas.SetTop(AiMattingCompareSplitterLine, 0d);
            Canvas.SetLeft(AiMattingCompareThumb, 0d);
            Canvas.SetTop(AiMattingCompareThumb, 0d);
            ResetAiMattingOverlayVisuals();

            AiEraserCompareSplitterLine.RenderTransform = _aiEraserCompareSplitterTransform;
            AiEraserCompareThumb.RenderTransform = _aiEraserCompareThumbTransform;
            Canvas.SetLeft(AiEraserCompareSplitterLine, 0d);
            Canvas.SetTop(AiEraserCompareSplitterLine, 0d);
            Canvas.SetLeft(AiEraserCompareThumb, 0d);
            Canvas.SetTop(AiEraserCompareThumb, 0d);
            ResetAiEraserCompareOverlayVisuals();

            Opened += OnPreviewWindowOpened;
            PositionChanged += OnPreviewWindowPositionChanged;
        }

        private void OnPreviewWindowOpened(object? sender, EventArgs e)
        {
            if (Screens is not null)
            {
                Screens.Changed -= OnPreviewWindowScreensChanged;
                Screens.Changed += OnPreviewWindowScreensChanged;
            }

            RefreshPreviewCompareRefreshRate();
        }

        private void OnPreviewWindowPositionChanged(object? sender, PixelPointEventArgs e)
        {
            RefreshPreviewCompareRefreshRate();
        }

        private void OnPreviewWindowScreensChanged(object? sender, EventArgs e)
        {
            RefreshPreviewCompareRefreshRate();
        }

        private void RefreshPreviewCompareRefreshRate()
        {
            var refreshRate = _displayRefreshRateService.GetRefreshRate(ResolveCurrentPreviewScreen());
            _previewCompareRefreshRate = refreshRate;
            _previewCompareFrameInterval = TimeSpan.FromSeconds(1d / refreshRate);
        }

        private Screen? ResolveCurrentPreviewScreen()
        {
            var screens = Screens;
            if (screens is null)
            {
                return null;
            }

            try
            {
                return screens.ScreenFromWindow(this)
                    ?? screens.ScreenFromVisual(this)
                    ?? screens.Primary;
            }
            catch (ObjectDisposedException)
            {
                return screens.Primary;
            }
        }

        private void QueueAiCompareFrameUpdate(Point point)
        {
            _pendingAiComparePoint = point;
            _isAiComparePointPending = true;
            RequestPreviewCompareAnimationFrame();
        }

        private void QueueAiMattingCompareFrameUpdate(Point point)
        {
            _pendingAiMattingComparePoint = point;
            _isAiMattingComparePointPending = true;
            RequestPreviewCompareAnimationFrame();
        }

        private void QueueAiEraserCompareFrameUpdate(Point point)
        {
            _pendingAiEraserComparePoint = point;
            _isAiEraserComparePointPending = true;
            RequestPreviewCompareAnimationFrame();
        }

        private void RequestPreviewCompareAnimationFrame()
        {
            if (_isClosed || _isPreviewCompareFrameRequested)
            {
                return;
            }

            _isPreviewCompareFrameRequested = true;
            RequestAnimationFrame(OnPreviewCompareAnimationFrame);
        }

        private void OnPreviewCompareAnimationFrame(TimeSpan _)
        {
            _isPreviewCompareFrameRequested = false;

            if (_isClosed || !HasPendingPreviewCompareUpdates())
            {
                return;
            }

            if (!ShouldCommitPreviewCompareFrame())
            {
                RequestPreviewCompareAnimationFrame();
                return;
            }

            ApplyPendingPreviewCompareUpdates();
        }

        private bool HasPendingPreviewCompareUpdates()
        {
            return _isAiComparePointPending || _isAiEraserComparePointPending || _isAiMattingComparePointPending;
        }

        private bool ShouldCommitPreviewCompareFrame()
        {
            if (_lastPreviewCompareCommitTime == TimeSpan.MinValue)
            {
                return true;
            }

            var elapsed = _previewCompareFrameStopwatch.Elapsed - _lastPreviewCompareCommitTime;
            return elapsed + TimeSpan.FromMilliseconds(PreviewCompareFrameToleranceMilliseconds) >= _previewCompareFrameInterval;
        }

        private void ApplyPendingPreviewCompareUpdates()
        {
            var applied = false;

            if (_isAiComparePointPending)
            {
                _isAiComparePointPending = false;
                applied = ApplyAiCompareSplitFromPoint(_pendingAiComparePoint) || applied;
            }

            if (_isAiMattingComparePointPending)
            {
                _isAiMattingComparePointPending = false;
                applied = ApplyAiMattingCompareSplitFromPoint(_pendingAiMattingComparePoint) || applied;
            }

            if (_isAiEraserComparePointPending)
            {
                _isAiEraserComparePointPending = false;
                applied = ApplyAiEraserCompareSplitFromPoint(_pendingAiEraserComparePoint) || applied;
            }

            if (applied)
            {
                _lastPreviewCompareCommitTime = _previewCompareFrameStopwatch.Elapsed;
            }
        }

        private void ClearPendingPreviewCompareUpdates()
        {
            _isPreviewCompareFrameRequested = false;
            _isAiComparePointPending = false;
            _isAiEraserComparePointPending = false;
            _isAiMattingComparePointPending = false;
        }

        private void ResetAiCompareOverlayVisuals()
        {
            AiCompareAfterClipHost.Clip = null;
            AiCompareSplitterLine.Height = 0d;
            _aiCompareSplitterTransform.X = 0d;
            _aiCompareSplitterTransform.Y = 0d;
            _aiCompareThumbTransform.X = 0d;
            _aiCompareThumbTransform.Y = 0d;
        }

        private void ResetAiMattingOverlayVisuals()
        {
            AiMattingCompareAfterClipHost.Clip = null;
            AiMattingCompareSplitterLine.Height = 0d;
            _aiMattingCompareSplitterTransform.X = 0d;
            _aiMattingCompareSplitterTransform.Y = 0d;
            _aiMattingCompareThumbTransform.X = 0d;
            _aiMattingCompareThumbTransform.Y = 0d;
        }

        private void ResetAiEraserCompareOverlayVisuals()
        {
            AiEraserCompareAfterClipHost.Clip = null;
            AiEraserCompareSplitterLine.Height = 0d;
            _aiEraserCompareSplitterTransform.X = 0d;
            _aiEraserCompareSplitterTransform.Y = 0d;
            _aiEraserCompareThumbTransform.X = 0d;
            _aiEraserCompareThumbTransform.Y = 0d;
        }

        private string T(string key)
        {
            return _localizationService.Translate(key);
        }

        private string FormatT(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        private void RefreshLocalizedText()
        {
            RefreshRecognitionChrome();
            CopySelectionMenuItem.Header = T("PreviewOcrCopy");
            CopyAllMenuItem.Header = T("PreviewOcrCopyAll");
            PlayGifButton.Content = T("Play");
            PauseGifButton.Content = T("Pause");
            ToastText.Text = T("PreviewOcrCopied");
            PreviewDisclaimerTitleText.Text = T("IntelligentFeatureDisclaimerTitle");
            PreviewDisclaimerBodyText.Text = T("PreviewIntelligentFeatureDisclaimer");
            RefreshAiPreviewText();
            RefreshAiEraserText();
            RefreshAiMattingText();

            if (_isPdfDocument)
            {
                RefreshPdfUiState();
            }

            if (_isOcrBusy)
            {
                SetOcrPlaceholder(GetBusyText(_panelMode));
            }
            else if (_isOcrPanelVisible && string.IsNullOrWhiteSpace(_ocrText))
            {
                SetOcrPlaceholder(GetEmptyPlaceholderText(_panelMode));
            }

            RefreshRecognitionContentVisibility();
            RefreshGifControlsState();
            RefreshOcrControls();
        }

        private void RefreshPreviewActionStates()
        {
            RefreshAiPreviewUi();
            RefreshAiEraserUi();
            RefreshAiMattingUi();
            RefreshOcrControls();
        }

        private bool HasActiveRecognitionSession()
        {
            return _isOcrBusy || _isOcrPanelVisible;
        }

        private bool IsBlockedByPreviewTools()
        {
            return _isAiPreviewBusy ||
                   _isAiCompareActive ||
                   _isAiSaveBusy ||
                   _isAiEraserEditing ||
                   _isAiEraserBusy ||
                   _isAiEraserCompareActive ||
                   _isAiEraserSaveBusy ||
                   _isAiMattingBusy ||
                   _isAiMattingCompareActive ||
                   _isAiMattingSaveBusy;
        }

        private void RefreshPdfPreview(bool preferImmediatePreview)
        {
            CancelPendingPdfPreview();

            if (!_isPdfDocument || string.IsNullOrWhiteSpace(_pdfFilePath))
            {
                return;
            }

            var filePath = _pdfFilePath;
            var pageIndex = Math.Clamp(_pdfPageIndex, 0, Math.Max(0, _pdfPageCount - 1));
            var cancellationSource = new CancellationTokenSource();
            _pdfPreviewCts = cancellationSource;
            var requestId = Interlocked.Increment(ref _pdfPreviewRequestId);

            _ = LoadPdfPreviewAsync(filePath, pageIndex, requestId, cancellationSource, preferImmediatePreview);
        }

        private void RefreshPdfUiState()
        {
            if (!_isPdfDocument)
            {
                PreviousPageButton.IsVisible = false;
                NextPageButton.IsVisible = false;
                PageIndicatorText.IsVisible = false;
                PageIndicatorText.Text = string.Empty;
                return;
            }

            _pdfPageCount = Math.Max(1, _pdfPageCount);
            _pdfPageIndex = Math.Clamp(_pdfPageIndex, 0, _pdfPageCount - 1);

            var hasMultiplePages = _pdfPageCount > 1;
            PreviousPageButton.IsVisible = hasMultiplePages;
            NextPageButton.IsVisible = hasMultiplePages;
            PreviousPageButton.IsEnabled = _pdfPageIndex > 0;
            NextPageButton.IsEnabled = _pdfPageIndex < _pdfPageCount - 1;
            PageIndicatorText.IsVisible = true;
            PageIndicatorText.Text = FormatT("PdfPageIndicatorTemplate", _pdfPageIndex + 1, _pdfPageCount);
        }

        private async Task LoadGifPreviewAsync(string filePath)
        {
            var requestId = Interlocked.Increment(ref _gifPreviewRequestId);

            if (ImageConversionService.TryGetCachedGifPreview(filePath, 1400, out var cachedFull))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_isClosed || requestId != _gifPreviewRequestId)
                    {
                        cachedFull.Dispose();
                        return;
                    }

                    if (cachedFull.Frames.Count == 0 || cachedFull.Frames.Count != cachedFull.Durations.Count)
                    {
                        cachedFull.Dispose();
                        return;
                    }

                    StartGifPreview(cachedFull);
                });

                return;
            }

            if (ImageConversionService.TryGetCachedGifPreview(filePath, 760, out var cachedFallback))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_isClosed || requestId != _gifPreviewRequestId)
                    {
                        cachedFallback.Dispose();
                        return;
                    }

                    if (cachedFallback.Frames.Count == 0 || cachedFallback.Frames.Count != cachedFallback.Durations.Count)
                    {
                        cachedFallback.Dispose();
                        return;
                    }

                    StartGifPreview(cachedFallback);
                });
            }

            var fullHandle = await ImageConversionService.GetOrLoadGifPreviewAsync(filePath, 1400);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isClosed || requestId != _gifPreviewRequestId)
                {
                    fullHandle?.Dispose();
                    return;
                }

                if (fullHandle is null || fullHandle.Frames.Count == 0 || fullHandle.Frames.Count != fullHandle.Durations.Count)
                {
                    fullHandle?.Dispose();
                    return;
                }

                StartGifPreview(fullHandle);
            });
        }

        private void StartGifPreview(ImageConversionService.GifPreviewHandle handle)
        {
            StopGifPreview(resetPlaybackState: false);

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

            var selection = GetEffectiveGifFrameRange(frames.Count);
            _gifPreviewIndex = selection.StartIndex;
            _previewBitmap?.Dispose();
            _previewBitmap = null;
            _isGifPlaying = frames.Count > 1;

            PreviewImage.Source = frames[_gifPreviewIndex];
            if (_isGifPlaying)
            {
                _gifPreviewTimer.Interval = ClampGifDuration(durations[_gifPreviewIndex]);
                _gifPreviewTimer.Start();
            }

            RefreshGifControlsState();
            RefreshOcrControls();
        }

        private void StopGifPreview(bool resetPlaybackState)
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

            if (resetPlaybackState)
            {
                _isGifPlaying = false;
            }

            RefreshGifControlsState();
            RefreshOcrControls();
        }

        private void PauseGifPreview()
        {
            if (_gifPreviewTimer.IsEnabled)
            {
                _gifPreviewTimer.Stop();
            }

            _isGifPlaying = false;
            RefreshGifControlsState();
            RefreshOcrControls();
        }

        private void ResumeGifPreview()
        {
            if (_gifPreviewFrames is null ||
                _gifPreviewDurations is null ||
                _gifPreviewFrames.Count <= 1 ||
                _gifPreviewFrames.Count != _gifPreviewDurations.Count)
            {
                return;
            }

            var selection = GetEffectiveGifFrameRange(_gifPreviewFrames.Count);
            if (_gifPreviewIndex < selection.StartIndex || _gifPreviewIndex > selection.EndIndex)
            {
                _gifPreviewIndex = selection.StartIndex;
            }

            _isGifPlaying = true;
            PreviewImage.Source = _gifPreviewFrames[_gifPreviewIndex];
            _gifPreviewTimer.Interval = ClampGifDuration(_gifPreviewDurations[_gifPreviewIndex]);
            _gifPreviewTimer.Start();
            RefreshGifControlsState();
            RefreshOcrControls();
        }

        private void RefreshGifControlsState()
        {
            var hasMultipleFrames = _gifPreviewFrames is not null && _gifPreviewFrames.Count > 1;
            GifControlsBar.IsVisible = hasMultipleFrames;

            if (!hasMultipleFrames)
            {
                GifFrameCountdownText.Text = string.Empty;
                PlayGifButton.IsEnabled = false;
                PauseGifButton.IsEnabled = false;
                GifFrameSlider.Minimum = 0;
                GifFrameSlider.Maximum = 1;
                GifFrameSlider.Value = 0;
                return;
            }

            var selection = GetEffectiveGifFrameRange(_gifPreviewFrames!.Count);
            var clampedIndex = Math.Clamp(_gifPreviewIndex, selection.StartIndex, selection.EndIndex);

            PlayGifButton.IsEnabled = !_isGifPlaying;
            PauseGifButton.IsEnabled = _isGifPlaying;

            _suppressGifSliderChange = true;
            try
            {
                GifFrameSlider.Minimum = selection.StartIndex;
                GifFrameSlider.Maximum = selection.EndIndex;
                GifFrameSlider.Value = clampedIndex;
            }
            finally
            {
                _suppressGifSliderChange = false;
            }

            var remainingFrames = selection.EndIndex - clampedIndex + 1;
            GifFrameCountdownText.Text = FormatT("PreviewGifFramesRemainingTemplate", remainingFrames);
        }

        private void SetCurrentGifFrame(int frameIndex)
        {
            if (_gifPreviewFrames is null || _gifPreviewDurations is null || _gifPreviewFrames.Count == 0)
            {
                return;
            }

            var selection = GetEffectiveGifFrameRange(_gifPreviewFrames.Count);
            _gifPreviewIndex = Math.Clamp(frameIndex, selection.StartIndex, selection.EndIndex);
            PreviewImage.Source = _gifPreviewFrames[_gifPreviewIndex];

            if (_isGifPlaying)
            {
                var durationIndex = Math.Min(_gifPreviewIndex, _gifPreviewDurations.Count - 1);
                _gifPreviewTimer.Interval = ClampGifDuration(_gifPreviewDurations[durationIndex]);
            }

            RefreshGifControlsState();

            if (_isOcrPanelVisible && !_isOcrBusy)
            {
                _ = RecognizeCurrentVisibleModeAsync(revealBusyPanelImmediately: false);
            }
        }

        private void OnGifPreviewTick(object? sender, EventArgs e)
        {
            if (_gifPreviewFrames is null || _gifPreviewDurations is null || _gifPreviewFrames.Count == 0)
            {
                StopGifPreview(resetPlaybackState: true);
                return;
            }

            var selection = GetEffectiveGifFrameRange(_gifPreviewFrames.Count);
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

            PreviewImage.Source = _gifPreviewFrames[_gifPreviewIndex];
            var durationIndex = Math.Min(_gifPreviewIndex, _gifPreviewDurations.Count - 1);
            _gifPreviewTimer.Interval = ClampGifDuration(_gifPreviewDurations[durationIndex]);

            RefreshGifControlsState();
        }

        private GifFrameRangeSelection GetEffectiveGifFrameRange(int frameCount)
        {
            if (frameCount <= 0 || _gifFrameRange is null)
            {
                return new GifFrameRangeSelection(0, Math.Max(0, frameCount - 1));
            }

            var maxIndex = frameCount - 1;
            var start = Math.Clamp(_gifFrameRange.Value.StartIndex, 0, maxIndex);
            var end = Math.Clamp(_gifFrameRange.Value.EndIndex, start, maxIndex);
            return new GifFrameRangeSelection(start, end);
        }

        private static TimeSpan ClampGifDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero || duration.TotalMilliseconds <= 20)
            {
                return TimeSpan.FromMilliseconds(100);
            }

            return duration;
        }

        private void CancelPendingPdfPreview()
        {
            var cancellationSource = Interlocked.Exchange(ref _pdfPreviewCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending PDF preview work cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private void CancelPendingStaticPreview()
        {
            var cancellationSource = Interlocked.Exchange(ref _staticPreviewCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending static preview work cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private void RefreshStaticPreview(string filePath, bool useBackground, string backgroundColor, bool preferImmediatePreview)
        {
            CancelPendingStaticPreview();

            var cancellationSource = new CancellationTokenSource();
            _staticPreviewCts = cancellationSource;
            var requestId = Interlocked.Increment(ref _staticPreviewRequestId);

            _ = LoadStaticPreviewAsync(
                filePath,
                useBackground,
                backgroundColor,
                requestId,
                cancellationSource,
                preferImmediatePreview);
        }

        private async Task LoadStaticPreviewAsync(
            string filePath,
            bool useBackground,
            string backgroundColor,
            long requestId,
            CancellationTokenSource cancellationSource,
            bool preferImmediatePreview)
        {
            Bitmap? lowPreview = null;
            Bitmap? highPreview = null;

            try
            {
                if (!preferImmediatePreview)
                {
                    await Task.Delay(32, cancellationSource.Token).ConfigureAwait(false);
                }

                cancellationSource.Token.ThrowIfCancellationRequested();
                lowPreview = await Task.Run(
                        () => ImageConversionService.TryCreatePreview(filePath, StaticPreviewLowQualityWidth, useBackground, backgroundColor),
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (lowPreview is not null)
                {
                    if (!await TryApplyStaticPreviewAsync(filePath, requestId, lowPreview).ConfigureAwait(false))
                    {
                        return;
                    }

                    lowPreview = null;
                }

                cancellationSource.Token.ThrowIfCancellationRequested();
                highPreview = await Task.Run(
                        () => ImageConversionService.TryCreatePreview(filePath, StaticPreviewHighQualityWidth, useBackground, backgroundColor),
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (highPreview is not null)
                {
                    if (!await TryApplyStaticPreviewAsync(filePath, requestId, highPreview).ConfigureAwait(false))
                    {
                        return;
                    }

                    highPreview = null;
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore stale preview requests when the window is closed or superseded.
            }
            finally
            {
                lowPreview?.Dispose();
                highPreview?.Dispose();

                if (ReferenceEquals(Interlocked.CompareExchange(ref _staticPreviewCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }
            }
        }

        private async Task<bool> TryApplyStaticPreviewAsync(string filePath, long requestId, Bitmap preview)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isClosed ||
                    requestId != _staticPreviewRequestId ||
                    _isPdfDocument ||
                    !string.Equals(_staticPreviewFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                _previewBitmap?.Dispose();
                _previewBitmap = preview;
                PreviewImage.Source = preview;
                RefreshOcrControls();
                return true;
            });
        }

        private async Task LoadPdfPreviewAsync(
            string filePath,
            int pageIndex,
            long requestId,
            CancellationTokenSource cancellationSource,
            bool preferImmediatePreview)
        {
            Bitmap? lowPreview = null;
            Bitmap? highPreview = null;

            try
            {
                if (!preferImmediatePreview)
                {
                    await Task.Delay(32, cancellationSource.Token).ConfigureAwait(false);
                }

                cancellationSource.Token.ThrowIfCancellationRequested();
                lowPreview = await Task.Run(
                        () => _pdfRenderService.TryCreatePreview(filePath, pageIndex, PdfPreviewLowQualityWidth),
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (lowPreview is not null)
                {
                    if (!await TryApplyPdfPreviewAsync(filePath, pageIndex, requestId, lowPreview).ConfigureAwait(false))
                    {
                        return;
                    }

                    lowPreview = null;
                }

                cancellationSource.Token.ThrowIfCancellationRequested();
                highPreview = await Task.Run(
                        () => _pdfRenderService.TryCreatePreview(filePath, pageIndex, PdfPreviewHighQualityWidth),
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (highPreview is not null)
                {
                    if (!await TryApplyPdfPreviewAsync(filePath, pageIndex, requestId, highPreview).ConfigureAwait(false))
                    {
                        return;
                    }

                    highPreview = null;
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore canceled preview requests so only the newest render is shown.
            }
            finally
            {
                lowPreview?.Dispose();
                highPreview?.Dispose();

                if (ReferenceEquals(Interlocked.CompareExchange(ref _pdfPreviewCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }
            }
        }

        private async Task<bool> TryApplyPdfPreviewAsync(string filePath, int pageIndex, long requestId, Bitmap preview)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isClosed ||
                    requestId != _pdfPreviewRequestId ||
                    !_isPdfDocument ||
                    !string.Equals(_pdfFilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    _pdfPageIndex != pageIndex)
                {
                    return false;
                }

                _previewBitmap?.Dispose();
                _previewBitmap = preview;
                PreviewImage.Source = preview;
                RefreshOcrControls();
                return true;
            });
        }

        private void BeginRecognitionSession(PreviewRecognitionMode mode)
        {
            _panelMode = mode;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();
        }

        private bool ShouldIgnoreRecognitionResult(CancellationTokenSource cancellationSource)
        {
            return _isClosed || !ReferenceEquals(_ocrCts, cancellationSource);
        }

        private async Task RunRecognitionSessionAsync(
            PreviewRecognitionMode mode,
            bool revealBusyPanelImmediately,
            Func<CancellationTokenSource, Task> executeAsync)
        {
            ArgumentNullException.ThrowIfNull(executeAsync);

            if (_isClosed)
            {
                return;
            }

            CancelPendingOcr();
            var cancellationSource = new CancellationTokenSource();
            _ocrCts = cancellationSource;
            _isOcrBusy = true;
            BeginRecognitionSession(mode);

            try
            {
                UpdateOcrBusyUi();

                if (revealBusyPanelImmediately || _isOcrPanelVisible)
                {
                    await SetOcrPanelVisibleAsync(true);
                    await WaitForUiRenderAsync();
                }

                await executeAsync(cancellationSource);
            }
            catch (OperationCanceledException)
            {
                // Ignore stale recognition requests when the user switches pages or frames.
            }
            finally
            {
                if (ReferenceEquals(_ocrCts, cancellationSource))
                {
                    _ocrCts = null;
                    _isOcrBusy = false;
                    UpdateOcrBusyUi();
                }

                cancellationSource.Dispose();
            }
        }

        private async Task RecognizeCurrentPreviewAsync(bool revealBusyPanelImmediately)
        {
            await RunRecognitionSessionAsync(
                PreviewRecognitionMode.Ocr,
                revealBusyPanelImmediately,
                async cancellationSource =>
                {
                    var result = await _previewOcrToolController
                        .RecognizeAsync(CreateCurrentOcrImageBytesAsync, _ocrLanguageOption, cancellationSource.Token);
                    if (ShouldIgnoreRecognitionResult(cancellationSource))
                    {
                        return;
                    }

                    ApplyOcrResult(result);
                });
        }

        private void CancelPendingOcr()
        {
            var cancellationSource = Interlocked.Exchange(ref _ocrCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending OCR work cleanly.", ex);
            }
        }

        private async Task<byte[]?> CreateCurrentOcrImageBytesAsync(CancellationToken cancellationToken)
        {
            if (_isPdfDocument && !string.IsNullOrWhiteSpace(_pdfFilePath))
            {
                var pdfBitmap = await Task.Run(
                    () => _pdfRenderService.TryCreatePreview(_pdfFilePath, _pdfPageIndex, PdfOcrRenderWidth),
                    cancellationToken);

                if (pdfBitmap is null)
                {
                    return null;
                }

                return await Task.Run(() =>
                {
                    using (pdfBitmap)
                    {
                        return SaveBitmapToBytes(pdfBitmap);
                    }
                }, cancellationToken);
            }

            if (PreviewImage.Source is not Bitmap bitmap)
            {
                return null;
            }

            return await Task.Run(() => SaveBitmapToBytes(bitmap), cancellationToken);
        }

        private void ApplyOcrResult(PreviewOcrRecognition result)
        {
            _panelMode = PreviewRecognitionMode.Ocr;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();
            _ocrText = result.HasText ? result.Text : string.Empty;
            _copyAllText = _ocrText;
            OcrTextBox.Text = result.HasText ? result.Text : string.Empty;

            if (result.HasText)
            {
                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(string.Empty);
            }
            else
            {
                var placeholder = result.ErrorMessage switch
                {
                    PreviewOcrToolController.UnavailableInputErrorCode => T("PreviewOcrUnavailable"),
                    PreviewOcrService.UnsupportedPlatformErrorCode => T("PreviewOcrUnsupportedPlatform"),
                    PreviewOcrService.PathErrorCode => T("PreviewOcrPathError"),
                    PreviewOcrService.EngineFilesMissingErrorCode => T("PreviewOcrEngineFilesMissing"),
                    PreviewOcrService.InitializationFailedErrorCode => T("PreviewOcrInitializationFailed"),
                    null or "" => GetEmptyPlaceholderText(PreviewRecognitionMode.Ocr),
                    _ => result.ErrorMessage!
                };

                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(placeholder);
            }

            RefreshRecognitionContentVisibility();
            RefreshRecognitionChrome();
            RefreshOcrControls();
        }

        private void UpdateOcrBusyUi()
        {
            OcrBusyProgressBar.IsVisible = _isOcrBusy;
            RecognitionProcessingNoticeCard.IsVisible = _isOcrBusy;
            RefreshRecognitionChrome();

            if (_isOcrBusy)
            {
                OcrStatusText.Text = GetBusyText(_panelMode);
                SetOcrPlaceholder(GetBusyText(_panelMode));
            }
            else
            {
                OcrStatusText.Text = string.Empty;
            }

            RefreshRecognitionContentVisibility();
            RefreshPreviewActionStates();
        }

        private void SetOcrPlaceholder(string text)
        {
            OcrPlaceholderText.Text = text;
            OcrPlaceholderText.IsVisible = !string.IsNullOrWhiteSpace(text);
        }

        private void RefreshOcrControls()
        {
            var hasPreviewBitmap = PreviewImage.Source is Bitmap;
            var isRecognitionSessionActive = HasActiveRecognitionSession();
            var isBlockedByPreviewTools = IsBlockedByPreviewTools();
            var canRunTopRecognitionActions = hasPreviewBitmap &&
                                             !isRecognitionSessionActive &&
                                             !isBlockedByPreviewTools &&
                                             (!_isGifPlaying || !_isGifInteractive());
            var canUseRecognitionPanelActions = !isBlockedByPreviewTools && !_isOcrBusy;
            var hasCopyAllText = !string.IsNullOrWhiteSpace(_copyAllText);

            OcrButton.IsEnabled = canRunTopRecognitionActions;
            QrButton.IsEnabled = canRunTopRecognitionActions;
            BarcodeButton.IsEnabled = canRunTopRecognitionActions;
            OcrRetryButton.IsEnabled = canUseRecognitionPanelActions && hasPreviewBitmap;
            CloseOcrButton.IsEnabled = canUseRecognitionPanelActions;
            OcrCopyAllButton.IsEnabled = canUseRecognitionPanelActions && hasCopyAllText;
            CopySelectionMenuItem.IsEnabled = canUseRecognitionPanelActions &&
                                              OcrTextBox.IsVisible &&
                                              !string.IsNullOrWhiteSpace(_ocrText);
            CopyAllMenuItem.IsEnabled = canUseRecognitionPanelActions && hasCopyAllText;
            OpenLinkButton.IsVisible = false;
            OpenLinkButton.IsEnabled = false;
        }

        private bool _isGifInteractive()
        {
            return _gifPreviewFrames is not null && _gifPreviewFrames.Count > 1;
        }

        private async Task SetOcrPanelVisibleAsync(bool visible)
        {
            if (_isOcrPanelVisible == visible)
            {
                if (visible)
                {
                    OcrPanel.Width = GetTargetOcrPanelWidth();
                }

                RefreshPreviewActionStates();
                return;
            }

            CancelOcrAnimation();
            var animationCts = new CancellationTokenSource();
            _ocrAnimationCts = animationCts;

            var startWidth = OcrPanel.Width;
            var targetWidth = visible ? GetTargetOcrPanelWidth() : 0d;
            var startOpacity = OcrPanel.Opacity;
            var targetOpacity = visible ? 1d : 0d;

            if (visible)
            {
                OcrPanel.IsVisible = true;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                while (stopwatch.ElapsedMilliseconds < OcrAnimationDurationMilliseconds)
                {
                    animationCts.Token.ThrowIfCancellationRequested();

                    var progress = stopwatch.Elapsed.TotalMilliseconds / OcrAnimationDurationMilliseconds;
                    var eased = EaseInOut(Math.Clamp(progress, 0d, 1d));
                    OcrPanel.Width = Lerp(startWidth, targetWidth, eased);
                    OcrPanel.Opacity = Lerp(startOpacity, targetOpacity, eased);

                    await Task.Delay(15, animationCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (ReferenceEquals(_ocrAnimationCts, animationCts))
                {
                    _ocrAnimationCts = null;
                }

                animationCts.Dispose();
            }

            OcrPanel.Width = targetWidth;
            OcrPanel.Opacity = targetOpacity;
            OcrPanel.IsVisible = visible;
            _isOcrPanelVisible = visible;
            RefreshPreviewActionStates();
        }

        private void CancelOcrAnimation()
        {
            var cancellationSource = Interlocked.Exchange(ref _ocrAnimationCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel OCR panel animation cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private double GetTargetOcrPanelWidth()
        {
            var windowWidth = Bounds.Width > 0
                ? Bounds.Width
                : (double.IsNaN(Width) || Width <= 0 ? 1220d : Width);

            var candidate = Math.Min(OcrPanelMaxWidth, Math.Max(OcrPanelMinWidth, windowWidth * 0.34d));
            return Math.Max(OcrPanelMinWidth, Math.Min(candidate, Math.Max(OcrPanelMinWidth, windowWidth - 420d)));
        }

        private static double EaseInOut(double progress)
        {
            return progress * progress * (3d - (2d * progress));
        }

        private static double Lerp(double start, double end, double progress)
        {
            return start + ((end - start) * progress);
        }

        private void ShowToast(string? text = null)
        {
            ToastText.Text = string.IsNullOrWhiteSpace(text) ? T("PreviewOcrCopied") : text;
            ToastBorder.IsVisible = true;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void OnToastTimerTick(object? sender, EventArgs e)
        {
            _toastTimer.Stop();
            ToastBorder.IsVisible = false;
        }

        private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isOcrPanelVisible)
            {
                OcrPanel.Width = GetTargetOcrPanelWidth();
                RefreshRecognitionLayoutBounds();
            }

            UpdateAiCompareLayout();
            UpdateAiEraserCompareLayout();
            UpdateAiEraserCursorVisual();
            UpdateAiMattingCompareLayout();
        }

        private async void OnOcrClick(object? sender, RoutedEventArgs e)
        {
            await RecognizeCurrentPreviewAsync(revealBusyPanelImmediately: true);
        }

        private async void OnRetryOcrClick(object? sender, RoutedEventArgs e)
        {
            await RecognizeCurrentVisibleModeAsync(revealBusyPanelImmediately: true);
        }

        private async void OnCloseOcrClick(object? sender, RoutedEventArgs e)
        {
            CancelPendingOcr();
            _isOcrBusy = false;
            ClearRecognitionResultState();
            await SetOcrPanelVisibleAsync(false);
            RefreshOcrControls();
        }

        private async void OnCopyAllOcrClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_copyAllText))
            {
                return;
            }

            await CopyTextToClipboardAsync(_copyAllText);
        }

        private void OnCopySelectionOcrClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OcrTextBox.SelectedText))
            {
                return;
            }

            OcrTextBox.Copy();
            ShowToast();
        }

        private void OnPreviousPageClick(object? sender, RoutedEventArgs e)
        {
            if (!_isPdfDocument || _pdfPageIndex <= 0)
            {
                return;
            }

            _pdfPageIndex--;
            RefreshPdfUiState();
            RefreshPdfPreview(preferImmediatePreview: true);

            if (_isOcrPanelVisible)
            {
                _ = RecognizeCurrentVisibleModeAsync(revealBusyPanelImmediately: false);
            }
        }

        private void OnNextPageClick(object? sender, RoutedEventArgs e)
        {
            if (!_isPdfDocument || _pdfPageIndex >= _pdfPageCount - 1)
            {
                return;
            }

            _pdfPageIndex++;
            RefreshPdfUiState();
            RefreshPdfPreview(preferImmediatePreview: true);

            if (_isOcrPanelVisible)
            {
                _ = RecognizeCurrentVisibleModeAsync(revealBusyPanelImmediately: false);
            }
        }

        private async void OnPlayGifClick(object? sender, RoutedEventArgs e)
        {
            if (_isGifPlaying)
            {
                return;
            }

            if (_isOcrPanelVisible)
            {
                CancelPendingOcr();
                _isOcrBusy = false;
                UpdateOcrBusyUi();
                await SetOcrPanelVisibleAsync(false);
                ClearRecognitionResultState();
            }

            ResumeGifPreview();
        }

        private static byte[] SaveBitmapToBytes(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream);
            return stream.ToArray();
        }

        private static async Task WaitForUiRenderAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        }

        private void OnPauseGifClick(object? sender, RoutedEventArgs e)
        {
            PauseGifPreview();
        }

        private void OnGifFrameSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressGifSliderChange || _gifPreviewFrames is null || _gifPreviewFrames.Count == 0)
            {
                return;
            }

            SetCurrentGifFrame((int)Math.Round(e.NewValue, MidpointRounding.AwayFromZero));
        }
    }
}






