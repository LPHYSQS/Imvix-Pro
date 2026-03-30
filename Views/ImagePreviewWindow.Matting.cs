using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ImvixPro.AI.Matting.Inference;
using ImvixPro.AI.Matting.Models;
using ImvixPro.AI.Matting.UI;
using ImvixPro.Models;
using ImvixPro.Services;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow
    {
        private enum AiMattingPreviewViewMode
        {
            Split,
            OriginalOnly,
            ResultOnly
        }

        private CancellationTokenSource? _aiMattingCts;
        private AiMattingResult? _aiMattingResult;
        private string? _aiMattingCacheKey;
        private string? _aiMattingResultPath;
        private Bitmap? _aiMattingCompareSourceBitmap;
        private PixelSize _aiMattingCompareSourcePixelSize;
        private Bitmap? _aiMattingPreviewBitmap;
        private PixelSize _aiMattingPreviewPixelSize;
        private bool _isAiMattingBusy;
        private bool _isAiMattingSaveBusy;
        private bool _isAiMattingCompareActive;
        private bool _isAiMattingCompareDragging;
        private double _aiMattingCompareSplitRatio = 0.5d;
        private AiMattingPreviewViewMode _aiMattingPreviewViewMode = AiMattingPreviewViewMode.Split;

        private void CleanupAiMattingPreview()
        {
            CancelPendingAiMatting();
            ReleaseAiMattingCache();
            _aiMattingCompareSourceBitmap?.Dispose();
            _aiMattingCompareSourceBitmap = null;
            _aiMattingCompareSourcePixelSize = default;
        }

        private void RefreshAiMattingText()
        {
            SetWrappedButtonContent(
                AiMattingButton,
                _isAiMattingBusy
                    ? T("PreviewMattingBusy")
                    : T("PreviewMattingButton"));
            SetWrappedButtonContent(AiMattingOriginalButton, T("PreviewMattingViewOriginal"));
            SetWrappedButtonContent(AiMattingResultButton, T("PreviewMattingViewResult"));
            SetWrappedButtonContent(AiMattingCompareButton, T("PreviewMattingViewCompare"));
            SetWrappedButtonContent(AiMattingSaveButton, T("PreviewMattingSaveAs"));
            SetWrappedButtonContent(AiMattingCloseButton, T("PreviewMattingClose"));
            AiMattingHintText.Text = T("PreviewMattingCompareHint");
            AiMattingBusyText.Text = _isAiMattingSaveBusy
                ? T("PreviewMattingSaving")
                : T("PreviewMattingBusy");
            RefreshPreviewActionStates();
        }

        private void RefreshAiMattingUi()
        {
            var shouldShowMattingButton = (_sourceAiMattingEligible && !_isAiMattingCompareActive) || _isAiMattingBusy;
            var sourceBitmap = ResolveAiMattingCompareSourceBitmap();
            var hasOriginalView = sourceBitmap is not null;
            var hasResultView = _aiMattingPreviewBitmap is not null;
            var isSplitView = _isAiMattingCompareActive &&
                              _aiMattingPreviewViewMode == AiMattingPreviewViewMode.Split &&
                              hasOriginalView &&
                              hasResultView;
            var isBlockedByOtherPreview = _isAiPreviewBusy || _isAiCompareActive || _isAiSaveBusy;
            var isBlockedByRecognitionSession = HasActiveRecognitionSession();

            AiMattingButton.IsVisible = shouldShowMattingButton;
            AiMattingButton.IsEnabled = shouldShowMattingButton &&
                                        !_isAiMattingBusy &&
                                        !_isAiMattingSaveBusy &&
                                        !_isAiEraserEditing &&
                                        !_isAiEraserBusy &&
                                        !isBlockedByOtherPreview &&
                                        !isBlockedByRecognitionSession;

            AiMattingBusyOverlay.IsVisible = _isAiMattingBusy || _isAiMattingSaveBusy;
            AiMattingCompareHost.IsVisible = isSplitView;
            AiMattingCompareInputLayer.IsVisible = isSplitView;
            AiMattingPreviewActionBar.IsVisible = _isAiMattingCompareActive;
            AiMattingOriginalButton.IsVisible = _isAiMattingCompareActive;
            AiMattingResultButton.IsVisible = _isAiMattingCompareActive;
            AiMattingCompareButton.IsVisible = _isAiMattingCompareActive;
            AiMattingSaveButton.IsEnabled = _isAiMattingCompareActive &&
                                            !_isAiMattingBusy &&
                                            !_isAiMattingSaveBusy &&
                                            !string.IsNullOrWhiteSpace(_aiMattingResultPath) &&
                                            File.Exists(_aiMattingResultPath);
            AiMattingCloseButton.IsEnabled = _isAiMattingCompareActive && !_isAiMattingBusy && !_isAiMattingSaveBusy;
            PreviewImage.IsVisible = !_isAiMattingCompareActive || !isSplitView;

            ApplyAiPreviewModeButtonState(
                AiMattingOriginalButton,
                _isAiMattingCompareActive && _aiMattingPreviewViewMode == AiMattingPreviewViewMode.OriginalOnly,
                _isAiMattingCompareActive && hasOriginalView && !_isAiMattingBusy && !_isAiMattingSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiMattingResultButton,
                _isAiMattingCompareActive && _aiMattingPreviewViewMode == AiMattingPreviewViewMode.ResultOnly,
                _isAiMattingCompareActive && hasResultView && !_isAiMattingBusy && !_isAiMattingSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiMattingCompareButton,
                _isAiMattingCompareActive && _aiMattingPreviewViewMode == AiMattingPreviewViewMode.Split,
                _isAiMattingCompareActive && hasOriginalView && hasResultView && !_isAiMattingBusy && !_isAiMattingSaveBusy);

            if (isSplitView)
            {
                AiMattingCompareBeforeImage.Source = sourceBitmap;
                AiMattingCompareAfterImage.Source = _aiMattingPreviewBitmap;
                AiMattingCompareInputLayer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                UpdateAiMattingCompareLayout();
            }
            else if (_isAiMattingCompareActive)
            {
                AiMattingCompareBeforeImage.Source = sourceBitmap;
                AiMattingCompareAfterImage.Source = _aiMattingPreviewBitmap;
                PreviewImage.Source = ResolveAiMattingSingleViewBitmap();
                AiMattingCompareInputLayer.Cursor = null;
            }
            else
            {
                RestoreStandardPreviewSource();
                AiMattingCompareInputLayer.Cursor = null;
            }
        }

        private Bitmap? ResolveAiMattingCompareSourceBitmap()
        {
            return _aiMattingCompareSourceBitmap ?? _previewBitmap;
        }

        private Bitmap? ResolveAiMattingSingleViewBitmap()
        {
            return _aiMattingPreviewViewMode switch
            {
                AiMattingPreviewViewMode.OriginalOnly => ResolveAiMattingCompareSourceBitmap(),
                AiMattingPreviewViewMode.ResultOnly => _aiMattingPreviewBitmap,
                _ => null
            };
        }

        private void CancelPendingAiMatting()
        {
            var cancellationSource = Interlocked.Exchange(ref _aiMattingCts, null);
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
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending AI matting work cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private void ReleaseAiMattingCache()
        {
            _isAiMattingCompareActive = false;
            _isAiMattingCompareDragging = false;
            _aiMattingCompareSplitRatio = 0.5d;
            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.Split;
            ClearPendingPreviewCompareUpdates();
            ResetAiMattingOverlayVisuals();
            AiMattingCompareBeforeImage.Source = null;
            AiMattingCompareAfterImage.Source = null;

            _aiMattingPreviewBitmap?.Dispose();
            _aiMattingPreviewBitmap = null;
            _aiMattingPreviewPixelSize = default;
            _aiMattingCacheKey = null;
            _aiMattingResultPath = null;

            var result = _aiMattingResult;
            _aiMattingResult = null;
            if (result is not null && !string.IsNullOrWhiteSpace(result.WorkingDirectory))
            {
                _aiMattingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
            }

            RefreshPreviewActionStates();
        }

        private string BuildAiMattingCacheKey(PreviewToolState toolState)
        {
            ArgumentNullException.ThrowIfNull(toolState);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{_sourceFilePath}|{toolState.AiMattingModel}|{toolState.AiMattingDevice}|{toolState.AiMattingResolutionMode}|{toolState.AiMattingEdgeOptimizationEnabled}|{toolState.AiMattingEdgeOptimizationStrength}");
        }

        private PreviewToolState BuildAiMattingState()
        {
            var session = _previewSessionStateProvider?.Invoke();
            return session?.PreviewToolState.Clone() ?? new PreviewToolState();
        }

        private ConversionOptions BuildAiMattingSaveOptions(PreviewToolState toolState)
        {
            ArgumentNullException.ThrowIfNull(toolState);

            var options = BuildAiEnhancementOptions();
            options.OutputFormat = AiMattingFormatCatalog.Normalize(toolState.AiMattingOutputFormat);
            options.ResizeMode = ResizeMode.None;
            options.RenameMode = RenameMode.KeepOriginal;
            options.RenamePrefix = string.Empty;
            options.RenameSuffix = string.Empty;
            options.OutputDirectoryRule = OutputDirectoryRule.SpecificFolder;
            options.OutputDirectory = string.Empty;
            options.AllowOverwrite = true;
            options.GifHandlingMode = GifHandlingMode.FirstFrame;
            options.GifSpecificFrameIndex = 0;
            options.GifSpecificFrameSelections.Clear();
            options.GifFrameRanges.Clear();
            options.PdfImageExportMode = PdfImageExportMode.CurrentPage;
            options.PdfDocumentExportMode = PdfDocumentExportMode.CurrentPage;
            options.PdfPageIndex = 0;
            options.PdfPageSelections.Clear();
            options.PdfPageRanges.Clear();
            options.AiEnhancementEnabled = false;
            options.MaxDegreeOfParallelism = 1;
            return options;
        }

        private async void OnAiMattingClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiMattingBusy ||
                _isAiMattingSaveBusy ||
                _isAiPreviewBusy ||
                _isAiEraserEditing ||
                _isAiEraserBusy ||
                _isAiCompareActive ||
                _isAiSaveBusy ||
                string.IsNullOrWhiteSpace(_sourceFilePath) ||
                !_sourceAiMattingEligible)
            {
                return;
            }

            var toolState = BuildAiMattingState();
            var cacheKey = BuildAiMattingCacheKey(toolState);
            if (string.Equals(_aiMattingCacheKey, cacheKey, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(_aiMattingResultPath) &&
                File.Exists(_aiMattingResultPath) &&
                _aiMattingPreviewBitmap is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(ShowAiMattingComparison);
                RefreshPreviewActionStates();
                return;
            }

            await CloseRecognitionPanelForAiPreviewAsync();
            HideAiPreviewComparison();
            ReleaseAiMattingCache();

            var cancellationSource = new CancellationTokenSource();
            _aiMattingCts = cancellationSource;
            _isAiMattingBusy = true;
            RefreshAiMattingText();
            RefreshPreviewActionStates();

            try
            {
                var result = await _aiMattingService.ProcessAsync(
                        _sourceFilePath,
                        toolState.AiMattingModel,
                        toolState.AiMattingDevice,
                        toolState.AiMattingResolutionMode,
                        toolState.AiMattingEdgeOptimizationEnabled,
                        toolState.AiMattingEdgeOptimizationStrength,
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (_isClosed || !ReferenceEquals(_aiMattingCts, cancellationSource))
                {
                    _aiMattingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                    return;
                }

                if (string.IsNullOrWhiteSpace(result.ResultPath) || !File.Exists(result.ResultPath))
                {
                    _aiMattingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                    ShowToast(T("PreviewMattingFailed"));
                    return;
                }

                var sourceBitmapTask = _aiMattingCompareSourceBitmap is null
                    ? Task.Run(() => LoadAiMattingCompareBitmap(_sourceFilePath!), cancellationSource.Token)
                    : Task.FromResult<AiPreviewBitmapLoadResult?>(null);
                var resultBitmapTask = Task.Run(() => LoadAiMattingCompareBitmap(result.ResultPath), cancellationSource.Token);

                await Task.WhenAll(sourceBitmapTask, resultBitmapTask).ConfigureAwait(false);

                var sourceBitmapResult = await sourceBitmapTask.ConfigureAwait(false);
                var resultBitmapResult = await resultBitmapTask.ConfigureAwait(false);
                if (resultBitmapResult?.Bitmap is null)
                {
                    sourceBitmapResult?.Dispose();
                    _aiMattingService.TryDeleteWorkingDirectory(result.WorkingDirectory);
                    ShowToast(T("PreviewMattingFailed"));
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (sourceBitmapResult is not null)
                    {
                        _aiMattingCompareSourceBitmap?.Dispose();
                        _aiMattingCompareSourceBitmap = sourceBitmapResult.Bitmap;
                        _aiMattingCompareSourcePixelSize = sourceBitmapResult.SourceSize;
                    }

                    _aiMattingPreviewBitmap?.Dispose();
                    _aiMattingResult = result;
                    _aiMattingCacheKey = cacheKey;
                    _aiMattingResultPath = result.ResultPath;
                    _aiMattingPreviewBitmap = resultBitmapResult.Bitmap;
                    _aiMattingPreviewPixelSize = resultBitmapResult.SourceSize;
                    ShowAiMattingComparison();
                    RefreshAiMattingText();
                    RefreshPreviewActionStates();

                    if (result.UsedGpuFallback)
                    {
                        ShowToast(T("PreviewMattingGpuFallback"));
                    }

                    if (result.UsedModelFallback)
                    {
                        ShowToast(string.Format(
                            CultureInfo.CurrentCulture,
                            T("PreviewMattingModelFallbackTemplate"),
                            AiMattingModelCatalog.BuildDisplayName(toolState.AiMattingModel, T),
                            AiMattingModelCatalog.BuildDisplayName(result.EffectiveModel, T)));
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "AI matting preview generation failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                if (ReferenceEquals(Interlocked.CompareExchange(ref _aiMattingCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }

                _isAiMattingBusy = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiMattingText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private AiPreviewBitmapLoadResult? LoadAiMattingCompareBitmap(string filePath)
        {
            return _aiPreviewComparisonService.TryLoadAsset(
                filePath,
                GetAiMattingCompareViewportWidth(),
                GetAiMattingCompareRenderScaling());
        }

        private void ShowAiMattingComparison()
        {
            if (ResolveAiMattingCompareSourceBitmap() is null || _aiMattingPreviewBitmap is null)
            {
                return;
            }

            RefreshPreviewCompareRefreshRate();
            _isAiMattingCompareActive = true;
            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.Split;
            _aiMattingCompareSplitRatio = 0.5d;
            RefreshPreviewActionStates();
        }

        private void HideAiMattingComparison()
        {
            _isAiMattingCompareActive = false;
            _isAiMattingCompareDragging = false;
            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.Split;
            ClearPendingPreviewCompareUpdates();
            ResetAiMattingOverlayVisuals();
            RefreshPreviewActionStates();
        }

        private async void OnSaveAiMattingClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiMattingSaveBusy ||
                _isAiMattingBusy ||
                string.IsNullOrWhiteSpace(_aiMattingResultPath) ||
                !File.Exists(_aiMattingResultPath))
            {
                return;
            }

            var toolState = BuildAiMattingState();
            var options = BuildAiMattingSaveOptions(toolState);
            var defaultFormat = AiMattingFormatCatalog.Normalize(toolState.AiMattingOutputFormat);
            var defaultExtension = ImageConversionService.GetFileExtension(defaultFormat);
            var suggestedName = string.IsNullOrWhiteSpace(_sourceFilePath)
                ? "matting_result"
                : $"{Path.GetFileNameWithoutExtension(_sourceFilePath)}_matting";

            var fileTypes = new FilePickerFileType[AiMattingFormatCatalog.SupportedTransparentFormats.Count];
            for (var index = 0; index < AiMattingFormatCatalog.SupportedTransparentFormats.Count; index++)
            {
                var format = AiMattingFormatCatalog.SupportedTransparentFormats[index];
                fileTypes[index] = new FilePickerFileType(AiMattingFormatCatalog.BuildDisplayName(format))
                {
                    Patterns = [$"*{ImageConversionService.GetFileExtension(format)}"]
                };
            }

            var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = T("PreviewMattingSaveAs"),
                SuggestedFileName = suggestedName,
                DefaultExtension = defaultExtension.TrimStart('.'),
                FileTypeChoices = fileTypes,
                ShowOverwritePrompt = true
            });

            var localPath = destination?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                return;
            }

            _isAiMattingSaveBusy = true;
            RefreshAiMattingText();
            RefreshPreviewActionStates();

            try
            {
                var targetFormat = InferMattingOutputFormat(localPath, defaultFormat);
                var extension = ImageConversionService.GetFileExtension(targetFormat);
                options.OutputFormat = targetFormat;
                var destinationPath = EnsureOutputExtension(localPath, extension);
                var exportSourcePath = await ResolveAiMattingExportSourcePathAsync(toolState).ConfigureAwait(false);
                await _imageConversionService.ExportRasterToPathAsync(exportSourcePath, destinationPath, options).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(T("PreviewMattingSaved")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "Saving the AI matting output failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                _isAiMattingSaveBusy = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiMattingText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private async Task<string> ResolveAiMattingExportSourcePathAsync(PreviewToolState toolState)
        {
            ArgumentNullException.ThrowIfNull(toolState);

            if (toolState.AiMattingBackgroundMode != AiMattingBackgroundMode.SolidColor || _aiMattingResult is null)
            {
                return _aiMattingResultPath!;
            }

            var flattenedPath = Path.Combine(_aiMattingResult.WorkingDirectory, "matting-export-solid.png");
            await Task.Run(() => CreateSolidBackgroundExport(_aiMattingResultPath!, flattenedPath, toolState.AiMattingBackgroundColor)).ConfigureAwait(false);
            return flattenedPath;
        }

        private static void CreateSolidBackgroundExport(string sourcePath, string outputPath, string? backgroundColor)
        {
            using var codec = SKCodec.Create(sourcePath);
            if (codec is null)
            {
                throw new InvalidOperationException("The AI matting result cannot be decoded for export.");
            }

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var sourceBitmap = new SKBitmap(info);
            var decodeResult = codec.GetPixels(sourceBitmap.Info, sourceBitmap.GetPixels());
            if (decodeResult != SKCodecResult.Success && decodeResult != SKCodecResult.IncompleteInput)
            {
                throw new InvalidOperationException("The AI matting result cannot be decoded for export.");
            }

            var background = SKColor.TryParse(backgroundColor, out var parsed)
                ? parsed
                : SKColors.White;
            using var outputBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(outputBitmap))
            {
                canvas.Clear(background);
                canvas.DrawBitmap(sourceBitmap, 0, 0);
                canvas.Flush();
            }

            using var image = SKImage.FromBitmap(outputBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
        }

        private static OutputImageFormat InferMattingOutputFormat(string localPath, OutputImageFormat fallbackFormat)
        {
            var extension = Path.GetExtension(localPath);
            foreach (var format in AiMattingFormatCatalog.SupportedTransparentFormats)
            {
                if (string.Equals(extension, ImageConversionService.GetFileExtension(format), StringComparison.OrdinalIgnoreCase))
                {
                    return format;
                }
            }

            return fallbackFormat;
        }

        private void OnShowAiMattingOriginalClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiMattingCompareActive || ResolveAiMattingCompareSourceBitmap() is null)
            {
                return;
            }

            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.OriginalOnly;
            RefreshPreviewActionStates();
        }

        private void OnShowAiMattingResultClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiMattingCompareActive || _aiMattingPreviewBitmap is null)
            {
                return;
            }

            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.ResultOnly;
            RefreshPreviewActionStates();
        }

        private void OnShowAiMattingCompareClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiMattingCompareActive || ResolveAiMattingCompareSourceBitmap() is null || _aiMattingPreviewBitmap is null)
            {
                return;
            }

            _aiMattingPreviewViewMode = AiMattingPreviewViewMode.Split;
            RefreshPreviewActionStates();
        }

        private void OnCloseAiMattingClick(object? sender, RoutedEventArgs e)
        {
            HideAiMattingComparison();
        }

        private void OnAiMattingComparePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isAiMattingCompareActive ||
                _aiMattingPreviewViewMode != AiMattingPreviewViewMode.Split ||
                !e.GetCurrentPoint(AiMattingCompareInputLayer).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _isAiMattingCompareDragging = true;
            e.Pointer.Capture(AiMattingCompareInputLayer);
            QueueAiMattingCompareFrameUpdate(e.GetPosition(AiMattingCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiMattingComparePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isAiMattingCompareDragging || !_isAiMattingCompareActive || _aiMattingPreviewViewMode != AiMattingPreviewViewMode.Split)
            {
                return;
            }

            QueueAiMattingCompareFrameUpdate(e.GetPosition(AiMattingCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiMattingComparePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isAiMattingCompareDragging)
            {
                return;
            }

            _isAiMattingCompareDragging = false;
            QueueAiMattingCompareFrameUpdate(e.GetPosition(AiMattingCompareInputLayer));
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void OnAiMattingComparePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isAiMattingCompareDragging = false;
        }

        private bool ApplyAiMattingCompareSplitFromPoint(Point point)
        {
            var contentRect = GetAiMattingCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return false;
            }

            var clampedX = Math.Clamp(point.X, contentRect.X, contentRect.Right);
            _aiMattingCompareSplitRatio = (clampedX - contentRect.X) / contentRect.Width;
            UpdateAiMattingCompareLayout();
            return true;
        }

        private void UpdateAiMattingCompareLayout()
        {
            if (!_isAiMattingCompareActive || _aiMattingPreviewViewMode != AiMattingPreviewViewMode.Split || _aiMattingPreviewBitmap is null)
            {
                ResetAiMattingOverlayVisuals();
                return;
            }

            var contentRect = GetAiMattingCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                ResetAiMattingOverlayVisuals();
                return;
            }

            var splitX = contentRect.X + (contentRect.Width * Math.Clamp(_aiMattingCompareSplitRatio, 0d, 1d));
            var lineWidth = double.IsNaN(AiMattingCompareSplitterLine.Width) || AiMattingCompareSplitterLine.Width <= 0
                ? 2d
                : AiMattingCompareSplitterLine.Width;
            var thumbWidth = double.IsNaN(AiMattingCompareThumb.Width) || AiMattingCompareThumb.Width <= 0
                ? 22d
                : AiMattingCompareThumb.Width;
            var thumbHeight = double.IsNaN(AiMattingCompareThumb.Height) || AiMattingCompareThumb.Height <= 0
                ? 22d
                : AiMattingCompareThumb.Height;

            AiMattingCompareAfterClipHost.Clip = new RectangleGeometry(new Rect(
                splitX,
                contentRect.Y,
                Math.Max(0d, contentRect.Right - splitX),
                contentRect.Height));

            AiMattingCompareSplitterLine.Height = contentRect.Height;
            _aiMattingCompareSplitterTransform.X = splitX - (lineWidth / 2d);
            _aiMattingCompareSplitterTransform.Y = contentRect.Y;
            _aiMattingCompareThumbTransform.X = splitX - (thumbWidth / 2d);
            _aiMattingCompareThumbTransform.Y = contentRect.Center.Y - (thumbHeight / 2d);
        }

        private Rect GetAiMattingCompareContentRect()
        {
            var bounds = AiMattingCompareHost.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0 || _aiMattingPreviewBitmap is null)
            {
                return default;
            }

            var pixelSize = _aiMattingPreviewBitmap.PixelSize;
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

        private double GetAiMattingCompareViewportWidth()
        {
            var width = PreviewSurface.Bounds.Width;
            if (width <= 0 && AiMattingCompareHost.Bounds.Width > 0)
            {
                width = AiMattingCompareHost.Bounds.Width;
            }

            if (width <= 0)
            {
                width = Math.Max(800d, Bounds.Width - 220d);
            }

            return width;
        }

        private double GetAiMattingCompareRenderScaling()
        {
            return VisualRoot is TopLevel topLevel
                ? Math.Max(1d, topLevel.RenderScaling)
                : 1d;
        }
    }
}







