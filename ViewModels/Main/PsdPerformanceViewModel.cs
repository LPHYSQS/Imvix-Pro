using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PsdModule;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private const int SelectedPsdPreviewWidth = 760;
        private const int SelectedPsdPreviewThrottleMilliseconds = 45;

        private readonly SemaphoreSlim _addFilesGate = new(1, 1);
        private CancellationTokenSource? _selectedPsdPreviewCts;
        private long _selectedPsdPreviewRequestId;
        private long _imageImportVersion;

        private readonly record struct InputItemImportResult(ImageItemViewModel? Item, string? Error)
        {
            public void Dispose()
            {
                Item?.Dispose();
            }
        }

        private void QueueAddFiles(IReadOnlyList<string> candidates)
        {
            _ = AddFilesAsync(candidates, Volatile.Read(ref _imageImportVersion));
        }

        private async Task AddFilesAsync(IReadOnlyList<string> candidates, long importVersion)
        {
            await _addFilesGate.WaitAsync().ConfigureAwait(false);

            try
            {
                try
                {
                    foreach (var path in candidates)
                    {
                        if (!ImageConversionService.SupportedInputExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var shouldSkip = await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (importVersion != Volatile.Read(ref _imageImportVersion))
                            {
                                return true;
                            }

                            return Images.Any(x => x.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
                        });

                        if (shouldSkip)
                        {
                            if (importVersion != Volatile.Read(ref _imageImportVersion))
                            {
                                break;
                            }

                            continue;
                        }

                        var importResult = await Task.Run(() => CreateInputItemImportResult(path)).ConfigureAwait(false);

                        var shouldStop = false;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (importVersion != Volatile.Read(ref _imageImportVersion))
                            {
                                importResult.Dispose();
                                shouldStop = true;
                                return;
                            }

                            if (Images.Any(x => x.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                            {
                                importResult.Dispose();
                                return;
                            }

                            if (importResult.Item is not null)
                            {
                                UpdateGifLabels(importResult.Item);
                                UpdatePdfSecurityPresentation(importResult.Item);
                                Images.Add(importResult.Item);
                                WarmGifPreviewIfNeeded(importResult.Item);

                                if (SelectedImage is null)
                                {
                                    SelectedImage = importResult.Item;
                                }

                                return;
                            }

                            FailedConversions.Add(new ConversionFailure(Path.GetFileName(path), TranslateInputError(importResult.Error)));
                        });

                        if (shouldStop)
                        {
                            break;
                        }
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (importVersion != Volatile.Read(ref _imageImportVersion))
                        {
                            return;
                        }

                        if (SelectedImage is null && Images.Count > 0)
                        {
                            SelectedImage = Images[0];
                        }

                        SetStatus("StatusReady");
                        RefreshConversionInsights();
                    });
                }
                catch
                {
                    await Dispatcher.UIThread.InvokeAsync(() => SetStatus("StatusReady"));
                }
            }
            finally
            {
                _addFilesGate.Release();
            }
        }

        private InputItemImportResult CreateInputItemImportResult(string path)
        {
            return TryCreateInputItem(path, out var item, out var error) && item is not null
                ? new InputItemImportResult(item, null)
                : new InputItemImportResult(null, error);
        }

        private void InvalidatePendingImports()
        {
            Interlocked.Increment(ref _imageImportVersion);
        }

        private bool ShouldLoadSelectedPsdPreviewAsync(ImageItemViewModel? image)
        {
            return image is not null &&
                   !image.IsPdfDocument &&
                   !image.IsAnimatedGif &&
                   PsdImportService.IsPsdFile(image.FilePath);
        }

        private void CancelPendingSelectedPsdPreviewRender()
        {
            var cancellationSource = Interlocked.Exchange(ref _selectedPsdPreviewCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch
            {
                // Ignore races while replacing preview work.
            }

            cancellationSource.Dispose();
        }

        private void RefreshSelectedPsdPreviewAsync(bool preferImmediatePreview, bool useThumbnailPlaceholder)
        {
            CancelPendingSelectedPsdPreviewRender();

            if (!ShouldLoadSelectedPsdPreviewAsync(SelectedImage) || SelectedImage is null)
            {
                return;
            }

            var filePath = SelectedImage.FilePath;
            var renderSettings = GetPreviewRenderSettings(filePath);

            if (useThumbnailPlaceholder && SelectedImage.Thumbnail is not null)
            {
                var placeholder = CloneBitmap(SelectedImage.Thumbnail);
                if (placeholder is not null)
                {
                    SelectedPreview?.Dispose();
                    SelectedPreview = placeholder;
                }
            }

            var cancellationSource = new CancellationTokenSource();
            _selectedPsdPreviewCts = cancellationSource;
            var requestId = Interlocked.Increment(ref _selectedPsdPreviewRequestId);

            _ = LoadSelectedPsdPreviewAsync(
                filePath,
                renderSettings.UseBackground,
                renderSettings.BackgroundColor,
                requestId,
                cancellationSource,
                preferImmediatePreview);
        }

        private async Task LoadSelectedPsdPreviewAsync(
            string filePath,
            bool useBackground,
            string backgroundColor,
            long requestId,
            CancellationTokenSource cancellationSource,
            bool preferImmediatePreview)
        {
            Bitmap? preview = null;

            try
            {
                if (!preferImmediatePreview)
                {
                    await Task.Delay(SelectedPsdPreviewThrottleMilliseconds, cancellationSource.Token).ConfigureAwait(false);
                }

                cancellationSource.Token.ThrowIfCancellationRequested();
                preview = await Task.Run(
                        () => ImageConversionService.TryCreatePreview(filePath, SelectedPsdPreviewWidth, useBackground, backgroundColor),
                        cancellationSource.Token)
                    .ConfigureAwait(false);

                if (preview is null)
                {
                    return;
                }

                if (!await TryApplySelectedPsdPreviewAsync(filePath, requestId, preview).ConfigureAwait(false))
                {
                    return;
                }

                preview = null;
            }
            catch (OperationCanceledException)
            {
                // Ignore stale preview requests when the user switches files or keeps dragging the color picker.
            }
            finally
            {
                preview?.Dispose();

                if (ReferenceEquals(Interlocked.CompareExchange(ref _selectedPsdPreviewCts, null, cancellationSource), cancellationSource))
                {
                    cancellationSource.Dispose();
                }
            }
        }

        private async Task<bool> TryApplySelectedPsdPreviewAsync(string filePath, long requestId, Bitmap preview)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (requestId != _selectedPsdPreviewRequestId ||
                    !ShouldLoadSelectedPsdPreviewAsync(SelectedImage) ||
                    SelectedImage is null ||
                    !SelectedImage.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                SelectedPreview?.Dispose();
                SelectedPreview = preview;
                return true;
            });
        }

        private static Bitmap? CloneBitmap(Bitmap source)
        {
            try
            {
                using var stream = new MemoryStream();
                source.Save(stream);
                stream.Position = 0;
                return Bitmap.DecodeToWidth(stream, Math.Max(1, source.PixelSize.Width));
            }
            catch
            {
                return null;
            }
        }
    }
}
