using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow
    {
        private enum AiPreviewViewMode
        {
            Split,
            OriginalOnly,
            EnhancedOnly
        }

        private readonly AiImageEnhancementService _aiImageEnhancementService;
        private readonly AiPreviewComparisonService _aiPreviewComparisonService;
        private Func<ConversionOptions>? _previewOptionsProvider;
        private Action<bool>? _previewAiBusyChanged;
        private string? _sourceFilePath;
        private bool _sourceAiPreviewEligible;
        private CancellationTokenSource? _aiPreviewCts;
        private AiEnhancementBatchResult? _aiPreviewBatchResult;
        private string? _aiPreviewCacheKey;
        private string? _aiPreviewEnhancedPath;
        private Bitmap? _aiCompareSourceBitmap;
        private PixelSize _aiCompareSourcePixelSize;
        private Bitmap? _aiPreviewBitmap;
        private PixelSize _aiPreviewEnhancedPixelSize;
        private AiPreviewComparisonDiagnostics? _aiPreviewDiagnostics;
        private bool _isAiPreviewBusy;
        private bool _isAiSaveBusy;
        private bool _isAiCompareActive;
        private bool _isAiCompareDragging;
        private bool _isOwnerAiBusyRegistered;
        private double _aiCompareSplitRatio = 0.5d;
        private AiPreviewViewMode _aiPreviewViewMode = AiPreviewViewMode.Split;

        private void InitializeAiPreview(
            string filePath,
            Func<ConversionOptions>? previewOptionsProvider,
            Action<bool>? previewAiBusyChanged,
            bool isSourceAiPreviewEligible)
        {
            _sourceFilePath = filePath;
            _previewOptionsProvider = previewOptionsProvider;
            _previewAiBusyChanged = previewAiBusyChanged;
            _sourceAiPreviewEligible = isSourceAiPreviewEligible;
            Activated += OnWindowActivated;
        }

        private void CleanupAiPreview()
        {
            Activated -= OnWindowActivated;
            CancelPendingAiPreview();
            SetOwnerAiBusy(false);
            ReleaseAiPreviewCache();
            _aiCompareSourceBitmap?.Dispose();
            _aiCompareSourceBitmap = null;
            _aiCompareSourcePixelSize = default;
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            RefreshPreviewActionStates();
        }

        private void RefreshAiPreviewText()
        {
            SetWrappedButtonContent(
                AiPreviewButton,
                _isAiPreviewBusy
                    ? T("PreviewAiBusy")
                    : T("PreviewAiButton"));
            SetWrappedButtonContent(AiPreviewOriginalButton, T("PreviewAiViewOriginal"));
            SetWrappedButtonContent(AiPreviewEnhancedButton, T("PreviewAiViewEnhanced"));
            SetWrappedButtonContent(AiPreviewCompareButton, T("PreviewAiViewCompare"));
            SetWrappedButtonContent(AiPreviewSaveButton, T("PreviewAiSaveAs"));
            SetWrappedButtonContent(AiPreviewCloseButton, T("PreviewAiClose"));
            AiPreviewHintText.Text = T("PreviewAiCompareHint");
            AiBusyText.Text = _isAiSaveBusy
                ? T("PreviewAiSaving")
                : T("PreviewAiBusy");
            RefreshAiPreviewDiagnosticsText();
            RefreshAiPreviewUi();
        }

        private void RefreshAiPreviewUi()
        {
            var shouldShowAiButton = (_sourceAiPreviewEligible && IsAiPreviewEnabledFromCurrentOptions() && !_isAiCompareActive) || _isAiPreviewBusy;
            var sourceBitmap = ResolveAiCompareSourceBitmap();
            var hasOriginalView = sourceBitmap is not null;
            var hasEnhancedView = _aiPreviewBitmap is not null;
            var isSplitView = _isAiCompareActive && _aiPreviewViewMode == AiPreviewViewMode.Split && hasOriginalView && hasEnhancedView;

            AiPreviewButton.IsVisible = shouldShowAiButton;
            AiPreviewButton.IsEnabled = shouldShowAiButton && !_isAiPreviewBusy && !_isAiSaveBusy && !_isAiMattingBusy && !_isAiMattingCompareActive && !_isAiMattingSaveBusy;

            AiBusyOverlay.IsVisible = _isAiPreviewBusy || _isAiSaveBusy;
            AiCompareHost.IsVisible = isSplitView;
            AiCompareInputLayer.IsVisible = isSplitView;
            AiPreviewActionBar.IsVisible = _isAiCompareActive;
            AiPreviewOriginalButton.IsVisible = _isAiCompareActive;
            AiPreviewEnhancedButton.IsVisible = _isAiCompareActive;
            AiPreviewCompareButton.IsVisible = _isAiCompareActive;
            AiPreviewSaveButton.IsEnabled = _isAiCompareActive && !_isAiPreviewBusy && !_isAiSaveBusy && !string.IsNullOrWhiteSpace(_aiPreviewEnhancedPath);
            AiPreviewCloseButton.IsEnabled = _isAiCompareActive && !_isAiPreviewBusy && !_isAiSaveBusy;
            PreviewImage.IsVisible = !_isAiCompareActive || !isSplitView;

            ApplyAiPreviewModeButtonState(
                AiPreviewOriginalButton,
                _isAiCompareActive && _aiPreviewViewMode == AiPreviewViewMode.OriginalOnly,
                _isAiCompareActive && hasOriginalView && !_isAiPreviewBusy && !_isAiSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiPreviewEnhancedButton,
                _isAiCompareActive && _aiPreviewViewMode == AiPreviewViewMode.EnhancedOnly,
                _isAiCompareActive && hasEnhancedView && !_isAiPreviewBusy && !_isAiSaveBusy);
            ApplyAiPreviewModeButtonState(
                AiPreviewCompareButton,
                _isAiCompareActive && _aiPreviewViewMode == AiPreviewViewMode.Split,
                _isAiCompareActive && hasOriginalView && hasEnhancedView && !_isAiPreviewBusy && !_isAiSaveBusy);

            if (isSplitView)
            {
                AiCompareBeforeImage.Source = sourceBitmap;
                AiCompareAfterImage.Source = _aiPreviewBitmap;
                AiCompareInputLayer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                UpdateAiCompareLayout();
            }
            else if (_isAiCompareActive)
            {
                AiCompareBeforeImage.Source = sourceBitmap;
                AiCompareAfterImage.Source = _aiPreviewBitmap;
                PreviewImage.Source = ResolveAiSingleViewBitmap();
                AiCompareInputLayer.Cursor = null;
            }
            else
            {
                RestoreStandardPreviewSource();
                AiCompareInputLayer.Cursor = null;
            }
        }

        private void RefreshAiPreviewDiagnosticsText()
        {
            if (_aiPreviewDiagnostics is not { } diagnostics)
            {
                AiPreviewDiagnosticsText.Text = string.Empty;
                AiPreviewDiagnosticsText.IsVisible = false;
                return;
            }

            var sourceSize = string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostics.SourceSize.Width} x {diagnostics.SourceSize.Height}");
            var enhancedSize = string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostics.EnhancedSize.Width} x {diagnostics.EnhancedSize.Height}");
            AiPreviewDiagnosticsText.Text = FormatT(
                "PreviewAiDiagnosticsTemplate",
                sourceSize,
                enhancedSize,
                diagnostics.SampleSummary);
            AiPreviewDiagnosticsText.IsVisible = true;
        }

        private static void ApplyAiPreviewModeButtonState(Button button, bool isActive, bool isEnabled)
        {
            button.IsEnabled = isEnabled;

            if (isActive)
            {
                if (!button.Classes.Contains("primary"))
                {
                    button.Classes.Add("primary");
                }

                return;
            }

            button.Classes.Remove("primary");
        }

        private Bitmap? ResolveAiCompareSourceBitmap()
        {
            return _aiCompareSourceBitmap ?? _previewBitmap;
        }

        private Bitmap? ResolveAiSingleViewBitmap()
        {
            return _aiPreviewViewMode switch
            {
                AiPreviewViewMode.OriginalOnly => ResolveAiCompareSourceBitmap(),
                AiPreviewViewMode.EnhancedOnly => _aiPreviewBitmap,
                _ => null
            };
        }

        private void RestoreStandardPreviewSource()
        {
            if (_gifPreviewFrames is not null && _gifPreviewFrames.Count > 0)
            {
                var frameIndex = Math.Clamp(_gifPreviewIndex, 0, _gifPreviewFrames.Count - 1);
                PreviewImage.Source = _gifPreviewFrames[frameIndex];
                return;
            }

            PreviewImage.Source = _previewBitmap;
        }

        private bool IsAiPreviewEnabledFromCurrentOptions()
        {
            var options = _previewOptionsProvider?.Invoke();
            return options?.AiEnhancementEnabled == true;
        }

        private ConversionOptions BuildAiEnhancementOptions()
        {
            var options = _previewOptionsProvider?.Invoke() ?? new ConversionOptions();
            options.LanguageCode = string.IsNullOrWhiteSpace(options.LanguageCode)
                ? _localizationService.CurrentLanguageCode
                : options.LanguageCode;
            return options;
        }

        private ConversionOptions BuildAiPreviewSaveOptions()
        {
            var source = BuildAiEnhancementOptions();
            source.ResizeMode = ResizeMode.None;
            source.RenameMode = RenameMode.KeepOriginal;
            source.RenamePrefix = string.Empty;
            source.RenameSuffix = string.Empty;
            source.OutputDirectoryRule = OutputDirectoryRule.SpecificFolder;
            source.OutputDirectory = string.Empty;
            source.AllowOverwrite = true;
            source.GifHandlingMode = GifHandlingMode.FirstFrame;
            source.GifSpecificFrameIndex = 0;
            source.GifSpecificFrameSelections.Clear();
            source.GifFrameRanges.Clear();
            source.PdfImageExportMode = PdfImageExportMode.CurrentPage;
            source.PdfDocumentExportMode = PdfDocumentExportMode.CurrentPage;
            source.PdfPageIndex = 0;
            source.PdfPageSelections.Clear();
            source.PdfPageRanges.Clear();
            source.AiEnhancementEnabled = false;
            source.MaxDegreeOfParallelism = 1;
            return source;
        }

        private string BuildAiPreviewCacheKey(ConversionOptions options)
        {
            var scale = AiEnhancementModelCatalog.NormalizeRequestedOutputScale(options.AiEnhancementScale);
            var modelKey = ResolveAiPreviewCacheModelKey(options);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{_sourceFilePath}|{scale}|{options.AiEnhancementModel}|{modelKey}|{options.AiExecutionMode}");
        }

        private string ResolveAiPreviewCacheModelKey(ConversionOptions options)
        {
            var modelsDirectory = RuntimeAssetLocator.AiEnhancementModelsDirectory;
            if (AiEnhancementModelCatalog.TryResolveModelSelection(
                    modelsDirectory,
                    options.AiEnhancementModel,
                    options.AiEnhancementScale,
                    out var resolvedSelection))
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{resolvedSelection.EffectiveModel}|{resolvedSelection.ResolvedModel.ModelDirectoryPath}|{resolvedSelection.ResolvedModel.ModelNameArgument}");
            }

            return options.AiEnhancementModel.ToString();
        }

        private string? BuildAiModelFallbackNotice(ConversionOptions options)
        {
            var modelsDirectory = RuntimeAssetLocator.AiEnhancementModelsDirectory;
            if (!AiEnhancementModelCatalog.TryResolveModelSelection(
                    modelsDirectory,
                    options.AiEnhancementModel,
                    options.AiEnhancementScale,
                    out var resolvedSelection) ||
                !resolvedSelection.UsedDefaultFallback)
            {
                return null;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                T("AiModelFallbackToDefaultTemplate"),
                AiEnhancementModelCatalog.BuildListDisplayName(options.AiEnhancementModel, T),
                AiEnhancementModelCatalog.BuildListDisplayName(AiEnhancementModel.General, T));
        }

        private void CancelPendingAiPreview()
        {
            var cancellationSource = Interlocked.Exchange(ref _aiPreviewCts, null);
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
                _logger.LogDebug(nameof(ImagePreviewWindow), "Failed to cancel pending AI preview work cleanly.", ex);
            }

            cancellationSource.Dispose();
        }

        private void ReleaseAiPreviewCache()
        {
            _isAiCompareActive = false;
            _isAiCompareDragging = false;
            _aiCompareSplitRatio = 0.5d;
            _aiPreviewViewMode = AiPreviewViewMode.Split;
            _aiPreviewDiagnostics = null;
            _aiPreviewEnhancedPixelSize = default;
            AiCompareAfterClipHost.Clip = null;
            AiCompareBeforeImage.Source = null;
            AiCompareAfterImage.Source = null;

            _aiPreviewBitmap?.Dispose();
            _aiPreviewBitmap = null;

            var batchResult = _aiPreviewBatchResult;
            _aiPreviewBatchResult = null;
            _aiPreviewCacheKey = null;
            _aiPreviewEnhancedPath = null;

            if (batchResult is not null && !string.IsNullOrWhiteSpace(batchResult.WorkingDirectory))
            {
                TryDeleteAiPreviewWorkingDirectory(batchResult.WorkingDirectory);
            }

            RefreshAiPreviewUi();
        }

        private void TryDeleteAiPreviewWorkingDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(ImagePreviewWindow), $"Failed to delete temporary AI preview directory '{path}'.", ex);
            }
        }

        private void SetOwnerAiBusy(bool isBusy)
        {
            if (isBusy)
            {
                if (_isOwnerAiBusyRegistered)
                {
                    return;
                }

                _previewAiBusyChanged?.Invoke(true);
                _isOwnerAiBusyRegistered = true;
                return;
            }

            if (!_isOwnerAiBusyRegistered)
            {
                return;
            }

            _previewAiBusyChanged?.Invoke(false);
            _isOwnerAiBusyRegistered = false;
        }

        private async Task CloseRecognitionPanelForAiPreviewAsync()
        {
            CancelPendingOcr();
            _isOcrBusy = false;
            ClearRecognitionResultState();

            if (_isOcrPanelVisible)
            {
                UpdateOcrBusyUi();
                await SetOcrPanelVisibleAsync(false);
            }

            RefreshPreviewActionStates();
        }

        private void ShowAiPreviewComparison()
        {
            if (ResolveAiCompareSourceBitmap() is null || _aiPreviewBitmap is null)
            {
                return;
            }

            HideAiMattingComparison();
            _isAiCompareActive = true;
            _aiPreviewViewMode = AiPreviewViewMode.Split;
            _aiCompareSplitRatio = 0.5d;
            RefreshPreviewActionStates();
        }

        private void HideAiPreviewComparison()
        {
            _isAiCompareActive = false;
            _isAiCompareDragging = false;
            _aiPreviewViewMode = AiPreviewViewMode.Split;
            AiCompareAfterClipHost.Clip = null;
            RefreshPreviewActionStates();
        }

        private double GetAiCompareViewportWidth()
        {
            var width = PreviewSurface.Bounds.Width;
            if (width <= 0 && AiCompareHost.Bounds.Width > 0)
            {
                width = AiCompareHost.Bounds.Width;
            }

            if (width <= 0)
            {
                width = Math.Max(800d, Bounds.Width - 220d);
            }

            return width;
        }

        private double GetAiCompareRenderScaling()
        {
            return VisualRoot is TopLevel topLevel
                ? Math.Max(1d, topLevel.RenderScaling)
                : 1d;
        }

        private AiPreviewBitmapLoadResult? LoadAiCompareBitmap(string filePath)
        {
            return _aiPreviewComparisonService.TryLoadAsset(
                filePath,
                GetAiCompareViewportWidth(),
                GetAiCompareRenderScaling());
        }

        private async void OnAiPreviewClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiPreviewBusy || _isAiSaveBusy || _isAiMattingBusy || _isAiMattingCompareActive || _isAiMattingSaveBusy || string.IsNullOrWhiteSpace(_sourceFilePath))
            {
                return;
            }

            var options = BuildAiEnhancementOptions();
            if (!_sourceAiPreviewEligible || !options.AiEnhancementEnabled)
            {
                RefreshAiPreviewUi();
                return;
            }

            var cacheKey = BuildAiPreviewCacheKey(options);
            if (string.Equals(_aiPreviewCacheKey, cacheKey, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(_aiPreviewEnhancedPath) &&
                File.Exists(_aiPreviewEnhancedPath) &&
                _aiPreviewBitmap is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(ShowAiPreviewComparison);
                RefreshPreviewActionStates();
                return;
            }

            await CloseRecognitionPanelForAiPreviewAsync();
            ReleaseAiPreviewCache();

            var fallbackNotice = BuildAiModelFallbackNotice(options);
            if (!string.IsNullOrWhiteSpace(fallbackNotice))
            {
                ShowToast(fallbackNotice);
            }

            if (!ImageItemViewModel.TryCreate(_sourceFilePath, out var sourceItem, out var error, generateThumbnail: false) ||
                sourceItem is null)
            {
                ShowToast(string.IsNullOrWhiteSpace(error) ? T("PreviewAiFailed") : error);
                return;
            }

            var cancellationSource = new CancellationTokenSource();
            _aiPreviewCts = cancellationSource;
            _isAiPreviewBusy = true;
            SetOwnerAiBusy(true);
            RefreshAiPreviewText();
            RefreshPreviewActionStates();

            try
            {
                var batchResult = await _aiImageEnhancementService
                    .EnhanceAsync([sourceItem], options, progress: null, cancellationSource.Token)
                    .ConfigureAwait(false);

                if (_isClosed || !ReferenceEquals(_aiPreviewCts, cancellationSource))
                {
                    if (!string.IsNullOrWhiteSpace(batchResult.WorkingDirectory))
                    {
                        TryDeleteAiPreviewWorkingDirectory(batchResult.WorkingDirectory);
                    }

                    return;
                }

                if (!batchResult.InputOverrides.TryGetValue(_sourceFilePath, out var enhancedPath) ||
                    string.IsNullOrWhiteSpace(enhancedPath) ||
                    !File.Exists(enhancedPath))
                {
                    if (!string.IsNullOrWhiteSpace(batchResult.WorkingDirectory))
                    {
                        TryDeleteAiPreviewWorkingDirectory(batchResult.WorkingDirectory);
                    }

                    ShowToast(T("PreviewAiFailed"));
                    return;
                }

                var sourceBitmapTask = _aiCompareSourceBitmap is null
                    ? Task.Run(() => LoadAiCompareBitmap(_sourceFilePath!), cancellationSource.Token)
                    : Task.FromResult<AiPreviewBitmapLoadResult?>(null);
                var enhancedBitmapTask = Task.Run(() => LoadAiCompareBitmap(enhancedPath), cancellationSource.Token);

                await Task.WhenAll(sourceBitmapTask, enhancedBitmapTask).ConfigureAwait(false);

                var sourceBitmapResult = await sourceBitmapTask.ConfigureAwait(false);
                var enhancedBitmapResult = await enhancedBitmapTask.ConfigureAwait(false);

                if (enhancedBitmapResult?.Bitmap is null)
                {
                    sourceBitmapResult?.Dispose();

                    if (!string.IsNullOrWhiteSpace(batchResult.WorkingDirectory))
                    {
                        TryDeleteAiPreviewWorkingDirectory(batchResult.WorkingDirectory);
                    }

                    ShowToast(T("PreviewAiFailed"));
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (sourceBitmapResult is not null)
                    {
                        _aiCompareSourceBitmap?.Dispose();
                        _aiCompareSourceBitmap = sourceBitmapResult.Bitmap;
                        _aiCompareSourcePixelSize = sourceBitmapResult.SourceSize;
                    }

                    _aiPreviewBitmap?.Dispose();
                    _aiPreviewBatchResult = batchResult;
                    _aiPreviewCacheKey = cacheKey;
                    _aiPreviewEnhancedPath = enhancedPath;
                    _aiPreviewBitmap = enhancedBitmapResult.Bitmap;
                    _aiPreviewEnhancedPixelSize = enhancedBitmapResult.SourceSize;
                    _aiPreviewDiagnostics = _aiCompareSourceBitmap is not null &&
                                            AiPreviewComparisonService.TryBuildDiagnostics(
                                                _aiCompareSourceBitmap,
                                                _aiCompareSourcePixelSize,
                                                _aiPreviewBitmap!,
                                                _aiPreviewEnhancedPixelSize,
                                                out var diagnostics)
                        ? diagnostics
                        : null;
                    ShowAiPreviewComparison();
                    RefreshAiPreviewText();
                    RefreshPreviewActionStates();
                });
            }
            catch (OperationCanceledException)
            {
                // Ignore canceled preview enhancement requests.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "AI preview generation failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                sourceItem.Dispose();

                if (ReferenceEquals(Interlocked.CompareExchange(ref _aiPreviewCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }

                _isAiPreviewBusy = false;
                SetOwnerAiBusy(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiPreviewText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private async void OnSaveAiPreviewClick(object? sender, RoutedEventArgs e)
        {
            if (_isAiSaveBusy ||
                _isAiPreviewBusy ||
                string.IsNullOrWhiteSpace(_sourceFilePath) ||
                string.IsNullOrWhiteSpace(_aiPreviewEnhancedPath) ||
                !File.Exists(_aiPreviewEnhancedPath))
            {
                return;
            }

            var options = BuildAiPreviewSaveOptions();
            var extension = ImageConversionService.GetFileExtension(options.OutputFormat);
            var suggestedName = $"{Path.GetFileNameWithoutExtension(_sourceFilePath)}_ai";
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

            _isAiSaveBusy = true;
            RefreshAiPreviewText();
            RefreshPreviewActionStates();

            try
            {
                var destinationPath = EnsureOutputExtension(localPath, extension);
                await _imageConversionService.ExportRasterToPathAsync(_aiPreviewEnhancedPath, destinationPath, options).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(T("PreviewAiSaved")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(ImagePreviewWindow), "Saving the AI preview output failed.", ex);
                await Dispatcher.UIThread.InvokeAsync(() => ShowToast(ex.Message));
            }
            finally
            {
                _isAiSaveBusy = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshAiPreviewText();
                    RefreshPreviewActionStates();
                });
            }
        }

        private static string EnsureOutputExtension(string path, string extension)
        {
            if (string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return Path.ChangeExtension(path, extension);
        }

        private void OnShowAiOriginalClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiCompareActive || ResolveAiCompareSourceBitmap() is null)
            {
                return;
            }

            _aiPreviewViewMode = AiPreviewViewMode.OriginalOnly;
            RefreshAiPreviewUi();
        }

        private void OnShowAiEnhancedClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiCompareActive || _aiPreviewBitmap is null)
            {
                return;
            }

            _aiPreviewViewMode = AiPreviewViewMode.EnhancedOnly;
            RefreshAiPreviewUi();
        }

        private void OnShowAiCompareClick(object? sender, RoutedEventArgs e)
        {
            if (!_isAiCompareActive || ResolveAiCompareSourceBitmap() is null || _aiPreviewBitmap is null)
            {
                return;
            }

            _aiPreviewViewMode = AiPreviewViewMode.Split;
            RefreshAiPreviewUi();
        }

        private void OnCloseAiPreviewClick(object? sender, RoutedEventArgs e)
        {
            HideAiPreviewComparison();
        }

        private void OnAiComparePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isAiCompareActive ||
                _aiPreviewViewMode != AiPreviewViewMode.Split ||
                !e.GetCurrentPoint(AiCompareInputLayer).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _isAiCompareDragging = true;
            e.Pointer.Capture(AiCompareInputLayer);
            UpdateAiCompareSplitFromPoint(e.GetPosition(AiCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiComparePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isAiCompareDragging || !_isAiCompareActive || _aiPreviewViewMode != AiPreviewViewMode.Split)
            {
                return;
            }

            UpdateAiCompareSplitFromPoint(e.GetPosition(AiCompareInputLayer));
            e.Handled = true;
        }

        private void OnAiComparePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isAiCompareDragging)
            {
                return;
            }

            _isAiCompareDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void OnAiComparePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isAiCompareDragging = false;
        }

        private void UpdateAiCompareSplitFromPoint(Point point)
        {
            var contentRect = GetAiCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return;
            }

            var clampedX = Math.Clamp(point.X, contentRect.X, contentRect.Right);
            _aiCompareSplitRatio = (clampedX - contentRect.X) / contentRect.Width;
            UpdateAiCompareLayout();
        }

        private void UpdateAiCompareLayout()
        {
            if (!_isAiCompareActive || _aiPreviewViewMode != AiPreviewViewMode.Split || _aiPreviewBitmap is null)
            {
                return;
            }

            var contentRect = GetAiCompareContentRect();
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return;
            }

            var splitX = contentRect.X + (contentRect.Width * Math.Clamp(_aiCompareSplitRatio, 0d, 1d));
            var lineWidth = double.IsNaN(AiCompareSplitterLine.Width) || AiCompareSplitterLine.Width <= 0
                ? 2d
                : AiCompareSplitterLine.Width;
            var thumbWidth = double.IsNaN(AiCompareThumb.Width) || AiCompareThumb.Width <= 0
                ? 22d
                : AiCompareThumb.Width;
            var thumbHeight = double.IsNaN(AiCompareThumb.Height) || AiCompareThumb.Height <= 0
                ? 22d
                : AiCompareThumb.Height;

            AiCompareAfterClipHost.Clip = new RectangleGeometry(new Rect(
                splitX,
                contentRect.Y,
                Math.Max(0d, contentRect.Right - splitX),
                contentRect.Height));

            AiCompareSplitterLine.Height = contentRect.Height;
            Canvas.SetLeft(AiCompareSplitterLine, splitX - (lineWidth / 2d));
            Canvas.SetTop(AiCompareSplitterLine, contentRect.Y);
            Canvas.SetLeft(AiCompareThumb, splitX - (thumbWidth / 2d));
            Canvas.SetTop(AiCompareThumb, contentRect.Center.Y - (thumbHeight / 2d));
        }

        private Rect GetAiCompareContentRect()
        {
            var bounds = AiCompareHost.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0 || _aiPreviewBitmap is null)
            {
                return default;
            }

            var pixelSize = _aiPreviewBitmap.PixelSize;
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
    }
}





