using Avalonia.Media.Imaging;
using ImageMagick;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImvixPro.Services.PsdModule
{
    public sealed class PsdRenderService
    {
        private const int CachedDocumentLimit = 2;
        private static readonly object CacheGate = new();
        private static readonly Dictionary<string, CachedDocument> DocumentCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsPsdFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".psd", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryReadDocumentInfo(string filePath, out PsdDocumentInfo? info, out string? error)
        {
            info = null;
            error = null;

            if (!File.Exists(filePath))
            {
                error = "File not found.";
                return false;
            }

            try
            {
                if (!TryRentDocument(filePath, out var lease, out error) || lease is null)
                {
                    return false;
                }

                using (lease)
                {
                    info = lease.Document.Info;
                }

                return true;
            }
            catch (MagickException)
            {
                error = PsdImportService.UnsupportedPsdErrorCode;
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        public SKBitmap? TryCreateBitmap(string filePath, bool useBackground, string? backgroundColor, out string? error)
        {
            error = null;

            if (!File.Exists(filePath))
            {
                error = "File not found.";
                return null;
            }

            try
            {
                if (!TryRentDocument(filePath, out var lease, out error) || lease is null)
                {
                    return null;
                }

                using (lease)
                {
                    return CloneBitmapWithOptionalBackground(lease.Document.Bitmap, useBackground, backgroundColor);
                }
            }
            catch (MagickException)
            {
                error = PsdImportService.UnsupportedPsdErrorCode;
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
                return null;
            }
        }

        public Bitmap? TryCreatePreview(string filePath, int maxWidth, bool useBackground, string? backgroundColor)
        {
            if (!TryRentDocument(filePath, out var lease, out _) || lease is null)
            {
                return null;
            }

            using (lease)
            {
                return CreatePreviewFromBitmap(lease.Document.Bitmap, maxWidth, useBackground, backgroundColor);
            }
        }

        private static SKBitmap? DecodeMergedBitmap(MagickImage image)
        {
            using var prepared = image.Clone();
            prepared.ColorSpace = ColorSpace.sRGB;
            prepared.Format = MagickFormat.Png32;

            var bytes = prepared.ToByteArray();
            if (bytes.Length == 0)
            {
                return null;
            }

            return SKBitmap.Decode(bytes);
        }

        private static string ResolveColorMode(MagickImage image)
        {
            var colorType = image.ColorType.ToString();
            return colorType switch
            {
                "TrueColor" => "RGB",
                "TrueColorAlpha" => "RGBA",
                "Palette" => "Indexed",
                "PaletteAlpha" => "Indexed + Alpha",
                "Grayscale" => "Grayscale",
                "GrayscaleAlpha" => "Grayscale + Alpha",
                "ColorSeparation" => "CMYK",
                "ColorSeparationAlpha" => "CMYK + Alpha",
                _ => colorType
            };
        }

        private static bool TryRentDocument(string filePath, out PsdDocumentLease? lease, out string? error)
        {
            return TryRentDocument(filePath, out lease, out _, out error);
        }

        private static bool TryRentDocument(string filePath, out PsdDocumentLease? lease, out string? cacheKey, out string? error)
        {
            lease = null;
            cacheKey = null;
            error = null;

            if (!TryCreateCacheKey(filePath, out var resolvedCacheKey, out error) || resolvedCacheKey is null)
            {
                return false;
            }

            cacheKey = resolvedCacheKey;

            lock (CacheGate)
            {
                if (DocumentCache.TryGetValue(resolvedCacheKey, out var cached))
                {
                    cached.RefCount++;
                    cached.LastAccessUtc = DateTime.UtcNow;
                    lease = new PsdDocumentLease(resolvedCacheKey, cached);
                    return true;
                }
            }

            CachedDocument? loaded;
            try
            {
                loaded = LoadCachedDocument(filePath);
                if (loaded is null)
                {
                    error = PsdImportService.UnsupportedPsdErrorCode;
                    return false;
                }
            }
            catch (MagickException)
            {
                error = PsdImportService.UnsupportedPsdErrorCode;
                return false;
            }

            CachedDocument? staleDuplicate = null;

            lock (CacheGate)
            {
                if (DocumentCache.TryGetValue(resolvedCacheKey, out var existing))
                {
                    existing.RefCount++;
                    existing.LastAccessUtc = DateTime.UtcNow;
                    staleDuplicate = loaded;
                    lease = new PsdDocumentLease(resolvedCacheKey, existing);
                }
                else
                {
                    loaded.RefCount = 1;
                    loaded.LastAccessUtc = DateTime.UtcNow;
                    DocumentCache[resolvedCacheKey] = loaded;
                    lease = new PsdDocumentLease(resolvedCacheKey, loaded);
                    TrimCacheIfNeeded();
                }
            }

            staleDuplicate?.Dispose();
            return true;
        }

        private static CachedDocument? LoadCachedDocument(string filePath)
        {
            using var image = new MagickImage(filePath);
            using var bitmap = DecodeMergedBitmap(image);
            if (bitmap is null)
            {
                return null;
            }

            var info = new PsdDocumentInfo(
                checked((int)Math.Max(1U, image.Width)),
                checked((int)Math.Max(1U, image.Height)),
                image.HasAlpha,
                image.Depth > 0 ? checked((int)image.Depth) : (int?)null,
                ResolveColorMode(image));

            return new CachedDocument(info, bitmap.Copy());
        }

        private static bool TryCreateCacheKey(string filePath, out string? cacheKey, out string? error)
        {
            cacheKey = null;
            error = null;

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    error = "File not found.";
                    return false;
                }

                cacheKey = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}");
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void ReleaseLease(string cacheKey)
        {
            List<CachedDocument>? releasedEntries = null;

            lock (CacheGate)
            {
                if (DocumentCache.TryGetValue(cacheKey, out var cached))
                {
                    cached.RefCount = Math.Max(0, cached.RefCount - 1);
                    cached.LastAccessUtc = DateTime.UtcNow;
                }

                if (DocumentCache.Count > CachedDocumentLimit)
                {
                    var evictedKeys = DocumentCache
                        .Where(static pair => pair.Value.RefCount == 0)
                        .OrderBy(static pair => pair.Value.LastAccessUtc)
                        .Take(DocumentCache.Count - CachedDocumentLimit)
                        .Select(static pair => pair.Key)
                        .ToArray();

                    if (evictedKeys.Length > 0)
                    {
                        releasedEntries = [];
                        foreach (var key in evictedKeys)
                        {
                            if (DocumentCache.Remove(key, out var removed))
                            {
                                releasedEntries.Add(removed);
                            }
                        }
                    }
                }
            }

            if (releasedEntries is null)
            {
                return;
            }

            foreach (var entry in releasedEntries)
            {
                entry.Dispose();
            }
        }

        private static void TrimCacheIfNeeded()
        {
            if (DocumentCache.Count <= CachedDocumentLimit)
            {
                return;
            }

            var evictedEntries = new List<CachedDocument>();
            var evictedKeys = DocumentCache
                .Where(static pair => pair.Value.RefCount == 0)
                .OrderBy(static pair => pair.Value.LastAccessUtc)
                .Take(DocumentCache.Count - CachedDocumentLimit)
                .Select(static pair => pair.Key)
                .ToArray();

            foreach (var key in evictedKeys)
            {
                if (DocumentCache.Remove(key, out var removed))
                {
                    evictedEntries.Add(removed);
                }
            }

            foreach (var entry in evictedEntries)
            {
                entry.Dispose();
            }
        }

        private static SKBitmap CloneBitmapWithOptionalBackground(SKBitmap sourceBitmap, bool useBackground, string? backgroundColor)
        {
            var info = new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(useBackground ? ParseBackgroundColor(backgroundColor) : SKColors.Transparent);
            canvas.DrawBitmap(sourceBitmap, 0, 0);
            canvas.Flush();
            return bitmap;
        }

        private static SKColor ParseBackgroundColor(string? backgroundColor)
        {
            if (!string.IsNullOrWhiteSpace(backgroundColor) && SKColor.TryParse(backgroundColor, out var parsed))
            {
                return parsed;
            }

            return SKColors.White;
        }

        private static Bitmap? CreatePreviewFromBitmap(SKBitmap sourceBitmap, int maxWidth, bool useBackground, string? backgroundColor)
        {
            var targetWidth = Math.Max(1, Math.Min(maxWidth, sourceBitmap.Width));
            var scale = targetWidth / (double)Math.Max(1, sourceBitmap.Width);
            var targetHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));
            var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var previewBitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(previewBitmap);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.Clear(useBackground ? ParseBackgroundColor(backgroundColor) : SKColors.Transparent);
            canvas.DrawBitmap(sourceBitmap, SKRect.Create(targetWidth, targetHeight), paint);
            canvas.Flush();

            using var image = SKImage.FromBitmap(previewBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
            {
                return null;
            }

            using var memory = new MemoryStream(data.ToArray());
            return Bitmap.DecodeToWidth(memory, targetWidth);
        }

        private sealed class CachedDocument : IDisposable
        {
            public CachedDocument(PsdDocumentInfo info, SKBitmap bitmap)
            {
                Info = info;
                Bitmap = bitmap;
            }

            public PsdDocumentInfo Info { get; }

            public SKBitmap Bitmap { get; }

            public int RefCount { get; set; }

            public DateTime LastAccessUtc { get; set; }

            public void Dispose()
            {
                Bitmap.Dispose();
            }
        }

        private sealed class PsdDocumentLease : IDisposable
        {
            private readonly string _cacheKey;
            private bool _isDisposed;

            public PsdDocumentLease(string cacheKey, CachedDocument document)
            {
                _cacheKey = cacheKey;
                Document = document;
            }

            public CachedDocument Document { get; }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                ReleaseLease(_cacheKey);
            }
        }
    }
}
