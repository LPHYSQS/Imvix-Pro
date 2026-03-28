using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    internal static class ImageLoader
    {
        private const int GifPreviewCacheLimit = 6;
        private static readonly object GifPreviewCacheGate = new();
        private static readonly Dictionary<string, GifPreviewCacheEntry> GifPreviewCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<GifPreviewCacheEntry?>> GifPreviewLoads = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim GifPreviewDecodeGate = new(2, 2);

        internal static bool TryGetCachedGifPreview(string filePath, int maxWidth, [NotNullWhen(true)] out ImageConversionService.GifPreviewHandle? handle)
        {
            handle = null;
            if (!IsGifFile(filePath))
            {
                return false;
            }

            var cacheKey = BuildGifPreviewCacheKey(filePath, maxWidth);
            lock (GifPreviewCacheGate)
            {
                if (!GifPreviewCache.TryGetValue(cacheKey, out var entry))
                {
                    return false;
                }

                entry.RefCount++;
                entry.LastAccessUtc = DateTime.UtcNow;
                handle = new ImageConversionService.GifPreviewHandle(cacheKey, entry);
                return true;
            }
        }

        internal static Task<ImageConversionService.GifPreviewHandle?> GetOrLoadGifPreviewAsync(string filePath, int maxWidth)
        {
            if (!IsGifFile(filePath))
            {
                return Task.FromResult<ImageConversionService.GifPreviewHandle?>(null);
            }

            if (TryGetCachedGifPreview(filePath, maxWidth, out var cachedHandle))
            {
                return Task.FromResult<ImageConversionService.GifPreviewHandle?>(cachedHandle);
            }

            return GetOrLoadGifPreviewCoreAsync(filePath, maxWidth);
        }

        internal static void WarmGifPreview(string filePath, int maxWidth)
        {
            if (!IsGifFile(filePath))
            {
                return;
            }

            var cacheKey = BuildGifPreviewCacheKey(filePath, maxWidth);
            lock (GifPreviewCacheGate)
            {
                if (GifPreviewCache.ContainsKey(cacheKey) || GifPreviewLoads.ContainsKey(cacheKey))
                {
                    return;
                }
            }

            var loader = GetOrCreateGifPreviewLoader(cacheKey, filePath, maxWidth);
            _ = loader.ContinueWith(task =>
            {
                if (task.Status != TaskStatus.RanToCompletion || task.Result is null)
                {
                    lock (GifPreviewCacheGate)
                    {
                        GifPreviewLoads.Remove(cacheKey);
                    }

                    return;
                }

                lock (GifPreviewCacheGate)
                {
                    GifPreviewLoads.Remove(cacheKey);
                    if (!GifPreviewCache.ContainsKey(cacheKey))
                    {
                        GifPreviewCache[cacheKey] = task.Result;
                    }

                    task.Result.LastAccessUtc = DateTime.UtcNow;
                    TrimGifPreviewCache();
                }
            }, TaskScheduler.Default);
        }

        internal static Bitmap? TryCreatePreview(string filePath, int maxWidth, bool svgUseBackground = false, string? svgBackgroundColor = null)
        {
            try
            {
                var extension = Path.GetExtension(filePath);
                if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return AppServices.PdfRenderService.TryCreatePreview(filePath, 0, maxWidth);
                }

                if (Services.PsdModule.PsdImportService.IsPsdFile(filePath))
                {
                    return AppServices.PsdRenderService.TryCreatePreview(filePath, maxWidth, svgUseBackground, svgBackgroundColor);
                }

                if (svgUseBackground ||
                    extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
                    ExecutableIconService.IsExecutableIconSource(filePath) ||
                    ShortcutIconService.IsShortcutIconSource(filePath))
                {
                    using var renderedBitmap = ImageSaver.DecodeToBitmap(filePath, svgUseBackground, svgBackgroundColor);
                    return renderedBitmap is null ? null : ImageConversionService.CreatePreviewFromBitmap(renderedBitmap, maxWidth);
                }

                using var stream = File.OpenRead(filePath);
                return Bitmap.DecodeToWidth(stream, maxWidth);
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(ImageLoader), $"Primary preview decode failed for '{filePath}'. Falling back to a slower preview path.", ex);
                try
                {
                    if (Services.PsdModule.PsdImportService.IsPsdFile(filePath))
                    {
                        return AppServices.PsdRenderService.TryCreatePreview(filePath, maxWidth, svgUseBackground, svgBackgroundColor);
                    }

                    using var fallbackBitmap = ImageSaver.DecodeToBitmap(filePath, svgUseBackground, svgBackgroundColor);
                    return fallbackBitmap is null ? null : ImageConversionService.CreatePreviewFromBitmap(fallbackBitmap, maxWidth);
                }
                catch (Exception fallbackEx)
                {
                    AppServices.Logger.LogDebug(nameof(ImageLoader), $"Fallback preview decode also failed for '{filePath}'.", fallbackEx);
                    return null;
                }
            }
        }

        internal static void ReleaseGifPreview(string cacheKey, GifPreviewCacheEntry entry)
        {
            lock (GifPreviewCacheGate)
            {
                entry.RefCount = Math.Max(0, entry.RefCount - 1);
                entry.LastAccessUtc = DateTime.UtcNow;
                TrimGifPreviewCache();
            }
        }

        private static bool TryLoadGifPreviewFrames(
            string filePath,
            int maxWidth,
            out List<Bitmap> frames,
            out List<TimeSpan> durations)
        {
            frames = [];
            durations = [];

            try
            {
                if (!Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                if (codec is null)
                {
                    return false;
                }

                var frameInfos = codec.FrameInfo;
                if (frameInfos.Length <= 1)
                {
                    return false;
                }

                var info = codec.Info;
                for (var i = 0; i < frameInfos.Length; i++)
                {
                    using var frameBitmap = new SKBitmap(info);
                    var decodeOptions = new SKCodecOptions(i)
                    {
                        PriorFrame = -1
                    };

                    var result = codec.GetPixels(info, frameBitmap.GetPixels(), decodeOptions);
                    if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                    {
                        ImageConversionService.DisposePreviewFrames(frames);
                        frames.Clear();
                        durations.Clear();
                        return false;
                    }

                    using var image = SKImage.FromBitmap(frameBitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    if (data is null)
                    {
                        ImageConversionService.DisposePreviewFrames(frames);
                        frames.Clear();
                        durations.Clear();
                        return false;
                    }

                    using var memory = new MemoryStream(data.ToArray());
                    frames.Add(Bitmap.DecodeToWidth(memory, maxWidth));

                    var duration = Math.Max(0, frameInfos[i].Duration);
                    durations.Add(TimeSpan.FromMilliseconds(duration));
                }

                return frames.Count > 0;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(ImageLoader), $"Failed to decode GIF preview frames for '{filePath}'.", ex);
                ImageConversionService.DisposePreviewFrames(frames);
                frames.Clear();
                durations.Clear();
                return false;
            }
        }

        private static async Task<ImageConversionService.GifPreviewHandle?> GetOrLoadGifPreviewCoreAsync(string filePath, int maxWidth)
        {
            var cacheKey = BuildGifPreviewCacheKey(filePath, maxWidth);
            var loader = GetOrCreateGifPreviewLoader(cacheKey, filePath, maxWidth);
            GifPreviewCacheEntry? entry = null;
            try
            {
                entry = await loader.ConfigureAwait(false);
            }
            finally
            {
                lock (GifPreviewCacheGate)
                {
                    GifPreviewLoads.Remove(cacheKey);
                }
            }

            if (entry is null)
            {
                return null;
            }

            lock (GifPreviewCacheGate)
            {
                if (!GifPreviewCache.TryGetValue(cacheKey, out var cached))
                {
                    cached = entry;
                    GifPreviewCache[cacheKey] = cached;
                }

                cached.RefCount++;
                cached.LastAccessUtc = DateTime.UtcNow;
                TrimGifPreviewCache();
                return new ImageConversionService.GifPreviewHandle(cacheKey, cached);
            }
        }

        private static Task<GifPreviewCacheEntry?> GetOrCreateGifPreviewLoader(string cacheKey, string filePath, int maxWidth)
        {
            lock (GifPreviewCacheGate)
            {
                if (GifPreviewLoads.TryGetValue(cacheKey, out var existing))
                {
                    return existing;
                }

                var loader = Task.Run(async () =>
                {
                    await GifPreviewDecodeGate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        return LoadGifPreviewEntry(filePath, maxWidth);
                    }
                    finally
                    {
                        GifPreviewDecodeGate.Release();
                    }
                });

                GifPreviewLoads[cacheKey] = loader;
                return loader;
            }
        }

        private static GifPreviewCacheEntry? LoadGifPreviewEntry(string filePath, int maxWidth)
        {
            var success = TryLoadGifPreviewFrames(filePath, maxWidth, out var frames, out var durations);
            if (!success || frames.Count == 0 || frames.Count != durations.Count)
            {
                ImageConversionService.DisposePreviewFrames(frames);
                return null;
            }

            return new GifPreviewCacheEntry(frames, durations);
        }

        private static void TrimGifPreviewCache()
        {
            if (GifPreviewCache.Count <= GifPreviewCacheLimit)
            {
                return;
            }

            var candidates = GifPreviewCache
                .Where(static kvp => kvp.Value.RefCount == 0)
                .OrderBy(static kvp => kvp.Value.LastAccessUtc)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (GifPreviewCache.Count <= GifPreviewCacheLimit)
                {
                    break;
                }

                GifPreviewCache.Remove(candidate.Key);
                candidate.Value.Dispose();
            }
        }

        private static string BuildGifPreviewCacheKey(string filePath, int maxWidth)
        {
            return $"{filePath}|{maxWidth}";
        }

        private static bool IsGifFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class GifPreviewCacheEntry
        {
            public GifPreviewCacheEntry(List<Bitmap> frames, List<TimeSpan> durations)
            {
                Frames = frames;
                Durations = durations;
            }

            public List<Bitmap> Frames { get; }

            public List<TimeSpan> Durations { get; }

            public int RefCount { get; set; }

            public DateTime LastAccessUtc { get; set; }

            public void Dispose()
            {
                ImageConversionService.DisposePreviewFrames(Frames);
            }
        }
    }
}
