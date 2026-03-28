using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace ImvixPro.Services
{
    public sealed record PreviewQrRecognition(
        string Text,
        bool HasText,
        string? ErrorMessage = null)
    {
        public static PreviewQrRecognition Empty()
        {
            return new PreviewQrRecognition(string.Empty, false);
        }

        public static PreviewQrRecognition Error(string message)
        {
            return new PreviewQrRecognition(string.Empty, false, message);
        }
    }

    public sealed record PreviewQrCodeResult(
        string Content,
        bool IsUrl,
        double NormalizedCenterX = 0d,
        double NormalizedCenterY = 0d);

    public sealed record PreviewQrBatchRecognition(
        IReadOnlyList<PreviewQrCodeResult> Results,
        string? ErrorMessage = null)
    {
        public bool HasResults => Results.Count > 0;

        public static PreviewQrBatchRecognition Empty()
        {
            return new PreviewQrBatchRecognition(Array.Empty<PreviewQrCodeResult>());
        }

        public static PreviewQrBatchRecognition Error(string message)
        {
            return new PreviewQrBatchRecognition(Array.Empty<PreviewQrCodeResult>(), message);
        }
    }

    public sealed class PreviewQrService
    {
        private const double MultiResultRowTolerance = 0.08d;
        private const double MultiResultDedupTolerance = 0.05d;
        private static readonly Regex UrlRegex = new(
            @"https?://[^\s""'<>]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Lazy<PreviewQrRuntimeStatus> RuntimeStatus =
            new(InitializeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);

        public const string PathErrorCode = "preview-qr-path-error";
        public const string UnsupportedPlatformErrorCode = "preview-qr-unsupported-platform";
        public const string InitializationFailedErrorCode = "preview-qr-initialization-failed";

        public static void WarmUpRuntime()
        {
            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady && !string.IsNullOrWhiteSpace(runtimeStatus.DiagnosticMessage))
            {
                Trace.TraceError($"QR runtime validation failed: {runtimeStatus.DiagnosticMessage}");
            }
        }

        public async Task<PreviewQrRecognition> RecognizeAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return PreviewQrRecognition.Error("QR image data is empty.");
            }

            if (!IsSystemDrawingSupported())
            {
                return PreviewQrRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady)
            {
                return PreviewQrRecognition.Error(runtimeStatus.ErrorCode ?? InitializationFailedErrorCode);
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return RecognizeCore(imageBytes, runtimeStatus.Settings);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"QR recognition failed: {ex}");
                    return PreviewQrRecognition.Error(InitializationFailedErrorCode);
                }
            }, cancellationToken);
        }

        public async Task<PreviewQrBatchRecognition> RecognizeAllAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return PreviewQrBatchRecognition.Error("QR image data is empty.");
            }

            if (!IsSystemDrawingSupported())
            {
                return PreviewQrBatchRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady)
            {
                return PreviewQrBatchRecognition.Error(runtimeStatus.ErrorCode ?? InitializationFailedErrorCode);
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return RecognizeAllCore(imageBytes, runtimeStatus.Settings);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"QR multi-recognition failed: {ex}");
                    return PreviewQrBatchRecognition.Error(InitializationFailedErrorCode);
                }
            }, cancellationToken);
        }

        [SupportedOSPlatformGuard("windows")]
        private static bool IsSystemDrawingSupported()
        {
            return OperatingSystem.IsWindows();
        }

        public static bool TryGetSingleUrl(string? text, out string? url)
        {
            url = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
            {
                return false;
            }

            return TryNormalizeHttpUrl(trimmed, out url);
        }

        public static IReadOnlyList<string> ExtractUrls(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            var matches = UrlRegex.Matches(text);
            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            var urls = new List<string>(matches.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                var candidate = TrimTrailingUrlPunctuation(match.Value);
                if (!TryNormalizeHttpUrl(candidate, out var normalizedUrl) ||
                    string.IsNullOrWhiteSpace(normalizedUrl) ||
                    !seen.Add(normalizedUrl))
                {
                    continue;
                }

                urls.Add(normalizedUrl);
            }

            return urls.Count == 0 ? Array.Empty<string>() : urls;
        }

        private static bool TryNormalizeHttpUrl(string? text, out string? url)
        {
            url = null;

            if (string.IsNullOrWhiteSpace(text) ||
                !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            url = uri.AbsoluteUri;
            return true;
        }

        private static string TrimTrailingUrlPunctuation(string value)
        {
            var end = value.Length;
            while (end > 0)
            {
                var c = value[end - 1];
                if (c is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}')
                {
                    end--;
                    continue;
                }

                break;
            }

            return end == value.Length ? value : value[..end];
        }

        private static PreviewQrRecognition RecognizeCore(byte[] imageBytes, PreviewQrDecoderSettings settings)
        {
            if (!IsSystemDrawingSupported())
            {
                return PreviewQrRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var primary = TryDecode(imageBytes, settings);
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return new PreviewQrRecognition(primary.Trim(), true);
            }

            var upscaledBytes = TryCreateUpscaledImageBytes(imageBytes, settings.UpscaleLongestEdge);
            if (upscaledBytes is not null)
            {
                var retry = TryDecode(upscaledBytes, settings);
                if (!string.IsNullOrWhiteSpace(retry))
                {
                    return new PreviewQrRecognition(retry.Trim(), true);
                }
            }

            return PreviewQrRecognition.Empty();
        }

        private static PreviewQrBatchRecognition RecognizeAllCore(byte[] imageBytes, PreviewQrDecoderSettings settings)
        {
            if (!IsSystemDrawingSupported())
            {
                return PreviewQrBatchRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var mergedResults = new List<PreviewQrCodeResult>();
            MergeQrResults(mergedResults, TryDecodeMultiple(imageBytes, settings));

            var upscaledBytes = TryCreateUpscaledImageBytes(imageBytes, settings.UpscaleLongestEdge);
            if (upscaledBytes is not null)
            {
                MergeQrResults(mergedResults, TryDecodeMultiple(upscaledBytes, settings));
            }

            if (mergedResults.Count > 1)
            {
                SortQrResults(mergedResults);
                return new PreviewQrBatchRecognition(mergedResults);
            }

            var singleResult = RecognizeCore(imageBytes, settings);
            if (!singleResult.HasText)
            {
                return string.IsNullOrWhiteSpace(singleResult.ErrorMessage)
                    ? PreviewQrBatchRecognition.Empty()
                    : PreviewQrBatchRecognition.Error(singleResult.ErrorMessage);
            }

            if (mergedResults.Count == 1 &&
                string.Equals(mergedResults[0].Content, singleResult.Text, StringComparison.Ordinal))
            {
                return new PreviewQrBatchRecognition(new[] { mergedResults[0] });
            }

            return new PreviewQrBatchRecognition(
                new[]
                {
                    CreateQrCodeResult(singleResult.Text.Trim(), 0d, 0d)
                });
        }

        [SupportedOSPlatform("windows")]
        private static string? TryDecode(byte[] imageBytes, PreviewQrDecoderSettings settings)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);
            var luminanceSource = CreateLuminanceSource(bitmap);
            var reader = CreateReader(settings);
            var result = reader.Decode(luminanceSource);
            return result?.Text;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<PreviewQrCodeResult> TryDecodeMultiple(byte[] imageBytes, PreviewQrDecoderSettings settings)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);
            var luminanceSource = CreateLuminanceSource(bitmap);
            var reader = CreateReader(settings);
            var results = reader.DecodeMultiple(luminanceSource);

            if (results is null || results.Length == 0)
            {
                return Array.Empty<PreviewQrCodeResult>();
            }

            var decodedResults = new List<PreviewQrCodeResult>(results.Length);
            foreach (var result in results)
            {
                var qrResult = CreateQrCodeResult(result, bitmap.Width, bitmap.Height);
                if (qrResult is not null)
                {
                    decodedResults.Add(qrResult);
                }
            }

            return decodedResults.Count == 0
                ? Array.Empty<PreviewQrCodeResult>()
                : decodedResults;
        }

        private static BarcodeReaderGeneric CreateReader(PreviewQrDecoderSettings settings)
        {
            return new BarcodeReaderGeneric
            {
                AutoRotate = settings.AutoRotate,
                Options = new DecodingOptions
                {
                    PossibleFormats = [BarcodeFormat.QR_CODE],
                    TryHarder = settings.TryHarder,
                    TryInverted = settings.TryInverted,
                    PureBarcode = settings.PureBarcode
                }
            };
        }

        [SupportedOSPlatform("windows")]
        private static Bitmap CreateArgbBitmap(Image sourceImage)
        {
            var bitmap = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(sourceImage, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            return bitmap;
        }

        [SupportedOSPlatform("windows")]
        private static RGBLuminanceSource CreateLuminanceSource(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                var byteCount = Math.Abs(bitmapData.Stride) * bitmapData.Height;
                var pixelBytes = new byte[byteCount];
                Marshal.Copy(bitmapData.Scan0, pixelBytes, 0, byteCount);
                return new RGBLuminanceSource(
                    pixelBytes,
                    bitmapData.Width,
                    bitmapData.Height,
                    RGBLuminanceSource.BitmapFormat.BGRA32);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private static PreviewQrCodeResult? CreateQrCodeResult(Result? result, int imageWidth, int imageHeight)
        {
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                return null;
            }

            var content = result.Text.Trim();
            var centerX = 0d;
            var centerY = 0d;
            var resultPoints = result.ResultPoints;

            if (resultPoints is not null && resultPoints.Length > 0 && imageWidth > 0 && imageHeight > 0)
            {
                var minX = double.MaxValue;
                var maxX = double.MinValue;
                var minY = double.MaxValue;
                var maxY = double.MinValue;

                foreach (var point in resultPoints)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }

                if (minX <= maxX && minY <= maxY)
                {
                    centerX = Math.Clamp(((minX + maxX) / 2d) / imageWidth, 0d, 1d);
                    centerY = Math.Clamp(((minY + maxY) / 2d) / imageHeight, 0d, 1d);
                }
            }

            return CreateQrCodeResult(content, centerX, centerY);
        }

        private static PreviewQrCodeResult CreateQrCodeResult(string content, double normalizedCenterX, double normalizedCenterY)
        {
            return new PreviewQrCodeResult(
                content,
                TryGetSingleUrl(content, out _),
                normalizedCenterX,
                normalizedCenterY);
        }

        private static void MergeQrResults(List<PreviewQrCodeResult> target, IReadOnlyList<PreviewQrCodeResult> source)
        {
            foreach (var result in source)
            {
                var isDuplicate = false;
                foreach (var existing in target)
                {
                    if (AreSameDetection(existing, result))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    target.Add(result);
                }
            }
        }

        private static bool AreSameDetection(PreviewQrCodeResult left, PreviewQrCodeResult right)
        {
            return string.Equals(left.Content, right.Content, StringComparison.Ordinal) &&
                   Math.Abs(left.NormalizedCenterX - right.NormalizedCenterX) <= MultiResultDedupTolerance &&
                   Math.Abs(left.NormalizedCenterY - right.NormalizedCenterY) <= MultiResultDedupTolerance;
        }

        private static void SortQrResults(List<PreviewQrCodeResult> results)
        {
            results.Sort((left, right) =>
            {
                var yDelta = left.NormalizedCenterY - right.NormalizedCenterY;
                if (Math.Abs(yDelta) > MultiResultRowTolerance)
                {
                    return yDelta < 0d ? -1 : 1;
                }

                var xDelta = left.NormalizedCenterX - right.NormalizedCenterX;
                if (Math.Abs(xDelta) > 0.001d)
                {
                    return xDelta < 0d ? -1 : 1;
                }

                return yDelta < 0d ? -1 : (yDelta > 0d ? 1 : 0);
            });
        }

        [SupportedOSPlatform("windows")]
        private static byte[]? TryCreateUpscaledImageBytes(byte[] imageBytes, int targetLongestEdge)
        {
            if (targetLongestEdge <= 0)
            {
                return null;
            }

            try
            {
                using var input = new MemoryStream(imageBytes, writable: false);
                using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);

                var longestEdge = Math.Max(sourceImage.Width, sourceImage.Height);
                if (longestEdge <= 0)
                {
                    return null;
                }

                var scale = Math.Max(1d, targetLongestEdge / (double)longestEdge);
                if (scale <= 1.05d)
                {
                    return null;
                }

                var targetWidth = Math.Max(1, (int)Math.Round(sourceImage.Width * scale));
                var targetHeight = Math.Max(1, (int)Math.Round(sourceImage.Height * scale));

                using var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
                bitmap.SetResolution(300, 300);

                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.White);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(sourceImage, new Rectangle(0, 0, targetWidth, targetHeight));

                using var output = new MemoryStream();
                bitmap.Save(output, ImageFormat.Png);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"QR upscale retry failed: {ex.Message}");
                return null;
            }
        }

        private static PreviewQrRuntimeStatus InitializeRuntime()
        {
            if (!IsSystemDrawingSupported())
            {
                return new PreviewQrRuntimeStatus(
                    false,
                    AppContext.BaseDirectory,
                    string.Empty,
                    PreviewQrDecoderSettings.Default,
                    UnsupportedPlatformErrorCode,
                    "QR preview is supported only on Windows.");
            }

            var baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return new PreviewQrRuntimeStatus(
                    false,
                    string.Empty,
                    string.Empty,
                    PreviewQrDecoderSettings.Default,
                    PathErrorCode,
                    "AppContext.BaseDirectory is empty.");
            }

            var configPath = RuntimeAssetLocator.QrConfigFilePath;
            if (!File.Exists(configPath))
            {
                return new PreviewQrRuntimeStatus(
                    false,
                    baseDirectory,
                    configPath,
                    PreviewQrDecoderSettings.Default,
                    PathErrorCode,
                    $"QR decoder config was not found: {configPath}");
            }

            try
            {
                using var stream = File.OpenRead(configPath);
                var settings = JsonSerializer.Deserialize<PreviewQrDecoderSettings>(stream) ?? PreviewQrDecoderSettings.Default;
                return new PreviewQrRuntimeStatus(true, baseDirectory, configPath, settings.Normalize());
            }
            catch (Exception ex)
            {
                return new PreviewQrRuntimeStatus(
                    false,
                    baseDirectory,
                    configPath,
                    PreviewQrDecoderSettings.Default,
                    InitializationFailedErrorCode,
                    $"Failed to load QR decoder config: {ex.Message}");
            }
        }

        private sealed record PreviewQrRuntimeStatus(
            bool IsReady,
            string BaseDirectory,
            string ConfigPath,
            PreviewQrDecoderSettings Settings,
            string? ErrorCode = null,
            string? DiagnosticMessage = null);

        private sealed class PreviewQrDecoderSettings
        {
            public static PreviewQrDecoderSettings Default { get; } = new();

            public bool AutoRotate { get; init; } = true;

            public bool TryHarder { get; init; } = true;

            public bool TryInverted { get; init; } = true;

            public bool PureBarcode { get; init; }

            public int UpscaleLongestEdge { get; init; } = 1800;

            public PreviewQrDecoderSettings Normalize()
            {
                return new PreviewQrDecoderSettings
                {
                    AutoRotate = AutoRotate,
                    TryHarder = TryHarder,
                    TryInverted = TryInverted,
                    PureBarcode = PureBarcode,
                    UpscaleLongestEdge = Math.Clamp(UpscaleLongestEdge, 0, 4096)
                };
            }
        }
    }
}
