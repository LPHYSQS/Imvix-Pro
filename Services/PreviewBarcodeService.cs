using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace ImvixPro.Services
{
    public sealed record PreviewBarcodeResult(
        string Content,
        string Format,
        bool IsUrl,
        bool IsQrCode,
        double NormalizedCenterX = 0d,
        double NormalizedCenterY = 0d);

    public sealed record PreviewBarcodeBatchRecognition(
        IReadOnlyList<PreviewBarcodeResult> Results,
        string? ErrorMessage = null)
    {
        public bool HasResults => Results.Count > 0;

        public static PreviewBarcodeBatchRecognition Empty()
        {
            return new PreviewBarcodeBatchRecognition(Array.Empty<PreviewBarcodeResult>());
        }

        public static PreviewBarcodeBatchRecognition Error(string message)
        {
            return new PreviewBarcodeBatchRecognition(Array.Empty<PreviewBarcodeResult>(), message);
        }
    }

    public sealed class PreviewBarcodeService
    {
        private const double MultiResultRowTolerance = 0.08d;
        private const double MultiResultDedupTolerance = 0.12d;
        private static readonly BarcodeFormat[] DefaultPossibleFormats =
        [
            BarcodeFormat.CODE_128,
            BarcodeFormat.CODE_39,
            BarcodeFormat.CODE_93,
            BarcodeFormat.EAN_13,
            BarcodeFormat.EAN_8,
            BarcodeFormat.UPC_A,
            BarcodeFormat.UPC_E,
            BarcodeFormat.ITF,
            BarcodeFormat.CODABAR,
            BarcodeFormat.MSI,
            BarcodeFormat.RSS_14,
            BarcodeFormat.RSS_EXPANDED,
            BarcodeFormat.AZTEC,
            BarcodeFormat.DATA_MATRIX,
            BarcodeFormat.PDF_417,
            BarcodeFormat.QR_CODE
        ];
        private static readonly BarcodeFormat[] LinearFormats =
        [
            BarcodeFormat.CODE_128,
            BarcodeFormat.CODE_39,
            BarcodeFormat.CODE_93,
            BarcodeFormat.EAN_13,
            BarcodeFormat.EAN_8,
            BarcodeFormat.UPC_A,
            BarcodeFormat.UPC_E,
            BarcodeFormat.ITF,
            BarcodeFormat.CODABAR,
            BarcodeFormat.MSI,
            BarcodeFormat.RSS_14,
            BarcodeFormat.RSS_EXPANDED
        ];
        private static readonly BarcodeFormat[] MatrixFormats =
        [
            BarcodeFormat.AZTEC,
            BarcodeFormat.DATA_MATRIX,
            BarcodeFormat.PDF_417,
            BarcodeFormat.QR_CODE
        ];
        private static readonly Lazy<PreviewBarcodeRuntimeStatus> RuntimeStatus =
            new(InitializeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);

        public const string PathErrorCode = "preview-barcode-path-error";
        public const string UnsupportedPlatformErrorCode = "preview-barcode-unsupported-platform";
        public const string InitializationFailedErrorCode = "preview-barcode-initialization-failed";

        public static void WarmUpRuntime()
        {
            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady && !string.IsNullOrWhiteSpace(runtimeStatus.DiagnosticMessage))
            {
                Trace.TraceError($"Barcode runtime validation failed: {runtimeStatus.DiagnosticMessage}");
            }
        }

        public async Task<PreviewBarcodeBatchRecognition> RecognizeAllAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return PreviewBarcodeBatchRecognition.Error("Barcode image data is empty.");
            }

            if (!IsSystemDrawingSupported())
            {
                return PreviewBarcodeBatchRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady)
            {
                return PreviewBarcodeBatchRecognition.Error(runtimeStatus.ErrorCode ?? InitializationFailedErrorCode);
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
                    Trace.TraceError($"Barcode multi-recognition failed: {ex}");
                    return PreviewBarcodeBatchRecognition.Error(InitializationFailedErrorCode);
                }
            }, cancellationToken);
        }

        [SupportedOSPlatformGuard("windows")]
        private static bool IsSystemDrawingSupported()
        {
            return OperatingSystem.IsWindows();
        }

        private static PreviewBarcodeBatchRecognition RecognizeAllCore(
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings)
        {
            if (!IsSystemDrawingSupported())
            {
                return PreviewBarcodeBatchRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var mergedResults = new List<PreviewBarcodeResult>();
            var possibleFormats = ResolvePossibleFormats(settings.PossibleFormats);

            MergeScanPasses(mergedResults, imageBytes, settings, possibleFormats);

            var upscaledBytes = TryCreateUpscaledImageBytes(imageBytes, settings.UpscaleLongestEdge);
            if (upscaledBytes is not null)
            {
                MergeScanPasses(mergedResults, upscaledBytes, settings, possibleFormats);
            }

            if (mergedResults.Count <= 1)
            {
                MergeBarcodeResults(mergedResults, TryDecodeSeparatedRegions(imageBytes, settings, possibleFormats));

                if (upscaledBytes is not null)
                {
                    MergeBarcodeResults(mergedResults, TryDecodeSeparatedRegions(upscaledBytes, settings, possibleFormats));
                }
            }

            if (mergedResults.Count == 0)
            {
                var singleResult = TryDecodeSingle(imageBytes, settings, possibleFormats)
                    ?? (upscaledBytes is null
                        ? null
                        : TryDecodeSingle(upscaledBytes, settings, possibleFormats));

                if (singleResult is not null)
                {
                    mergedResults.Add(singleResult);
                }
            }

            if (mergedResults.Count == 0)
            {
                return PreviewBarcodeBatchRecognition.Empty();
            }

            CollapseEquivalentResults(mergedResults);
            SortBarcodeResults(mergedResults);
            return new PreviewBarcodeBatchRecognition(mergedResults);
        }

        [SupportedOSPlatform("windows")]
        private static void MergeScanPasses(
            List<PreviewBarcodeResult> target,
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            var generalPassResults = TryDecodeMultiple(imageBytes, settings, possibleFormats);
            MergeBarcodeResults(target, generalPassResults);

            if (generalPassResults.Count > 1 && HasMixedQrAndBarcodes(generalPassResults))
            {
                return;
            }

            var linearFormats = IntersectFormats(possibleFormats, LinearFormats);
            if (linearFormats.Count > 0)
            {
                MergeBarcodeResults(target, TryDecodeMultiple(imageBytes, settings, linearFormats));
            }

            var matrixFormats = IntersectFormats(possibleFormats, MatrixFormats);
            if (matrixFormats.Count > 0)
            {
                MergeBarcodeResults(target, TryDecodeMultiple(imageBytes, settings, matrixFormats));
            }
        }

        private static bool HasMixedQrAndBarcodes(IReadOnlyList<PreviewBarcodeResult> results)
        {
            var hasQr = false;
            var hasLinearBarcode = false;

            foreach (var result in results)
            {
                hasQr |= result.IsQrCode;
                hasLinearBarcode |= !result.IsQrCode;

                if (hasQr && hasLinearBarcode)
                {
                    return true;
                }
            }

            return false;
        }

        [SupportedOSPlatform("windows")]
        private static PreviewBarcodeResult? TryDecodeSingle(
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);
            var luminanceSource = CreateLuminanceSource(bitmap);
            var reader = CreateReader(settings, possibleFormats);
            var result = reader.Decode(luminanceSource);
            return CreateBarcodeResult(result, bitmap.Width, bitmap.Height);
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<PreviewBarcodeResult> TryDecodeMultiple(
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);
            var luminanceSource = CreateLuminanceSource(bitmap);
            var reader = CreateReader(settings, possibleFormats);
            var results = reader.DecodeMultiple(luminanceSource);

            if (results is null || results.Length == 0)
            {
                return Array.Empty<PreviewBarcodeResult>();
            }

            var decodedResults = new List<PreviewBarcodeResult>(results.Length);
            foreach (var result in results)
            {
                var barcodeResult = CreateBarcodeResult(result, bitmap.Width, bitmap.Height);
                if (barcodeResult is not null)
                {
                    decodedResults.Add(barcodeResult);
                }
            }

            return decodedResults.Count == 0
                ? Array.Empty<PreviewBarcodeResult>()
                : decodedResults;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<PreviewBarcodeResult> TryDecodeTiled(
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);

            var decodedResults = new List<PreviewBarcodeResult>();
            var tilePatterns = new (int Columns, int Rows)[]
            {
                (2, 1),
                (3, 1),
                (1, 2),
                (2, 2),
                (3, 2)
            };

            foreach (var tilePattern in tilePatterns)
            {
                foreach (var tile in BuildTileRectangles(bitmap.Width, bitmap.Height, tilePattern.Columns, tilePattern.Rows))
                {
                    using var tileBitmap = CopyBitmapRegion(bitmap, tile);
                    var tileResults = DecodeTile(tileBitmap, tile, bitmap.Width, bitmap.Height, settings, possibleFormats);
                    MergeBarcodeResults(decodedResults, tileResults);
                }
            }

            return decodedResults.Count == 0
                ? Array.Empty<PreviewBarcodeResult>()
                : decodedResults;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<PreviewBarcodeResult> TryDecodeSeparatedRegions(
            byte[] imageBytes,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
            using var bitmap = CreateArgbBitmap(sourceImage);

            var decodedResults = new List<PreviewBarcodeResult>();
            foreach (var region in BuildSeparatedRegionRectangles(bitmap))
            {
                using var regionBitmap = CopyBitmapRegion(bitmap, region);
                MergeBarcodeResults(
                    decodedResults,
                    DecodeTile(regionBitmap, region, bitmap.Width, bitmap.Height, settings, possibleFormats));
            }

            return decodedResults.Count == 0
                ? Array.Empty<PreviewBarcodeResult>()
                : decodedResults;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<PreviewBarcodeResult> DecodeTile(
            Bitmap tileBitmap,
            Rectangle tileRect,
            int totalWidth,
            int totalHeight,
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            var luminanceSource = CreateLuminanceSource(tileBitmap);
            var reader = CreateReader(settings, possibleFormats);
            var results = reader.DecodeMultiple(luminanceSource);

            if (results is not null && results.Length > 0)
            {
                var decodedResults = new List<PreviewBarcodeResult>(results.Length);
                foreach (var result in results)
                {
                    var barcodeResult = CreateBarcodeResult(
                        result,
                        totalWidth,
                        totalHeight,
                        tileRect.X,
                        tileRect.Y,
                        tileBitmap.Width,
                        tileBitmap.Height);
                    if (barcodeResult is not null)
                    {
                        decodedResults.Add(barcodeResult);
                    }
                }

                return decodedResults.Count == 0
                    ? Array.Empty<PreviewBarcodeResult>()
                    : decodedResults;
            }

            var singleResult = reader.Decode(luminanceSource);
            var fallbackResult = CreateBarcodeResult(
                singleResult,
                totalWidth,
                totalHeight,
                tileRect.X,
                tileRect.Y,
                tileBitmap.Width,
                tileBitmap.Height);

            return fallbackResult is null
                ? Array.Empty<PreviewBarcodeResult>()
                : new[] { fallbackResult };
        }

        private static BarcodeReaderGeneric CreateReader(
            PreviewBarcodeDecoderSettings settings,
            IReadOnlyList<BarcodeFormat> possibleFormats)
        {
            return new BarcodeReaderGeneric
            {
                AutoRotate = settings.AutoRotate,
                Options = new DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat>(possibleFormats),
                    TryHarder = settings.TryHarder,
                    TryInverted = settings.TryInverted,
                    PureBarcode = settings.PureBarcode
                }
            };
        }

        private static IReadOnlyList<BarcodeFormat> ResolvePossibleFormats(string[]? configuredFormats)
        {
            if (configuredFormats is null || configuredFormats.Length == 0)
            {
                return DefaultPossibleFormats;
            }

            var resolved = new List<BarcodeFormat>(configuredFormats.Length);
            var seen = new HashSet<BarcodeFormat>();

            foreach (var configuredFormat in configuredFormats)
            {
                if (!TryParseBarcodeFormat(configuredFormat, out var format) || !seen.Add(format))
                {
                    continue;
                }

                resolved.Add(format);
            }

            return resolved.Count == 0 ? DefaultPossibleFormats : resolved;
        }

        private static List<BarcodeFormat> IntersectFormats(
            IReadOnlyList<BarcodeFormat> source,
            IReadOnlyList<BarcodeFormat> filter)
        {
            var filterSet = new HashSet<BarcodeFormat>(filter);
            var results = new List<BarcodeFormat>();

            foreach (var format in source)
            {
                if (filterSet.Contains(format))
                {
                    results.Add(format);
                }
            }

            return results;
        }

        private static bool TryParseBarcodeFormat(string? value, out BarcodeFormat format)
        {
            format = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim()
                .Replace('-', '_')
                .Replace(' ', '_');

            return Enum.TryParse(normalized, ignoreCase: true, out format);
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
        private static Bitmap CopyBitmapRegion(Bitmap source, Rectangle sourceRect)
        {
            var bitmap = new Bitmap(sourceRect.Width, sourceRect.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                sourceRect,
                GraphicsUnit.Pixel);
            return bitmap;
        }

        [SupportedOSPlatform("windows")]
        private static Bitmap CreateAnalysisBitmap(Bitmap source, int maxLongestEdge)
        {
            var longestEdge = Math.Max(source.Width, source.Height);
            if (longestEdge <= 0 || longestEdge <= maxLongestEdge)
            {
                return CopyBitmapRegion(source, new Rectangle(0, 0, source.Width, source.Height));
            }

            var scale = maxLongestEdge / (double)longestEdge;
            var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
            return bitmap;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<Rectangle> BuildSeparatedRegionRectangles(Bitmap source)
        {
            const int analysisLongestEdge = 480;
            using var analysis = CreateAnalysisBitmap(source, analysisLongestEdge);
            var rectangles = new List<Rectangle>();

            foreach (var band in BuildAxisBands(analysis, scanColumns: true))
            {
                if (TryGetDarkRangeOnAxis(analysis, band.Start, band.End, scanColumns: false, out var top, out var bottom))
                {
                    AddUniqueRectangle(rectangles, MapAnalysisRectangle(source, analysis, band.Start, top, band.End, bottom));
                }
            }

            foreach (var band in BuildAxisBands(analysis, scanColumns: false))
            {
                if (TryGetDarkRangeOnAxis(analysis, band.Start, band.End, scanColumns: true, out var left, out var right))
                {
                    AddUniqueRectangle(rectangles, MapAnalysisRectangle(source, analysis, left, band.Start, right, band.End));
                }
            }

            return rectangles.Count == 0
                ? Array.Empty<Rectangle>()
                : rectangles;
        }

        [SupportedOSPlatform("windows")]
        private static IReadOnlyList<(int Start, int End)> BuildAxisBands(Bitmap analysis, bool scanColumns)
        {
            var length = scanColumns ? analysis.Width : analysis.Height;
            var orthogonalLength = scanColumns ? analysis.Height : analysis.Width;
            var activeThreshold = Math.Max(3, orthogonalLength / 22);
            var minimumBandLength = Math.Max(12, length / 14);
            var bands = new List<(int Start, int End)>();
            var bandStart = -1;

            for (var index = 0; index < length; index++)
            {
                var darkCount = 0;
                for (var offset = 0; offset < orthogonalLength; offset++)
                {
                    var pixel = scanColumns
                        ? analysis.GetPixel(index, offset)
                        : analysis.GetPixel(offset, index);

                    if (IsDarkPixel(pixel))
                    {
                        darkCount++;
                    }
                }

                var isActive = darkCount >= activeThreshold;
                if (isActive)
                {
                    if (bandStart < 0)
                    {
                        bandStart = index;
                    }
                }
                else if (bandStart >= 0)
                {
                    if (index - bandStart >= minimumBandLength)
                    {
                        bands.Add((bandStart, index));
                    }

                    bandStart = -1;
                }
            }

            if (bandStart >= 0 && length - bandStart >= minimumBandLength)
            {
                bands.Add((bandStart, length));
            }

            return bands;
        }

        [SupportedOSPlatform("windows")]
        private static bool TryGetDarkRangeOnAxis(
            Bitmap analysis,
            int start,
            int end,
            bool scanColumns,
            out int rangeStart,
            out int rangeEnd)
        {
            rangeStart = 0;
            rangeEnd = 0;

            var length = scanColumns ? analysis.Width : analysis.Height;
            var orthogonalLength = scanColumns ? analysis.Height : analysis.Width;
            var activeThreshold = Math.Max(3, Math.Max(1, end - start) / 18);

            var min = -1;
            var max = -1;

            for (var index = 0; index < length; index++)
            {
                var darkCount = 0;
                for (var offset = start; offset < end; offset++)
                {
                    if (offset < 0 || offset >= orthogonalLength)
                    {
                        continue;
                    }

                    var pixel = scanColumns
                        ? analysis.GetPixel(index, offset)
                        : analysis.GetPixel(offset, index);

                    if (IsDarkPixel(pixel))
                    {
                        darkCount++;
                    }
                }

                if (darkCount < activeThreshold)
                {
                    continue;
                }

                min = min < 0 ? index : Math.Min(min, index);
                max = Math.Max(max, index + 1);
            }

            if (min < 0 || max <= min)
            {
                return false;
            }

            rangeStart = min;
            rangeEnd = max;
            return true;
        }

        [SupportedOSPlatform("windows")]
        private static Rectangle MapAnalysisRectangle(
            Bitmap source,
            Bitmap analysis,
            int left,
            int top,
            int right,
            int bottom)
        {
            var scaleX = source.Width / (double)analysis.Width;
            var scaleY = source.Height / (double)analysis.Height;
            var paddingX = Math.Max(14, (int)Math.Round(scaleX * 10d));
            var paddingY = Math.Max(14, (int)Math.Round(scaleY * 10d));

            var mappedLeft = Math.Max(0, (int)Math.Floor(left * scaleX) - paddingX);
            var mappedTop = Math.Max(0, (int)Math.Floor(top * scaleY) - paddingY);
            var mappedRight = Math.Min(source.Width, (int)Math.Ceiling(right * scaleX) + paddingX);
            var mappedBottom = Math.Min(source.Height, (int)Math.Ceiling(bottom * scaleY) + paddingY);

            return new Rectangle(
                mappedLeft,
                mappedTop,
                Math.Max(1, mappedRight - mappedLeft),
                Math.Max(1, mappedBottom - mappedTop));
        }

        private static void AddUniqueRectangle(List<Rectangle> rectangles, Rectangle candidate)
        {
            var candidateArea = candidate.Width * candidate.Height;
            if (candidateArea <= 0)
            {
                return;
            }

            for (var index = 0; index < rectangles.Count; index++)
            {
                var existing = rectangles[index];
                var intersection = Rectangle.Intersect(existing, candidate);
                if (intersection.IsEmpty)
                {
                    continue;
                }

                var intersectionArea = intersection.Width * intersection.Height;
                var overlapRatio = intersectionArea / (double)Math.Min(
                    Math.Max(1, existing.Width * existing.Height),
                    candidateArea);

                if (overlapRatio < 0.75d)
                {
                    continue;
                }

                if (candidateArea < existing.Width * existing.Height)
                {
                    rectangles[index] = candidate;
                }

                return;
            }

            rectangles.Add(candidate);
        }

        [SupportedOSPlatform("windows")]
        private static bool IsDarkPixel(Color pixel)
        {
            if (pixel.A <= 32)
            {
                return false;
            }

            var luminance = ((pixel.R * 299) + (pixel.G * 587) + (pixel.B * 114)) / 1000;
            return luminance < 205;
        }

        private static IReadOnlyList<Rectangle> BuildTileRectangles(int width, int height, int columns, int rows)
        {
            if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0)
            {
                return Array.Empty<Rectangle>();
            }

            var rectangles = new List<Rectangle>(columns * rows);
            var cellWidth = width / (double)columns;
            var cellHeight = height / (double)rows;
            var overlapX = Math.Max(18, (int)Math.Round(cellWidth * 0.16d));
            var overlapY = Math.Max(18, (int)Math.Round(cellHeight * 0.16d));

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var left = Math.Max(0, (int)Math.Floor(column * cellWidth) - overlapX);
                    var top = Math.Max(0, (int)Math.Floor(row * cellHeight) - overlapY);
                    var right = Math.Min(width, (int)Math.Ceiling((column + 1) * cellWidth) + overlapX);
                    var bottom = Math.Min(height, (int)Math.Ceiling((row + 1) * cellHeight) + overlapY);

                    if (right <= left || bottom <= top)
                    {
                        continue;
                    }

                    rectangles.Add(new Rectangle(left, top, right - left, bottom - top));
                }
            }

            return rectangles;
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

        private static PreviewBarcodeResult? CreateBarcodeResult(Result? result, int imageWidth, int imageHeight)
        {
            return CreateBarcodeResult(result, imageWidth, imageHeight, 0, 0, imageWidth, imageHeight);
        }

        private static PreviewBarcodeResult? CreateBarcodeResult(
            Result? result,
            int imageWidth,
            int imageHeight,
            int offsetX,
            int offsetY,
            int scanWidth,
            int scanHeight)
        {
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                return null;
            }

            var content = result.Text.Trim();
            var centerX = 0d;
            var centerY = 0d;
            var resultPoints = result.ResultPoints;

            if (resultPoints is not null && resultPoints.Length > 0 && imageWidth > 0 && imageHeight > 0 && scanWidth > 0 && scanHeight > 0)
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
                    centerX = Math.Clamp((offsetX + ((minX + maxX) / 2d)) / imageWidth, 0d, 1d);
                    centerY = Math.Clamp((offsetY + ((minY + maxY) / 2d)) / imageHeight, 0d, 1d);
                }
            }

            return new PreviewBarcodeResult(
                content,
                ToDisplayFormat(result.BarcodeFormat),
                PreviewQrService.TryGetSingleUrl(content, out _),
                result.BarcodeFormat == BarcodeFormat.QR_CODE,
                centerX,
                centerY);
        }

        private static string ToDisplayFormat(BarcodeFormat format)
        {
            return format switch
            {
                BarcodeFormat.CODE_128 => "Code128",
                BarcodeFormat.CODE_39 => "Code39",
                BarcodeFormat.CODE_93 => "Code93",
                BarcodeFormat.EAN_13 => "EAN-13",
                BarcodeFormat.EAN_8 => "EAN-8",
                BarcodeFormat.UPC_A => "UPC-A",
                BarcodeFormat.UPC_E => "UPC-E",
                BarcodeFormat.ITF => "ITF",
                BarcodeFormat.CODABAR => "Codabar",
                BarcodeFormat.MSI => "MSI",
                BarcodeFormat.RSS_14 => "RSS-14",
                BarcodeFormat.RSS_EXPANDED => "RSS Expanded",
                BarcodeFormat.AZTEC => "Aztec",
                BarcodeFormat.DATA_MATRIX => "Data Matrix",
                BarcodeFormat.PDF_417 => "PDF417",
                BarcodeFormat.QR_CODE => "QR",
                _ => format.ToString()
            };
        }

        private static void MergeBarcodeResults(List<PreviewBarcodeResult> target, IReadOnlyList<PreviewBarcodeResult> source)
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

        private static void CollapseEquivalentResults(List<PreviewBarcodeResult> results)
        {
            if (results.Count <= 1)
            {
                return;
            }

            var collapsed = new List<PreviewBarcodeResult>(results.Count);
            foreach (var result in results)
            {
                var existingIndex = -1;
                for (var index = 0; index < collapsed.Count; index++)
                {
                    if (string.Equals(collapsed[index].Content, result.Content, StringComparison.Ordinal) &&
                        string.Equals(collapsed[index].Format, result.Format, StringComparison.Ordinal))
                    {
                        existingIndex = index;
                        break;
                    }
                }

                if (existingIndex < 0)
                {
                    collapsed.Add(result);
                    continue;
                }

                var existing = collapsed[existingIndex];
                var existingHasPosition = existing.NormalizedCenterX > 0.001d || existing.NormalizedCenterY > 0.001d;
                var currentHasPosition = result.NormalizedCenterX > 0.001d || result.NormalizedCenterY > 0.001d;
                if (currentHasPosition && !existingHasPosition)
                {
                    collapsed[existingIndex] = result;
                }
            }

            results.Clear();
            results.AddRange(collapsed);
        }

        private static bool AreSameDetection(PreviewBarcodeResult left, PreviewBarcodeResult right)
        {
            if (!string.Equals(left.Content, right.Content, StringComparison.Ordinal) ||
                !string.Equals(left.Format, right.Format, StringComparison.Ordinal))
            {
                return false;
            }

            var leftHasPosition = left.NormalizedCenterX > 0.001d || left.NormalizedCenterY > 0.001d;
            var rightHasPosition = right.NormalizedCenterX > 0.001d || right.NormalizedCenterY > 0.001d;
            if (!leftHasPosition || !rightHasPosition)
            {
                return true;
            }

            return Math.Abs(left.NormalizedCenterX - right.NormalizedCenterX) <= MultiResultDedupTolerance &&
                   Math.Abs(left.NormalizedCenterY - right.NormalizedCenterY) <= MultiResultDedupTolerance;
        }

        private static void SortBarcodeResults(List<PreviewBarcodeResult> results)
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
                Trace.TraceWarning($"Barcode upscale retry failed: {ex.Message}");
                return null;
            }
        }

        private static PreviewBarcodeRuntimeStatus InitializeRuntime()
        {
            if (!IsSystemDrawingSupported())
            {
                return new PreviewBarcodeRuntimeStatus(
                    false,
                    AppContext.BaseDirectory,
                    string.Empty,
                    PreviewBarcodeDecoderSettings.Default,
                    UnsupportedPlatformErrorCode,
                    "Barcode preview is supported only on Windows.");
            }

            var baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return new PreviewBarcodeRuntimeStatus(
                    false,
                    string.Empty,
                    string.Empty,
                    PreviewBarcodeDecoderSettings.Default,
                    PathErrorCode,
                    "AppContext.BaseDirectory is empty.");
            }

            var configPath = RuntimeAssetLocator.BarcodeConfigFilePath;
            if (!File.Exists(configPath))
            {
                return new PreviewBarcodeRuntimeStatus(
                    false,
                    baseDirectory,
                    configPath,
                    PreviewBarcodeDecoderSettings.Default,
                    PathErrorCode,
                    $"Barcode decoder config was not found: {configPath}");
            }

            try
            {
                using var stream = File.OpenRead(configPath);
                var settings = JsonSerializer.Deserialize<PreviewBarcodeDecoderSettings>(stream)
                               ?? PreviewBarcodeDecoderSettings.Default;
                return new PreviewBarcodeRuntimeStatus(true, baseDirectory, configPath, settings.Normalize());
            }
            catch (Exception ex)
            {
                return new PreviewBarcodeRuntimeStatus(
                    false,
                    baseDirectory,
                    configPath,
                    PreviewBarcodeDecoderSettings.Default,
                    InitializationFailedErrorCode,
                    $"Failed to load barcode decoder config: {ex.Message}");
            }
        }

        private sealed record PreviewBarcodeRuntimeStatus(
            bool IsReady,
            string BaseDirectory,
            string ConfigPath,
            PreviewBarcodeDecoderSettings Settings,
            string? ErrorCode = null,
            string? DiagnosticMessage = null);

        private sealed class PreviewBarcodeDecoderSettings
        {
            public static PreviewBarcodeDecoderSettings Default { get; } = new();

            public bool AutoRotate { get; init; } = true;

            public bool TryHarder { get; init; } = true;

            public bool TryInverted { get; init; } = true;

            public bool PureBarcode { get; init; }

            public int UpscaleLongestEdge { get; init; } = 2200;

            public string[]? PossibleFormats { get; init; }

            public PreviewBarcodeDecoderSettings Normalize()
            {
                return new PreviewBarcodeDecoderSettings
                {
                    AutoRotate = AutoRotate,
                    TryHarder = TryHarder,
                    TryInverted = TryInverted,
                    PureBarcode = PureBarcode,
                    UpscaleLongestEdge = Math.Clamp(UpscaleLongestEdge, 0, 4096),
                    PossibleFormats = NormalizePossibleFormats(PossibleFormats)
                };
            }

            private static string[] NormalizePossibleFormats(string[]? possibleFormats)
            {
                if (possibleFormats is null || possibleFormats.Length == 0)
                {
                    return CreateDefaultFormatNames();
                }

                var normalized = new List<string>(possibleFormats.Length);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var possibleFormat in possibleFormats)
                {
                    if (!TryParseBarcodeFormat(possibleFormat, out var format))
                    {
                        continue;
                    }

                    var formatName = format.ToString();
                    if (seen.Add(formatName))
                    {
                        normalized.Add(formatName);
                    }
                }

                return normalized.Count == 0 ? CreateDefaultFormatNames() : normalized.ToArray();
            }

            private static string[] CreateDefaultFormatNames()
            {
                var values = new string[DefaultPossibleFormats.Length];
                for (var index = 0; index < DefaultPossibleFormats.Length; index++)
                {
                    values[index] = DefaultPossibleFormats[index].ToString();
                }

                return values;
            }
        }
    }
}






