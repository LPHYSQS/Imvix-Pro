using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ImvixPro.AI.Inpainting.Inference;
using ImvixPro.AI.Inpainting.Models;
using ImvixPro.Models;
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
        private const double DefaultAiEraserBrushSize = AiEraserSettings.DefaultBrushSize;
        private const int AiEraserOverlayPixelStride = 4;
        private static readonly SKColor AiEraserOverlayColor = new(59, 130, 246, 112);

        private enum AiEraserToolMode
        {
            Brush,
            Eraser
        }

        private enum AiEraserPreviewViewMode
        {
            Split,
            OriginalOnly,
            ResultOnly
        }

        private CancellationTokenSource? _aiEraserCts;
        private AiInpaintingResult? _aiEraserActiveResult;
        private WriteableBitmap? _aiEraserOverlayBitmap;
        private SKBitmap? _aiEraserPreviewMaskBitmap;
        private SKBitmap? _aiEraserSourceMaskBitmap;
        private Bitmap? _aiEraserCompareSourceBitmap;
        private byte[]? _aiEraserOverlayRowBuffer;
        private Point _aiEraserCursorPoint;
        private PixelSize _aiEraserSourcePixelSize;
        private bool _isAiEraserEditing;
        private bool _isAiEraserBusy;
        private bool _isAiEraserSaveBusy;
        private bool _isAiEraserDrawing;
        private bool _isAiEraserCursorVisible;
        private bool _isAiEraserCompareActive;
        private bool _isAiEraserCompareDragging;
        private bool _isSyncingAiEraserBrushSize;
        private AiEraserToolMode _aiEraserToolMode = AiEraserToolMode.Brush;
        private double _aiEraserBrushSize = DefaultAiEraserBrushSize;
        private double _aiEraserCompareSplitRatio = 0.5d;
        private AiEraserPreviewViewMode _aiEraserPreviewViewMode = AiEraserPreviewViewMode.Split;

        private void CleanupAiEraser()
        {
            CancelPendingAiEraser();
            ReleaseAiEraserEditorSurface();
            HideAiEraserComparison(refreshUi: false);

            _aiEraserCompareSourceBitmap?.Dispose();
            _aiEraserCompareSourceBitmap = null;
            _isAiEraserSaveBusy = false;

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
            SetWrappedButtonContent(AiEraserEditCloseButton, T("PreviewAiClose"));
            SetWrappedButtonContent(AiEraserOriginalButton, T("PreviewAiViewOriginal"));
            SetWrappedButtonContent(AiEraserResultButton, T("PreviewEraserViewResult"));
            SetWrappedButtonContent(AiEraserCompareButton, T("PreviewAiViewCompare"));
            SetWrappedButtonContent(AiEraserSaveButton, T("PreviewAiSaveAs"));
            SetWrappedButtonContent(AiEraserCloseButton, T("PreviewAiClose"));
            AiEraserHintText.Text = T("PreviewEraserCompareHint");
            AiEraserBusyText.Text = _isAiEraserSaveBusy
                ? T("PreviewAiSaving")
                : T("PreviewEraserBusy");
            AiEraserProcessingNoticeTitleText.Text = T("PreviewProcessingNoticeTitle");
            AiEraserProcessingNoticeBodyText.Text = T("PreviewAiProcessingNotice");
            SyncAiEraserBrushSizeFromPreviewToolState();
            RefreshAiEraserBrushSizeDisplay();
            RefreshAiEraserUi();
        }

        private void RefreshAiEraserUi()
        {
            SyncAiEraserBrushSizeFromPreviewToolState();
            RefreshAiEraserBrushSizeDisplay();

            var shouldShowButton = _sourceAiInpaintingEligible &&
                                   !_isAiEraserEditing &&
                                   !_isAiEraserCompareActive;
            var sourceBitmap = ResolveAiEraserCompareSourceBitmap();
            var hasOriginalView = sourceBitmap is not null;
            var hasResultView = _aiEraserActiveResult is not null && _previewBitmap is not null;
            var isSplitView = _isAiEraserCompareActive &&
                              _aiEraserPreviewViewMode == AiEraserPreviewViewMode.Split &&
                              hasOriginalView &&
                              hasResultView;
            var isBlockedByOtherPreview = _isAiPreviewBusy ||
                                          _isAiSaveBusy ||
                                          _isAiCompareActive ||
                                          _isAiMattingBusy ||
                                          _isAiMattingSaveBusy ||
                                          _isAiMattingCompareActive;
            var isBlockedByRecognitionSession = HasActiveRecognitionSession();

            AiEraserButton.IsVisible = shouldShowButton;
            AiEraserButton.IsEnabled = shouldShowButton &&
                                       !_isAiEraserBusy &&
                                       !_isAiEraserSaveBusy &&
                                       !isBlockedByOtherPreview &&
                                       !isBlockedByRecognitionSession;

            AiEraserToolbar.IsVisible = _isAiEraserEditing;
            AiEraserToolbar.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy;
            AiEraserEditCloseButton.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy;
            AiEraserEditHost.IsVisible = _isAiEraserEditing;
            AiEraserInputLayer.IsVisible = _isAiEraserEditing;
            AiEraserBusyOverlay.IsVisible = _isAiEraserBusy || _isAiEraserSaveBusy;
            AiEraserProcessingNoticeCard.IsVisible = _isAiEraserBusy;
            AiEraserMaskOverlayImage.Source = _isAiEraserEditing ? _aiEraserOverlayBitmap : null;
            AiEraserInputLayer.Cursor = _isAiEraserEditing ? new Cursor(StandardCursorType.Cross) : null;

            AiEraserCompareHost.IsVisible = isSplitView;
            AiEraserCompareInputLayer.IsVisible = isSplitView;
            AiEraserPreviewActionBar.IsVisible = _isAiEraserCompareActive;
            AiEraserOriginalButton.IsVisible = _isAiEraserCompareActive;
            AiEraserResultButton.IsVisible = _isAiEraserCompareActive;
            AiEraserCompareButton.IsVisible = _isAiEraserCompareActive;
            AiEraserSaveButton.IsEnabled = _isAiEraserCompareActive &&
                                           !_isAiEraserBusy &&
                                           !_isAiEraserSaveBusy &&
                                           ResolveAiEraserResultPath() is not null;
            AiEraserCloseButton.IsEnabled = _isAiEraserCompareActive &&
                                            !_isAiEraserBusy &&
                                            !_isAiEraserSaveBusy;

            ApplyAiPreviewModeButtonState(
                AiEraserBrushButton,
                _aiEraserToolMode == AiEraserToolMode.Brush,
                _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiEraserEraseButton,
                _aiEraserToolMode == AiEraserToolMode.Eraser,
                _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiEraserOriginalButton,
                _isAiEraserCompareActive && _aiEraserPreviewViewMode == AiEraserPreviewViewMode.OriginalOnly,
                _isAiEraserCompareActive && hasOriginalView && !_isAiEraserBusy && !_isAiEraserSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiEraserResultButton,
                _isAiEraserCompareActive && _aiEraserPreviewViewMode == AiEraserPreviewViewMode.ResultOnly,
                _isAiEraserCompareActive && hasResultView && !_isAiEraserBusy && !_isAiEraserSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiEraserCompareButton,
                _isAiEraserCompareActive && _aiEraserPreviewViewMode == AiEraserPreviewViewMode.Split,
                _isAiEraserCompareActive && hasOriginalView && hasResultView && !_isAiEraserBusy && !_isAiEraserSaveBusy);

            AiEraserConfirmButton.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy;
            AiEraserSizeSlider.IsEnabled = _isAiEraserEditing && !_isAiEraserBusy && !_isAiEraserSaveBusy;
            AiEraserCursorCanvas.IsVisible = _isAiEraserEditing;
            UpdateAiEraserCursorVisual();

            if (isSplitView)
            {
                AiEraserCompareBeforeImage.Source = sourceBitmap;
                AiEraserCompareAfterImage.Source = _previewBitmap;
                AiEraserCompareInputLayer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                UpdateAiEraserCompareLayout();
            }
            else if (_isAiEraserCompareActive)
            {
                AiEraserCompareBeforeImage.Source = sourceBitmap;
                AiEraserCompareAfterImage.Source = _previewBitmap;
                PreviewImage.Source = ResolveAiEraserSingleViewBitmap();
                AiEraserCompareInputLayer.Cursor = null;
            }
            else
            {
                if (!_isAiCompareActive && !_isAiMattingCompareActive)
                {
                    RestoreStandardPreviewSource();
                }

                AiEraserCompareInputLayer.Cursor = null;
            }
        }

        private void SyncAiEraserBrushSizeFromPreviewToolState()
        {
            if (!_isAiEraserEditing ||
                _isAiEraserDrawing ||
                _previewAiEraserBrushSizeChanged is null)
            {
                return;
            }

            var syncedBrushSize = Math.Clamp(
                AiEraserSettings.NormalizeBrushSize(ResolvePreviewToolState().AiEraserDefaultBrushSize),
                AiEraserSizeSlider.Minimum,
                AiEraserSizeSlider.Maximum);
            if (Math.Abs(_aiEraserBrushSize - syncedBrushSize) <= 0.1d)
            {
                return;
            }

            _aiEraserBrushSize = syncedBrushSize;
        }

        private void RefreshAiEraserBrushSizeDisplay()
        {
            AiEraserSizeLabel.Text = string.Create(
                CultureInfo.CurrentCulture,
                $"{T("ERASER_SIZE")} ({Math.Round(_aiEraserBrushSize, MidpointRounding.AwayFromZero)})");

            var sliderValue = Math.Clamp(_aiEraserBrushSize, AiEraserSizeSlider.Minimum, AiEraserSizeSlider.Maximum);
            if (Math.Abs(AiEraserSizeSlider.Value - sliderValue) <= 0.1d)
            {
                return;
            }

            _isSyncingAiEraserBrushSize = true;
            try
            {
                AiEraserSizeSlider.Value = sliderValue;
            }
            finally
            {
                _isSyncingAiEraserBrushSize = false;
            }
        }

        private Bitmap? ResolveAiEraserCompareSourceBitmap()
        {
            return _aiEraserCompareSourceBitmap;
        }

        private Bitmap? ResolveAiEraserSingleViewBitmap()
        {
            return _aiEraserPreviewViewMode switch
            {
                AiEraserPreviewViewMode.OriginalOnly => ResolveAiEraserCompareSourceBitmap(),
                AiEraserPreviewViewMode.ResultOnly => _previewBitmap,
                _ => null
            };
        }

        private string? ResolveAiEraserResultPath()
        {
            return _aiEraserActiveResult is { ResultPath: { } resultPath } && File.Exists(resultPath)
                ? resultPath
                : null;
        }

        private async void OnAiEraserClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiEraserEditing ||
                _isAiEraserBusy ||
                _isAiEraserCompareActive ||
                _isAiEraserSaveBusy ||
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
                ShowToast(T("PreviewEraserFailed"));
                return;
            }

            RefreshPreviewActionStates();
        }

        private bool TryBeginAiEraserEditing()
        {
            var inputPath = ResolveAiEraserInputPath();
            var previewToolState = ResolvePreviewToolState();
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
            _aiEraserOverlayRowBuffer = null;
            _isAiEraserEditing = true;
            _isAiEraserDrawing = false;
            _isAiEraserCursorVisible = false;
            _aiEraserBrushSize = Math.Clamp(
                AiEraserSettings.NormalizeBrushSize(previewToolState.AiEraserDefaultBrushSize),
                AiEraserSizeSlider.Minimum,
                AiEraserSizeSlider.Maximum);
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
            AiEraserInputLayer.Cursor = null;

            _aiEraserPreviewMaskBitmap?.Dispose();
            _aiEraserPreviewMaskBitmap = null;

            _aiEraserSourceMaskBitmap?.Dispose();
            _aiEraserSourceMaskBitmap = null;

            _aiEraserOverlayBitmap?.Dispose();
            _aiEraserOverlayBitmap = null;
            _aiEraserOverlayRowBuffer = null;
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

        private PreviewToolState ResolvePreviewToolState()
        {
            return _previewSessionStateProvider?.Invoke()?.PreviewToolState?.Clone() ?? new PreviewToolState();
        }

        private AiInpaintingOptions BuildAiInpaintingOptions()
        {
            var previewToolState = ResolvePreviewToolState();
            return new AiInpaintingOptions
            {
                MaskExpansionPixels = AiEraserSettings.NormalizeMaskExpansionPixels(previewToolState.AiEraserMaskExpansionPixels),
                EdgeBlendStrength = AiEraserSettings.NormalizeEdgeBlendStrength(previewToolState.AiEraserEdgeBlendStrength)
            };
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

        private void SyncAiEraserOverlayToUi(SKRectI? dirtyRect = null)
        {
            if (_aiEraserPreviewMaskBitmap is null ||
                _aiEraserOverlayBitmap is null)
            {
                return;
            }

            var copyRect = dirtyRect ?? new SKRectI(0, 0, _aiEraserPreviewMaskBitmap.Width, _aiEraserPreviewMaskBitmap.Height);
            copyRect = ClampAiEraserDirtyRect(copyRect, _aiEraserPreviewMaskBitmap.Width, _aiEraserPreviewMaskBitmap.Height);
            if (copyRect.Width <= 0 || copyRect.Height <= 0)
            {
                return;
            }

            using var framebuffer = _aiEraserOverlayBitmap.Lock();
            var rowCopyBytes = copyRect.Width * AiEraserOverlayPixelStride;
            var rowBuffer = EnsureAiEraserOverlayRowBuffer(rowCopyBytes);
            var sourcePixels = _aiEraserPreviewMaskBitmap.GetPixels();
            for (var row = copyRect.Top; row < copyRect.Bottom; row++)
            {
                var sourceOffset = (row * _aiEraserPreviewMaskBitmap.RowBytes) + (copyRect.Left * AiEraserOverlayPixelStride);
                var destinationOffset = (row * framebuffer.RowBytes) + (copyRect.Left * AiEraserOverlayPixelStride);
                Marshal.Copy(
                    IntPtr.Add(sourcePixels, sourceOffset),
                    rowBuffer,
                    0,
                    rowCopyBytes);
                Marshal.Copy(
                    rowBuffer,
                    0,
                    framebuffer.Address + destinationOffset,
                    rowCopyBytes);
            }
        }

        private byte[] EnsureAiEraserOverlayRowBuffer(int requiredLength)
        {
            if (_aiEraserOverlayRowBuffer is null || _aiEraserOverlayRowBuffer.Length < requiredLength)
            {
                _aiEraserOverlayRowBuffer = new byte[requiredLength];
            }

            return _aiEraserOverlayRowBuffer;
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
            SyncAiEraserOverlayToUi(CalculateAiEraserDirtyRect(
                _aiEraserPreviewMaskBitmap,
                fromPoint.PreviewPoint,
                toPoint.PreviewPoint,
                fromPoint.PreviewDiameter));
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

        private static SKRectI CalculateAiEraserDirtyRect(
            SKBitmap bitmap,
            SKPoint fromPoint,
            SKPoint toPoint,
            float diameter)
        {
            var padding = Math.Max(2f, (diameter / 2f) + 2f);
            var left = (int)Math.Floor(Math.Min(fromPoint.X, toPoint.X) - padding);
            var top = (int)Math.Floor(Math.Min(fromPoint.Y, toPoint.Y) - padding);
            var right = (int)Math.Ceiling(Math.Max(fromPoint.X, toPoint.X) + padding);
            var bottom = (int)Math.Ceiling(Math.Max(fromPoint.Y, toPoint.Y) + padding);

            return ClampAiEraserDirtyRect(new SKRectI(left, top, right, bottom), bitmap.Width, bitmap.Height);
        }

        private static SKRectI ClampAiEraserDirtyRect(SKRectI rect, int width, int height)
        {
            var left = Math.Clamp(rect.Left, 0, width);
            var top = Math.Clamp(rect.Top, 0, height);
            var right = Math.Clamp(rect.Right, left, width);
            var bottom = Math.Clamp(rect.Bottom, top, height);
            return new SKRectI(left, top, right, bottom);
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

        private void ShowAiEraserComparison()
        {
            if (ResolveAiEraserCompareSourceBitmap() is null || _previewBitmap is null)
            {
                return;
            }

            RefreshPreviewCompareRefreshRate();
            _isAiEraserCompareActive = true;
            _aiEraserPreviewViewMode = AiEraserPreviewViewMode.Split;
            _aiEraserCompareSplitRatio = 0.5d;
            RefreshPreviewActionStates();
        }

        private void HideAiEraserComparison(bool refreshUi = true)
        {
            _isAiEraserCompareActive = false;
            _isAiEraserCompareDragging = false;
            _aiEraserPreviewViewMode = AiEraserPreviewViewMode.Split;
            _aiEraserCompareSplitRatio = 0.5d;
            ClearPendingPreviewCompareUpdates();
            ResetAiEraserCompareOverlayVisuals();
            AiEraserCompareBeforeImage.Source = null;
            AiEraserCompareAfterImage.Source = null;

            if (refreshUi)
            {
                RefreshPreviewActionStates();
            }
        }

        private async void OnAiEraserConfirmClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiEraserBusy || _isAiEraserSaveBusy || !_isAiEraserEditing)
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
            var currentPreview = _previewBitmap;
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath) || processMaskBitmap is null || currentPreview is null)
            {
                ShowToast(T("PreviewEraserFailed"));
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
                    .ProcessAsync(inputPath, processMaskBitmap, BuildAiInpaintingOptions(), cancellationSource.Token)
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
                    throw new InvalidOperationException(T("PreviewEraserFailed"));
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

                    HideAiEraserComparison(refreshUi: false);
                    _aiEraserCompareSourceBitmap?.Dispose();
                    _aiEraserCompareSourceBitmap = currentPreview;

                    _previewBitmap = previewBitmap;
                    PreviewImage.Source = previewBitmap;

                    _aiEraserActiveResult = result;
                    ReleaseAiEraserEditorSurface();
                    ShowAiEraserComparison();
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

        private async void OnSaveAiEraserClick(object? sender, RoutedEventArgs e)
        {
            var resultPath = ResolveAiEraserResultPath();
            if (_isAiEraserSaveBusy ||
                _isAiEraserBusy ||
                string.IsNullOrWhiteSpace(resultPath))
            {
                return;
            }

            var options = BuildAiPreviewSaveOptions();
            var extension = ImageConversionService.GetFileExtension(options.OutputFormat);
            var suggestedName = string.IsNullOrWhiteSpace(_sourceFilePath)
                ? "eraser_result"
                : $"{Path.GetFileNameWithoutExtension(_sourceFilePath)}_eraser";
            var fileType = new FilePickerFileType(options.OutputFormat.ToString().ToUpperInvariant())
            {
                Patterns = [$"*{extension}"]
            };

            var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = T("PreviewAiSaveAs"),
                SuggestedFileName = suggestedName,
                DefaultExtension = extension.TrimStart('.'),
                FileTypeChoices = [fileType],
                ShowOverwritePrompt = true
            });

            var localPath = destination?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                return;
            }

            _isAiEraserSaveBusy = true;
            RefreshAiEraserText();
            RefreshPreviewActionStates();

            try
            {
                var destinationPath = EnsureOutputExtension(localPath, extension);
                await _imageConversionService.ExportRasterToPathAsync(resultPath, destinationPath, options).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(T("PreviewEraserSaved")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "Saving the AI eraser output failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                _isAiEraserSaveBusy = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiEraserText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private void OnCloseAiEraserEditClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiEraserEditing || _isAiEraserBusy || _isAiEraserSaveBusy)
            {
                return;
            }

            ReleaseAiEraserEditorSurface();
            RefreshPreviewActionStates();
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
            var normalizedBrushSize = Math.Clamp(e.NewValue, AiEraserSizeSlider.Minimum, AiEraserSizeSlider.Maximum);
            if (Math.Abs(_aiEraserBrushSize - normalizedBrushSize) <= 0.1d)
            {
                return;
            }

            _aiEraserBrushSize = normalizedBrushSize;
            if (!_isSyncingAiEraserBrushSize)
            {
                _previewAiEraserBrushSizeChanged?.Invoke(
                    AiEraserSettings.NormalizeBrushSize((int)Math.Round(normalizedBrushSize, MidpointRounding.AwayFromZero)));
            }

            RefreshAiEraserText();
        }

        private void OnShowAiEraserOriginalClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiEraserCompareActive || ResolveAiEraserCompareSourceBitmap() is null)
            {
                return;
            }

            _aiEraserPreviewViewMode = AiEraserPreviewViewMode.OriginalOnly;
            RefreshAiEraserUi();
        }

        private void OnShowAiEraserResultClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiEraserCompareActive || _previewBitmap is null)
            {
                return;
            }

            _aiEraserPreviewViewMode = AiEraserPreviewViewMode.ResultOnly;
            RefreshAiEraserUi();
        }

        private void OnShowAiEraserCompareClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiEraserCompareActive || ResolveAiEraserCompareSourceBitmap() is null || _previewBitmap is null)
            {
                return;
            }

            _aiEraserPreviewViewMode = AiEraserPreviewViewMode.Split;
            RefreshAiEraserUi();
        }

        private void OnCloseAiEraserClick(object? sender, RoutedEventArgs e)
        {
            HideAiEraserComparison();
        }

        private void OnAiEraserComparePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isAiEraserCompareActive ||
                _aiEraserPreviewViewMode != AiEraserPreviewViewMode.Split ||
                !e.GetCurrentPoint(AiEraserCompareInputLayer).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _isAiEraserCompareDragging = true;
            e.Pointer.Capture(AiEraserCompareInputLayer);
            QueueAiEraserCompareFrameUpdate(e.GetPosition(AiEraserCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiEraserComparePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isAiEraserCompareDragging || !_isAiEraserCompareActive || _aiEraserPreviewViewMode != AiEraserPreviewViewMode.Split)
            {
                return;
            }

            QueueAiEraserCompareFrameUpdate(e.GetPosition(AiEraserCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiEraserComparePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isAiEraserCompareDragging)
            {
                return;
            }

            _isAiEraserCompareDragging = false;
            QueueAiEraserCompareFrameUpdate(e.GetPosition(AiEraserCompareInputLayer));
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void OnAiEraserComparePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isAiEraserCompareDragging = false;
        }

        private bool ApplyAiEraserCompareSplitFromPoint(Point point)
        {
            var contentRect = GetAiEraserCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return false;
            }

            var clampedX = Math.Clamp(point.X, contentRect.X, contentRect.Right);
            _aiEraserCompareSplitRatio = (clampedX - contentRect.X) / contentRect.Width;
            UpdateAiEraserCompareLayout();
            return true;
        }

        private void UpdateAiEraserCompareLayout()
        {
            if (!_isAiEraserCompareActive || _aiEraserPreviewViewMode != AiEraserPreviewViewMode.Split || _previewBitmap is null)
            {
                ResetAiEraserCompareOverlayVisuals();
                return;
            }

            var contentRect = GetAiEraserCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                ResetAiEraserCompareOverlayVisuals();
                return;
            }

            var splitX = contentRect.X + (contentRect.Width * Math.Clamp(_aiEraserCompareSplitRatio, 0d, 1d));
            var lineWidth = double.IsNaN(AiEraserCompareSplitterLine.Width) || AiEraserCompareSplitterLine.Width <= 0
                ? 2d
                : AiEraserCompareSplitterLine.Width;
            var thumbWidth = double.IsNaN(AiEraserCompareThumb.Width) || AiEraserCompareThumb.Width <= 0
                ? 22d
                : AiEraserCompareThumb.Width;
            var thumbHeight = double.IsNaN(AiEraserCompareThumb.Height) || AiEraserCompareThumb.Height <= 0
                ? 22d
                : AiEraserCompareThumb.Height;

            AiEraserCompareAfterClipHost.Clip = new RectangleGeometry(new Rect(
                splitX,
                contentRect.Y,
                Math.Max(0d, contentRect.Right - splitX),
                contentRect.Height));

            AiEraserCompareSplitterLine.Height = contentRect.Height;
            _aiEraserCompareSplitterTransform.X = splitX - (lineWidth / 2d);
            _aiEraserCompareSplitterTransform.Y = contentRect.Y;
            _aiEraserCompareThumbTransform.X = splitX - (thumbWidth / 2d);
            _aiEraserCompareThumbTransform.Y = contentRect.Center.Y - (thumbHeight / 2d);
        }

        private Rect GetAiEraserCompareContentRect()
        {
            var bounds = AiEraserCompareHost.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0 || _previewBitmap is null)
            {
                return default;
            }

            var pixelSize = _previewBitmap.PixelSize;
            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
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

        private void OnAiEraserPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isAiEraserEditing ||
                _isAiEraserBusy ||
                _isAiEraserSaveBusy ||
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
