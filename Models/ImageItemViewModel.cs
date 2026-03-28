using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Services;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace ImvixPro.Models
{
    public sealed partial class ImageItemViewModel : ObservableObject, IDisposable
    {
        private ImageItemViewModel(
            string filePath,
            long fileSize,
            int pixelWidth,
            int pixelHeight,
            Bitmap? thumbnail,
            int gifFrameCount,
            int pdfPageCount,
            bool isPdfDocument,
            bool isEncrypted,
            bool isUnlocked,
            string passwordCache)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Extension = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            FileSizeBytes = Math.Max(0, fileSize);
            SizeText = FormatSize(FileSizeBytes);
            PixelWidth = Math.Max(0, pixelWidth);
            PixelHeight = Math.Max(0, pixelHeight);
            GifFrameCount = Math.Max(1, gifFrameCount);
            IsAnimatedGif = GifFrameCount > 1;
            PdfPageCount = Math.Max(0, pdfPageCount);
            IsPdfDocument = isPdfDocument || PdfPageCount > 0;
            Thumbnail = thumbnail;
            IsEncrypted = IsPdfDocument && isEncrypted;
            IsUnlocked = !IsPdfDocument || !IsEncrypted || isUnlocked;
            PasswordCache = passwordCache;
        }

        public string FilePath { get; }

        public string FileName { get; }

        public string Extension { get; }

        public long FileSizeBytes { get; }

        public string SizeText { get; }

        [ObservableProperty]
        private int pixelWidth;

        [ObservableProperty]
        private int pixelHeight;

        public long PixelCount => (long)PixelWidth * PixelHeight;

        public int GifFrameCount { get; }

        public bool IsAnimatedGif { get; }

        [ObservableProperty]
        private int pdfPageCount;

        public bool IsPdfDocument { get; }

        [ObservableProperty]
        private bool isEncrypted;

        [ObservableProperty]
        private bool isUnlocked = true;

        [ObservableProperty]
        private string passwordCache = string.Empty;

        [ObservableProperty]
        private string pdfLockTooltipText = string.Empty;

        public string ResolutionText => FormatResolutionText(PixelWidth, PixelHeight);

        public PdfFileState PdfSecurityState => new(IsEncrypted, IsUnlocked);

        public bool NeedsPdfUnlock => IsPdfDocument && PdfSecurityState.NeedsUnlock;

        public bool CanAccessPdfContent => !IsPdfDocument || !PdfSecurityState.NeedsUnlock;

        public bool ShouldShowPdfUnlockButton => IsPdfDocument && PdfSecurityState.NeedsUnlock;

        public bool ShowsUnlockedPdfLockIcon => IsPdfDocument && !NeedsPdfUnlock;

        [ObservableProperty]
        private bool isMarked;

        [ObservableProperty]
        private Bitmap? thumbnail;

        [ObservableProperty]
        private string gifBadgeText = string.Empty;

        [ObservableProperty]
        private string gifFrameCountText = string.Empty;

        partial void OnIsEncryptedChanged(bool value)
        {
            OnPropertyChanged(nameof(PdfSecurityState));
            OnPropertyChanged(nameof(NeedsPdfUnlock));
            OnPropertyChanged(nameof(CanAccessPdfContent));
            OnPropertyChanged(nameof(ShouldShowPdfUnlockButton));
            OnPropertyChanged(nameof(ShowsUnlockedPdfLockIcon));
        }

        partial void OnIsUnlockedChanged(bool value)
        {
            OnPropertyChanged(nameof(PdfSecurityState));
            OnPropertyChanged(nameof(NeedsPdfUnlock));
            OnPropertyChanged(nameof(CanAccessPdfContent));
            OnPropertyChanged(nameof(ShouldShowPdfUnlockButton));
            OnPropertyChanged(nameof(ShowsUnlockedPdfLockIcon));
        }

        partial void OnPixelWidthChanged(int value)
        {
            OnPropertyChanged(nameof(PixelCount));
            OnPropertyChanged(nameof(ResolutionText));
        }

        partial void OnPixelHeightChanged(int value)
        {
            OnPropertyChanged(nameof(PixelCount));
            OnPropertyChanged(nameof(ResolutionText));
        }

        public static bool TryCreate(string filePath, out ImageItemViewModel? item, out string? error, bool generateThumbnail = true)
        {
            item = null;
            error = null;

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    error = "File not found.";
                    return false;
                }

                if (ExecutableIconService.IsExecutableIconSource(filePath) ||
                    ShortcutIconService.IsShortcutIconSource(filePath))
                {
                    return TryCreateExtractedIconItem(filePath, generateThumbnail, out item, out error);
                }

                Bitmap? thumbnail = null;
                if (generateThumbnail)
                {
                    try
                    {
                        if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                        {
                            thumbnail = ImageConversionService.TryCreatePreview(filePath, 140);
                        }
                        else
                        {
                            using var stream = File.OpenRead(filePath);
                            thumbnail = Bitmap.DecodeToWidth(stream, 140);
                        }
                    }
                    catch
                    {
                        try
                        {
                            using var fallback = TryDecodeWithSystemDrawing(filePath);
                            if (fallback is not null)
                            {
                                using var image = SKImage.FromBitmap(fallback);
                                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                                if (data is not null)
                                {
                                    using var memory = new MemoryStream(data.ToArray());
                                    thumbnail = Bitmap.DecodeToWidth(memory, 140);
                                }
                            }
                        }
                        catch
                        {
                            // Keep import usable when thumbnail generation fails.
                        }
                    }
                }

                _ = TryReadImageInfo(filePath, out var width, out var height, out var frameCount);
                var gifFrameCount = Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                    ? frameCount
                    : 1;

                item = CreateImported(filePath, fileInfo.Length, width, height, thumbnail, gifFrameCount);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryCreateExtractedIconItem(string filePath, bool generateThumbnail, out ImageItemViewModel? item, out string? error)
        {
            item = null;
            error = null;

            using var bitmap = TryExtractIconBitmap(filePath);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                error = ShortcutIconService.IsShortcutIconSource(filePath)
                    ? "Unable to extract icon from shortcut."
                    : "Unable to extract icon from executable.";
                return false;
            }

            var encodedBytes = TryEncodeBitmapToPng(bitmap);
            var fileSize = encodedBytes?.LongLength > 0
                ? encodedBytes.LongLength
                : Math.Max(1, bitmap.ByteCount);
            var thumbnail = generateThumbnail
                ? TryCreateThumbnailFromEncodedBytes(encodedBytes, 140) ?? TryCreateThumbnailFromBitmap(bitmap, 140)
                : null;

            item = CreateImported(filePath, fileSize, bitmap.Width, bitmap.Height, thumbnail, gifFrameCount: 1);
            return true;
        }

        private static SKBitmap? TryExtractIconBitmap(string filePath)
        {
            if (ExecutableIconService.IsExecutableIconSource(filePath))
            {
                return ExecutableIconService.TryExtractPrimaryIconBitmap(filePath);
            }

            if (ShortcutIconService.IsShortcutIconSource(filePath))
            {
                return ShortcutIconService.TryExtractPrimaryIconBitmap(filePath);
            }

            return null;
        }

        public void Dispose()
        {
            Thumbnail?.Dispose();
        }

        internal static ImageItemViewModel CreateImported(
            string filePath,
            long fileSize,
            int pixelWidth,
            int pixelHeight,
            Bitmap? thumbnail,
            int gifFrameCount,
            int pdfPageCount = 0,
            bool isPdfDocument = false,
            bool isEncrypted = false,
            bool isUnlocked = true,
            string passwordCache = "")
        {
            return new ImageItemViewModel(
                filePath,
                fileSize,
                pixelWidth,
                pixelHeight,
                thumbnail,
                gifFrameCount,
                pdfPageCount,
                isPdfDocument,
                isEncrypted,
                isUnlocked,
                passwordCache ?? string.Empty);
        }

        private static bool TryReadImageInfo(string filePath, out int width, out int height, out int frameCount)
        {
            width = 0;
            height = 0;
            frameCount = 1;

            try
            {
                if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var svg = new SKSvg();
                    var picture = svg.Load(filePath);
                    if (picture is null)
                    {
                        return false;
                    }

                    var bounds = picture.CullRect;
                    width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
                    height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
                    frameCount = 1;
                    return true;
                }

                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                if (codec is null)
                {
                    return TryReadImageInfoWithSystemDrawing(filePath, out width, out height, out frameCount);
                }

                width = Math.Max(0, codec.Info.Width);
                height = Math.Max(0, codec.Info.Height);
                frameCount = Math.Max(1, codec.FrameCount);
                return width > 0 && height > 0;
            }
            catch
            {
                return TryReadImageInfoWithSystemDrawing(filePath, out width, out height, out frameCount);
            }
        }

        private static bool TryReadImageInfoWithSystemDrawing(string filePath, out int width, out int height, out int frameCount)
        {
            width = 0;
            height = 0;
            frameCount = 1;

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                return TryReadImageInfoWithSystemDrawingCore(filePath, out width, out height, out frameCount);
            }
            catch
            {
                width = 0;
                height = 0;
                frameCount = 1;
                return false;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool TryReadImageInfoWithSystemDrawingCore(string filePath, out int width, out int height, out int frameCount)
        {
            width = 0;
            height = 0;
            frameCount = 1;

            using var stream = File.OpenRead(filePath);
            using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            width = Math.Max(1, image.Width);
            height = Math.Max(1, image.Height);

            if (Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                image.FrameDimensionsList.Length > 0)
            {
                var dimension = new FrameDimension(image.FrameDimensionsList[0]);
                frameCount = Math.Max(1, image.GetFrameCount(dimension));
            }

            return true;
        }

        private static SKBitmap? TryDecodeWithSystemDrawing(string filePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                return TryDecodeWithSystemDrawingCore(filePath);
            }
            catch
            {
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static SKBitmap? TryDecodeWithSystemDrawingCore(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            using var memory = new MemoryStream();
            image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;
            return SKBitmap.Decode(memory);
        }

        private static string FormatResolutionText(int width, int height)
        {
            return width > 0 && height > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{width} x {height}")
                : "-";
        }

        private static byte[]? TryEncodeBitmapToPng(SKBitmap bitmap)
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data?.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap? TryCreateThumbnailFromEncodedBytes(byte[]? encodedBytes, int maxWidth)
        {
            if (encodedBytes is null || encodedBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using var memory = new MemoryStream(encodedBytes, writable: false);
                return Bitmap.DecodeToWidth(memory, maxWidth);
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap? TryCreateThumbnailFromBitmap(SKBitmap bitmap, int maxWidth)
        {
            var encodedBytes = TryEncodeBitmapToPng(bitmap);
            return TryCreateThumbnailFromEncodedBytes(encodedBytes, maxWidth);
        }

        private static string FormatSize(long size)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;

            if (size < kb)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{size} B");
            }

            if (size < mb)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{size / kb:0.0} KB");
            }

            return string.Create(CultureInfo.InvariantCulture, $"{size / mb:0.0} MB");
        }
    }
}
