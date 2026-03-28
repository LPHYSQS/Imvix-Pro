using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;

namespace ImvixPro.Services
{
    public sealed class AiPreviewComparisonService
    {
        public const int MinDecodeWidth = 2048;
        public const int MaxDecodeWidth = 4096;
        private const double ViewportOverscan = 3d;

        private static readonly (double X, double Y)[] SamplePoints =
        [
            (0.18d, 0.21d),
            (0.33d, 0.67d),
            (0.50d, 0.50d),
            (0.72d, 0.28d),
            (0.84d, 0.79d)
        ];

        public AiPreviewBitmapLoadResult? TryLoadAsset(string filePath, double viewportWidth, double renderScaling)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var sourceSize = TryReadSourceSize(filePath, out var resolvedSize)
                ? resolvedSize
                : default;
            var decodeWidth = ResolveDecodeWidth(sourceSize.Width, viewportWidth, renderScaling);

            try
            {
                using var stream = File.OpenRead(filePath);
                var bitmap = sourceSize.Width > 0 && decodeWidth < sourceSize.Width
                    ? Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality)
                    : new Bitmap(stream);

                if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                {
                    sourceSize = bitmap.PixelSize;
                }

                return new AiPreviewBitmapLoadResult(filePath, bitmap, sourceSize);
            }
            catch
            {
                try
                {
                    var fallback = ImageConversionService.TryCreatePreview(filePath, decodeWidth);
                    if (fallback is null)
                    {
                        return null;
                    }

                    if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                    {
                        sourceSize = fallback.PixelSize;
                    }

                    return new AiPreviewBitmapLoadResult(filePath, fallback, sourceSize);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static int ResolveDecodeWidth(int sourceWidth, double viewportWidth, double renderScaling)
        {
            var viewportTarget = (int)Math.Ceiling(
                Math.Max(1d, viewportWidth) *
                Math.Max(1d, renderScaling) *
                ViewportOverscan);
            var decodeWidth = Math.Clamp(viewportTarget, MinDecodeWidth, MaxDecodeWidth);
            return sourceWidth > 0
                ? Math.Min(Math.Max(1, sourceWidth), decodeWidth)
                : decodeWidth;
        }

        public static bool TryBuildDiagnostics(
            Bitmap sourceBitmap,
            PixelSize sourceSize,
            Bitmap enhancedBitmap,
            PixelSize enhancedSize,
            out AiPreviewComparisonDiagnostics diagnostics)
        {
            diagnostics = default;

            if (!TryDecodeBitmap(sourceBitmap, out var decodedSource) ||
                !TryDecodeBitmap(enhancedBitmap, out var decodedEnhanced))
            {
                return false;
            }

            using (decodedSource!)
            using (decodedEnhanced!)
            {
                var sample = FindMostDifferentSample(decodedSource!, decodedEnhanced!);
                diagnostics = new AiPreviewComparisonDiagnostics(
                    sourceSize,
                    enhancedSize,
                    sample.Summary,
                    sample.Delta > 0);
                return true;
            }
        }

        private static AiPreviewSampleResult FindMostDifferentSample(SKBitmap sourceBitmap, SKBitmap enhancedBitmap)
        {
            AiPreviewSampleResult? best = null;

            foreach (var point in SamplePoints)
            {
                var sourceColor = SamplePixel(sourceBitmap, point.X, point.Y);
                var enhancedColor = SamplePixel(enhancedBitmap, point.X, point.Y);
                var delta = Math.Abs(sourceColor.Red - enhancedColor.Red) +
                            Math.Abs(sourceColor.Green - enhancedColor.Green) +
                            Math.Abs(sourceColor.Blue - enhancedColor.Blue);
                var sampleX = (int)Math.Round(point.X * 100d, MidpointRounding.AwayFromZero);
                var sampleY = (int)Math.Round(point.Y * 100d, MidpointRounding.AwayFromZero);
                var summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"@{sampleX}%,{sampleY}% RGB ({sourceColor.Red},{sourceColor.Green},{sourceColor.Blue}) -> ({enhancedColor.Red},{enhancedColor.Green},{enhancedColor.Blue}), Δ={delta}");
                var candidate = new AiPreviewSampleResult(summary, delta);

                if (best is null || candidate.Delta > best.Value.Delta)
                {
                    best = candidate;
                }
            }

            return best ?? new AiPreviewSampleResult("n/a", 0);
        }

        private static SKColor SamplePixel(SKBitmap bitmap, double normalizedX, double normalizedY)
        {
            var width = Math.Max(1, bitmap.Width);
            var height = Math.Max(1, bitmap.Height);
            var x = Math.Clamp((int)Math.Round((width - 1) * normalizedX, MidpointRounding.AwayFromZero), 0, width - 1);
            var y = Math.Clamp((int)Math.Round((height - 1) * normalizedY, MidpointRounding.AwayFromZero), 0, height - 1);
            return bitmap.GetPixel(x, y);
        }

        private static bool TryDecodeBitmap(Bitmap bitmap, out SKBitmap? decodedBitmap)
        {
            decodedBitmap = null;

            try
            {
                using var memory = new MemoryStream();
                bitmap.Save(memory);
                memory.Position = 0;
                decodedBitmap = SKBitmap.Decode(memory);
                return decodedBitmap is not null;
            }
            catch
            {
                decodedBitmap?.Dispose();
                decodedBitmap = null;
                return false;
            }
        }

        private static bool TryReadSourceSize(string filePath, out PixelSize size)
        {
            size = default;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                if (codec is not null)
                {
                    size = new PixelSize(Math.Max(1, codec.Info.Width), Math.Max(1, codec.Info.Height));
                    return true;
                }
            }
            catch
            {
            }

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using var image = System.Drawing.Image.FromFile(filePath);
                size = new PixelSize(Math.Max(1, image.Width), Math.Max(1, image.Height));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private readonly record struct AiPreviewSampleResult(string Summary, int Delta);
    }

    public sealed class AiPreviewBitmapLoadResult : IDisposable
    {
        public AiPreviewBitmapLoadResult(string filePath, Bitmap bitmap, PixelSize sourceSize)
        {
            FilePath = filePath;
            Bitmap = bitmap;
            SourceSize = sourceSize;
        }

        public string FilePath { get; }

        public Bitmap Bitmap { get; }

        public PixelSize SourceSize { get; }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }

    public readonly record struct AiPreviewComparisonDiagnostics(
        PixelSize SourceSize,
        PixelSize EnhancedSize,
        string SampleSummary,
        bool HasPixelDifference);
}
