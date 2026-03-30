using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ImvixPro.AI.Inpainting.Inference;
using ImvixPro.Services;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow
    {
        private const double DefaultAiEraserBrushSize = 36d;
        private static readonly SKColor AiEraserOverlayColor = new(239, 68, 68, 148);

        private enum AiEraserToolMode
        {
            Brush,
            Eraser
        }

        private CancellationTokenSource? _aiEraserCts;
        private AiInpaintingResult? _aiEraserActiveResult;
        private WriteableBitmap? _aiEraserOverlayBitmap;
        private SKBitmap? _aiEraserPreviewMaskBitmap;
        private SKBitmap? _aiEraserSourceMaskBitmap;
        private byte[]? _aiEraserOverlayCopyBuffer;
        private Point _aiEraserCursorPoint;
        private PixelSize _aiEraserSourcePixelSize;
        private bool _isAiEraserEditing;
        private bool _isAiEraserBusy;
        private bool _isAiEraserDrawing;
        private bool _isAiEraserCursorVisible;
        private AiEraserToolMode _aiEraserToolMode = AiEraserToolMode.Brush;
        private double _aiEraserBrushSize = DefaultAiEraserBrushSize;

        private void CleanupAiEraser()
        {
            CancelPendingAiEraser();
            ReleaseAiEraserEditorSurface();

            if (_aiEraserActiveResult is { } result)
            {
                _aiInpaintingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                _aiEraserActiveResult = null;
            }
        }

        private void RefreshAiEraserText()
        {
            SetWrappedButtonContent(AiEraserButton, T("AI_ERASER"));
            SetWrappedButtonContent(AiEraserBrushButton, T("ERASER_BRUSH"));
            SetWrappedButtonContent(AiEraserEraseButton, T("ERASER_ERASE"));
            SetWrappedButtonContent(AiEraserConfirmButton, T("ERASER_CONFIRM"));
            AiEraserBusyText.Text = T("AI_ERASER");
            AiEraserSizeLabel.Text = string.Create(
                CultureInfo.CurrentCulture,
                $"{T("ERASER_SIZE")} ({Math.Round(_aiEraserBrushSize, MidpointRounding.AwayFromZero)})");

            var sliderValue = Math.Clamp(_aiEraserBrushSize, AiEraserSizeSlider.Minimum, AiEraserSizeSlider.Maximum);
            if (Math.Abs(AiEraserSizeSlider.Value - sliderValue) > 0.1d)
            {
                AiEraserSizeSlider.Value = sliderValue;
            }

            RefreshAiEraserUi();
        }

        private void RefreshAiEraserUi()
        {
            var shouldShowButton = _sourceAiInpaintingEligible || _isAiEraserEditing || _isAiEraserBusy;
            var isBlockedByOtherPreview = _isAiPreviewBusy ||
                                          _isAiSaveBusy ||
                                          _isAiCompareActive ||
                                          _isAiMattingBusy ||
                                          _isAiMattingSaveBusy ||
                                          _isAiMattingCompareActive;
            var isBlockedByRecognitionSession = HasActiveRecognitionSession();

            AiEraserButton.IsVisible = shouldShowButton;
            AiEraserButton.IsEnabled = shouldShowButton &&
                                       !_isAiEraserEditing &&
                                       !_isAiEraserBusy &&
                                       !isBlockedByOtherPreview &&
                                       !isBlockedByRecognitionSession;

            AiEraserToolbar.IsVisible = _isAiEraserEditing;
            AiEraserToolbar.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy;
            AiEraserEditHost.IsVisible = _isAiEraserEditing;
            AiEraserInputLayer.IsVisible = _isAiEraserEditing;
            AiEraserBusyOverlay.IsVisible = _isAiEraserBusy;
            AiEraserMaskOverlayImage.Source = _isAiEraserEditing ? _aiEraserOverlayBitmap : null;
            AiEraserInputLayer.Cursor = _isAiEraserEditing ? new Cursor(StandardCursorType.Cross) : null;

            ApplyAiPreviewModeButtonState(
                AiEraserBrushButton,
                _aiEraserToolMode == AiEraserToolMode.Brush,
                _isAiEraserEditing && !_isAiEraserBusy);
            ApplyAiPreviewModeButtonState(
                AiEraserEraseButton,
                _aiEraserToolMode == AiEraserToolMode.Eraser,
                _isAiEraserEditing && !_isAiEraserBusy);

            AiEraserConfirmButton.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy;
            AiEraserSizeSlider.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy;
            AiEraserCursorCanvas.IsVisible = _isAiEraserEditing;
            UpdateAiEraserCursorVisual();
        }

        private async void OnAiEraserClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiEraserEditing ||
                _isAiEraserBusy ||
                !_sourceAiInpaintingEligible ||
                string.IsNullOrWhiteSpace(_sourceFilePath))
            {
                return;
            }

            await CloseRecognitionPanelForAiPreviewAsync();
            HideAiPreviewComparison();
            HideAiMattingComparison();

            if (!TryBeginAiEraserEditing())
            {
                ShowToast("Unable to initialize AI eraser for this preview.");
                return;
            }

            RefreshPreviewActionStates();
        }

        private bool TryBeginAiEraserEditing()
        {
            var inputPath = ResolveAiEraserInputPath();
            if (string.IsNullOrWhiteSpace(inputPath) ||
                !File.Exists(inputPath) ||
                !TryReadImagePixelSize(inputPath, out _aiEraserSourcePixelSize) ||
                !TryGetCurrentPreviewPixelSize(out var previewPixelSize))
            {
                return false;
            }

            ReleaseAiEraserEditorSurface();

            _aiEraserSourceMaskBitmap = CreateTransparentBitmap(_aiEraserSourcePixelSize.Width, _aiEraserSourcePixelSize.Height);
            _aiEraserPreviewMaskBitmap = CreateTransparentBitmap(previewPixelSize.Width, previewPixelSize.Height);
            _aiEraserOverlayBitmap = new WriteableBitmap(
                previewPixelSize,
                new Vector(96d, 96d),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            _aiEraserOverlayCopyBuffer = new byte[_aiEraserPreviewMaskBitmap.ByteCount];
            _isAiEraserEditing = true;
            _isAiEraserDrawing = false;
            _isAiEraserCursorVisible = false;
            _aiEraserBrushSize = Math.Clamp(_aiEraserBrushSize, AiEraserSizeSlider.Minimum, AiEraserSizeSlider.Maximum);
            SyncAiEraserOverlayToUi();
            UpdateAiEraserCursorVisual();
            return true;
        }

        private void ReleaseAiEraserEditorSurface()
        {
            _isAiEraserEditing = false;
            _isAiEraserDrawing = false;
            _isAiEraserCursorVisible = false;

            AiEraserMaskOverlayImage.Source = null;
            AiEraserCursorRing.IsVisible = false;

            _aiEraserPreviewMaskBitmap?.Dispose();
            _aiEraserPreviewMaskBitmap = null;

            _aiEraserSourceMaskBitmap?.Dispose();
            _aiEraserSourceMaskBitmap = null;

            _aiEraserOverlayBitmap?.Dispose();
            _aiEraserOverlayBitmap = null;
            _aiEraserOverlayCopyBuffer = null;
            _aiEraserSourcePixelSize = default;
        }

        private void CancelPendingAiEraser()
        {
            var cancellationSource = Interlocked.Exchange(ref _aiEraserCts, null);
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
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending AI eraser work cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private string? ResolveAiEraserInputPath()
        {
            if (_aiEraserActiveResult is { ResultPath: { } resultPath } &&
                File.Exists(resultPath))
            {
                return resultPath;
            }

            return _sourceFilePath;
        }

        private static bool TryReadImagePixelSize(string filePath, out PixelSize size)
        {
            size = default;

            try
            {
                using var codec = SKCodec.Create(filePath);
                if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
                {
                    return false;
                }

                size = new PixelSize(codec.Info.Width, codec.Info.Height);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetCurrentPreviewPixelSize(out PixelSize pixelSize)
        {
            if (PreviewImage.Source is Bitmap bitmap &&
                bitmap.PixelSize.Width > 0 &&
                bitmap.PixelSize.Height > 0)
            {
                pixelSize = bitmap.PixelSize;
                return true;
            }

            if (_previewBitmap is not null &&
                _previewBitmap.PixelSize.Width > 0 &&
                _previewBitmap.PixelSize.Height > 0)
            {
                pixelSize = _previewBitmap.PixelSize;
                return true;
            }

            pixelSize = default;
            return false;
        }

        private static SKBitmap CreateTransparentBitmap(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            bitmap.Erase(SKColors.Transparent);
            return bitmap;
        }

        private void SyncAiEraserOverlayToUi()
        {
            if (_aiEraserPreviewMaskBitmap is null ||
                _aiEraserOverlayBitmap is null ||
                _aiEraserOverlayCopyBuffer is null)
            {
                return;
            }

            Marshal.Copy(_aiEraserPreviewMaskBitmap.GetPixels(), _aiEraserOverlayCopyBuffer, 0, _aiEraserPreviewMaskBitmap.ByteCount);

            using var framebuffer = _aiEraserOverlayBitmap.Lock();
            for (var row = 0; row < framebuffer.Size.Height; row++)
            {
                Marshal.Copy(
                    _aiEraserOverlayCopyBuffer,
                    row * _aiEraserPreviewMaskBitmap.RowBytes,
                    framebuffer.Address + (row * framebuffer.RowBytes),
                    _aiEraserPreviewMaskBitmap.RowBytes);
            }
        }

        private Rect GetAiEraserContentRect()
        {
            var bounds = AiEraserInputLayer.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = AiEraserEditHost.Bounds;
            }

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = PreviewSurface.Bounds;
            }

            if (bounds.Width <= 0 || bounds.Height <= 0 || !TryGetCurrentPreviewPixelSize(out var pixelSize))
            {
                return default;
            }

            var scale = Math.Min(bounds.Width / pixelSize.Width, bounds.Height / pixelSize.Height);
            var width = pixelSize.Width * scale;
            var height = pixelSize.Height * scale;
            var offsetX = (bounds.Width - width) / 2d;
            var offsetY = (bounds.Height - height) / 2d;
            return new Rect(offsetX, offsetY, width, height);
        }

        private bool TryMapAiEraserPoint(Point point, bool clampToContent, out AiEraserMappedPoint mappedPoint)
        {
            mappedPoint = default;

            if (_aiEraserPreviewMaskBitmap is null || _aiEraserSourceMaskBitmap is null)
            {
                return false;
            }

            var contentRect = GetAiEraserContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return false;
            }

            if (!clampToContent && !contentRect.Contains(point))
            {
                return false;
            }

            var clampedPoint = new Point(
                Math.Clamp(point.X, contentRect.X, contentRect.Right),
                Math.Clamp(point.Y, contentRect.Y, contentRect.Bottom));
            var relativeX = contentRect.Width <= 0
                ? 0d
                : (clampedPoint.X - contentRect.X) / contentRect.Width;
            var relativeY = contentRect.Height <= 0
                ? 0d
                : (clampedPoint.Y - contentRect.Y) / contentRect.Height;

            var previewPoint = new SKPoint(
                (float)(relativeX * Math.Max(0, _aiEraserPreviewMaskBitmap.Width - 1)),
                (float)(relativeY * Math.Max(0, _aiEraserPreviewMaskBitmap.Height - 1)));
            var sourcePoint = new SKPoint(
                (float)(relativeX * Math.Max(0, _aiEraserSourceMaskBitmap.Width - 1)),
                (float)(relativeY * Math.Max(0, _aiEraserSourceMaskBitmap.Height - 1)));

            var previewDiameter = Math.Max(1f, (float)(_aiEraserBrushSize * (_aiEraserPreviewMaskBitmap.Width / contentRect.Width)));
            var sourceDiameter = Math.Max(1f, (float)(_aiEraserBrushSize * (_aiEraserSourceMaskBitmap.Width / contentRect.Width)));

            mappedPoint = new AiEraserMappedPoint(clampedPoint, previewPoint, sourcePoint, previewDiameter, sourceDiameter);
            return true;
        }

        private void DrawAiEraserSegment(AiEraserMappedPoint fromPoint, AiEraserMappedPoint toPoint)
        {
            if (_aiEraserPreviewMaskBitmap is null || _aiEraserSourceMaskBitmap is null)
            {
                return;
            }

            DrawAiEraserStroke(
                _aiEraserPreviewMaskBitmap,
                fromPoint.PreviewPoint,
                toPoint.PreviewPoint,
                fromPoint.PreviewDiameter,
                _aiEraserToolMode == AiEraserToolMode.Eraser,
                AiEraserOverlayColor);
            DrawAiEraserStroke(
                _aiEraserSourceMaskBitmap,
                fromPoint.SourcePoint,
                toPoint.SourcePoint,
                fromPoint.SourceDiameter,
                _aiEraserToolMode == AiEraserToolMode.Eraser,
                SKColors.White);
            SyncAiEraserOverlayToUi();
        }

        private static void DrawAiEraserStroke(
            SKBitmap bitmap,
            SKPoint fromPoint,
            SKPoint toPoint,
            float diameter,
            bool erase,
            SKColor color)
        {
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeWidth = Math.Max(1f, diameter),
                Color = color,
                BlendMode = erase ? SKBlendMode.Clear : SKBlendMode.SrcOver
            };

            var deltaX = toPoint.X - fromPoint.X;
            var deltaY = toPoint.Y - fromPoint.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) < 0.25f)
            {
                canvas.DrawCircle(toPoint, Math.Max(0.5f, diameter / 2f), paint);
            }
            else
            {
                canvas.DrawLine(fromPoint, toPoint, paint);
            }

            canvas.Flush();
        }

        private bool HasAiEraserMaskContent()
        {
            return HasAiEraserMaskContent(_aiEraserPreviewMaskBitmap) ||
                   HasAiEraserMaskContent(_aiEraserSourceMaskBitmap);
        }

        private static bool HasAiEraserMaskContent(SKBitmap? bitmap)
        {
            if (bitmap is null)
            {
                return false;
            }

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (IsAiEraserMaskPixelVisible(bitmap.GetPixel(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private SKBitmap? CreateAiEraserProcessingMaskBitmap()
        {
            if (HasAiEraserMaskContent(_aiEraserPreviewMaskBitmap))
            {
                return _aiEraserPreviewMaskBitmap?.Copy();
            }

            if (HasAiEraserMaskContent(_aiEraserSourceMaskBitmap))
            {
                return _aiEraserSourceMaskBitmap?.Copy();
            }

            return null;
        }

        private static bool IsAiEraserMaskPixelVisible(SKColor color)
        {
            return color.Alpha >= 16 ||
                   color.Red >= 16 ||
                   color.Green >= 16 ||
                   color.Blue >= 16;
        }

        private async void OnAiEraserConfirmClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiEraserBusy || !_isAiEraserEditing)
            {
                return;
            }

            if (!HasAiEraserMaskContent())
            {
                ShowToast(T("ERASER_EMPTY"));
                return;
            }

            var inputPath = ResolveAiEraserInputPath();
            using var processMaskBitmap = CreateAiEraserProcessingMaskBitmap();
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath) || processMaskBitmap is null)
            {
                ShowToast("Unable to start AI eraser for this preview.");
                return;
            }

            var cancellationSource = new CancellationTokenSource();
            _aiEraserCts = cancellationSource;
            _isAiEraserBusy = true;
            SetOwnerAiBusy(true);
            RefreshAiEraserText();
            RefreshPreviewActionStates();

            try
            {
                var result = await _aiInpaintingService
                    .ProcessAsync(inputPath, processMaskBitmap, cancellationSource.Token)
                    .ConfigureAwait(false);

                if (_isClosed || !ReferenceEquals(_aiEraserCts, cancellationSource))
                {
                    _aiInpaintingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                    return;
                }

                var previewBitmap = await Task.Run(
                        () => ImageConversionService.TryCreatePreview(result.ResultPath, StaticPreviewHighQualityWidth),
                        cancellationSource.Token)
                    .ConfigureAwait(false);
                if (previewBitmap is null)
                {
                    _aiInpaintingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                    throw new InvalidOperationException("The AI eraser result could not be rendered back into the preview.");
                }

                var previousResult = _aiEraserActiveResult;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ReleaseAiPreviewCache();
                    _aiCompareSourceBitmap?.Dispose();
                    _aiCompareSourceBitmap = null;
                    _aiCompareSourcePixelSize = default;

                    ReleaseAiMattingCache();
                    _aiMattingCompareSourceBitmap?.Dispose();
                    _aiMattingCompareSourceBitmap = null;
                    _aiMattingCompareSourcePixelSize = default;

                    var oldPreview = _previewBitmap;
                    _previewBitmap = previewBitmap;
                    PreviewImage.Source = previewBitmap;
                    oldPreview?.Dispose();

                    _aiEraserActiveResult = result;
                    ReleaseAiEraserEditorSurface();
                    RefreshAiEraserText();
                    RefreshPreviewActionStates();
                });

                if (previousResult is not null &&
                    !string.Equals(previousResult.WorkingDirectory, result.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _aiInpaintingService.TryDeleteWorkingDirectory(previousResult.WorkingDirectory);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "AI eraser preview generation failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                if (ReferenceEquals(Interlocked.CompareExchange(ref _aiEraserCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }

                _isAiEraserBusy = false;
                SetOwnerAiBusy(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiEraserText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private void OnAiEraserBrushClick(object? sender, RoutedEventArgs e)
        {
            _aiEraserToolMode = AiEraserToolMode.Brush;
            RefreshAiEraserUi();
        }

        private void OnAiEraserEraseClick(object? sender, RoutedEventArgs e)
        {
            _aiEraserToolMode = AiEraserToolMode.Eraser;
            RefreshAiEraserUi();
        }

        private void OnAiEraserSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            _aiEraserBrushSize = Math.Clamp(e.NewValue, AiEraserSizeSlider.Minimum, AiEraserSizeSlider.Maximum);
            RefreshAiEraserText();
        }

        private void OnAiEraserPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isAiEraserEditing ||
                _isAiEraserBusy ||
                !e.GetCurrentPoint(AiEraserInputLayer).Properties.IsLeftButtonPressed ||
                !TryMapAiEraserPoint(e.GetPosition(AiEraserInputLayer), clampToContent: false, out var mappedPoint))
            {
                return;
            }

            _isAiEraserDrawing = true;
            _aiEraserCursorPoint = mappedPoint.DisplayPoint;
            _isAiEraserCursorVisible = true;
            AiEraserInputLayer.Cursor = new Cursor(StandardCursorType.Cross);
            DrawAiEraserSegment(mappedPoint, mappedPoint);
            e.Pointer.Capture(AiEraserInputLayer);
            UpdateAiEraserCursorVisual();
            e.Handled = true;
        }

        private void OnAiEraserPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isAiEraserEditing)
            {
                return;
            }

            var position = e.GetPosition(AiEraserInputLayer);
            if (_isAiEraserDrawing)
            {
                if (!TryMapAiEraserPoint(_aiEraserCursorPoint, clampToContent: true, out var fromPoint) ||
                    !TryMapAiEraserPoint(position, clampToContent: true, out var toPoint))
                {
                    return;
                }

                _aiEraserCursorPoint = toPoint.DisplayPoint;
                _isAiEraserCursorVisible = true;
                DrawAiEraserSegment(fromPoint, toPoint);
                UpdateAiEraserCursorVisual();
                e.Handled = true;
                return;
            }

            if (TryMapAiEraserPoint(position, clampToContent: false, out var hoverPoint))
            {
                _aiEraserCursorPoint = hoverPoint.DisplayPoint;
                _isAiEraserCursorVisible = true;
                UpdateAiEraserCursorVisual();
            }
            else
            {
                _isAiEraserCursorVisible = false;
                UpdateAiEraserCursorVisual();
            }
        }

        private void OnAiEraserPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isAiEraserDrawing)
            {
                return;
            }

            _isAiEraserDrawing = false;
            if (TryMapAiEraserPoint(e.GetPosition(AiEraserInputLayer), clampToContent: false, out var hoverPoint))
            {
                _aiEraserCursorPoint = hoverPoint.DisplayPoint;
                _isAiEraserCursorVisible = true;
            }

            e.Pointer.Capture(null);
            UpdateAiEraserCursorVisual();
            e.Handled = true;
        }

        private void OnAiEraserPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isAiEraserDrawing = false;
        }

        private void OnAiEraserPointerExited(object? sender, PointerEventArgs e)
        {
            if (_isAiEraserDrawing)
            {
                return;
            }

            _isAiEraserCursorVisible = false;
            UpdateAiEraserCursorVisual();
        }

        private void UpdateAiEraserCursorVisual()
        {
            if (!_isAiEraserEditing || !_isAiEraserCursorVisible)
            {
                AiEraserCursorRing.IsVisible = false;
                return;
            }

            var diameter = Math.Max(8d, _aiEraserBrushSize);
            AiEraserCursorRing.IsVisible = true;
            AiEraserCursorRing.Width = diameter;
            AiEraserCursorRing.Height = diameter;
            AiEraserCursorRing.CornerRadius = new CornerRadius(diameter / 2d);
            Canvas.SetLeft(AiEraserCursorRing, _aiEraserCursorPoint.X - (diameter / 2d));
            Canvas.SetTop(AiEraserCursorRing, _aiEraserCursorPoint.Y - (diameter / 2d));
        }

        private readonly record struct AiEraserMappedPoint(
            Point DisplayPoint,
            SKPoint PreviewPoint,
            SKPoint SourcePoint,
            float PreviewDiameter,
            float SourceDiameter);
    }
}
