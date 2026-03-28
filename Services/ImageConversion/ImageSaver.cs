using Avalonia.Media.Imaging;
using ImvixPro.Models;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    internal static class ImageSaver
    {
        internal static void ConvertBitmapToSvg(SKBitmap sourceBitmap, string destinationPath)
        {
            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var canvas = SKSvgCanvas.Create(SKRect.Create(sourceBitmap.Width, sourceBitmap.Height), stream);

            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(sourceBitmap, 0, 0);
            canvas.Flush();
        }

        internal static void SaveBitmap(SKBitmap sourceBitmap, string destinationPath, ConversionOptions options)
        {
            if (options.OutputFormat == OutputImageFormat.Ico)
            {
                ConvertToIco(sourceBitmap, destinationPath);
                return;
            }

            if (options.OutputFormat == OutputImageFormat.Bmp)
            {
                ConvertToBmp(sourceBitmap, destinationPath);
                return;
            }

            if (options.OutputFormat == OutputImageFormat.Gif)
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("GIF output is only supported on Windows in this build.");
                }

                var gifQuality = ResolveGifQuality(options);
                if (TryCreateGifQuantizationTable(gifQuality, out var quantizationTable) && quantizationTable is not null)
                {
                    using var quantized = QuantizeGifColors(sourceBitmap, quantizationTable);
                    ConvertToGif(quantized, destinationPath);
                }
                else
                {
                    ConvertToGif(sourceBitmap, destinationPath);
                }
                return;
            }

            if (options.OutputFormat == OutputImageFormat.Tiff)
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("TIFF output is only supported on Windows in this build.");
                }

                ConvertToTiff(sourceBitmap, destinationPath);
                return;
            }

            using var image = SKImage.FromBitmap(sourceBitmap);
            var skiaFormat = ImageConversionService.ToSkiaFormat(options.OutputFormat);
            var encodedQuality = options.OutputFormat is OutputImageFormat.Jpeg or OutputImageFormat.Webp
                ? ResolveQuality(options)
                : 100;

            using var data = image.Encode(skiaFormat, encodedQuality);
            if (data is null)
            {
                throw new InvalidOperationException("Failed to encode image.");
            }

            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(stream);
        }

        internal static ConversionOptions NormalizeOptions(ConversionOptions options)
        {
            return new ConversionOptions
            {
                OutputFormat = options.OutputFormat,
                CompressionMode = options.CompressionMode,
                Quality = Math.Clamp(options.Quality, 1, 100),
                ResizeMode = options.ResizeMode,
                ResizeWidth = Math.Max(1, options.ResizeWidth),
                ResizeHeight = Math.Max(1, options.ResizeHeight),
                ResizePercent = Math.Clamp(options.ResizePercent, 1, 1000),
                RenameMode = options.RenameMode,
                RenamePrefix = options.RenamePrefix ?? string.Empty,
                RenameSuffix = options.RenameSuffix ?? string.Empty,
                RenameStartNumber = Math.Max(0, options.RenameStartNumber),
                RenameNumberDigits = Math.Clamp(options.RenameNumberDigits, 1, 8),
                OutputDirectoryRule = options.OutputDirectoryRule,
                OutputDirectory = options.OutputDirectory ?? string.Empty,
                AutoResultFolderName = string.IsNullOrWhiteSpace(options.AutoResultFolderName)
                    ? "Imvix_Output"
                    : options.AutoResultFolderName.Trim(),
                AllowOverwrite = options.AllowOverwrite,
                SvgUseBackground = options.SvgUseBackground,
                SvgBackgroundColor = string.IsNullOrWhiteSpace(options.SvgBackgroundColor)
                    ? "#FFFFFFFF"
                    : options.SvgBackgroundColor,
                IconUseTransparency = options.IconUseTransparency,
                IconBackgroundColor = string.IsNullOrWhiteSpace(options.IconBackgroundColor)
                    ? "#FFFFFFFF"
                    : options.IconBackgroundColor,
                LanguageCode = string.IsNullOrWhiteSpace(options.LanguageCode) ? "en-US" : options.LanguageCode,
                GifHandlingMode = options.GifHandlingMode,
                GifSpecificFrameIndex = Math.Max(0, options.GifSpecificFrameIndex),
                GifSpecificFrameSelections = new Dictionary<string, int>(options.GifSpecificFrameSelections ?? [], StringComparer.OrdinalIgnoreCase),
                GifFrameRanges = new Dictionary<string, GifFrameRangeSelection>(options.GifFrameRanges ?? [], StringComparer.OrdinalIgnoreCase),
                PdfImageExportMode = options.PdfImageExportMode,
                PdfDocumentExportMode = options.PdfDocumentExportMode,
                PdfPageIndex = Math.Max(0, options.PdfPageIndex),
                PdfPageSelections = new Dictionary<string, int>(options.PdfPageSelections ?? [], StringComparer.OrdinalIgnoreCase),
                PdfPageRanges = new Dictionary<string, PdfPageRangeSelection>(options.PdfPageRanges ?? [], StringComparer.OrdinalIgnoreCase),
                PdfUnlockStates = new Dictionary<string, bool>(options.PdfUnlockStates ?? [], StringComparer.OrdinalIgnoreCase),
                MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism)
            };
        }

        internal static int ResolveQuality(ConversionOptions options)
        {
            return options.CompressionMode switch
            {
                CompressionMode.HighQuality => 92,
                CompressionMode.Balanced => 80,
                CompressionMode.HighCompression => 60,
                _ => Math.Clamp(options.Quality, 1, 100)
            };
        }

        internal static int ResolveGifQuality(ConversionOptions options)
        {
            return options.CompressionMode switch
            {
                CompressionMode.HighQuality => 100,
                CompressionMode.Balanced => 80,
                CompressionMode.HighCompression => 60,
                _ => Math.Clamp(options.Quality, 1, 100)
            };
        }

        internal static bool TryCreateGifQuantizationTable(int gifQuality, [NotNullWhen(true)] out byte[]? table)
        {
            table = null;
            var clamped = Math.Clamp(gifQuality, 1, 100);
            if (clamped >= 100)
            {
                return false;
            }

            var levels = clamped switch
            {
                >= 90 => 6,
                >= 70 => 5,
                >= 50 => 4,
                >= 30 => 3,
                _ => 2
            };

            table = BuildGifQuantizationTable(levels);
            return true;
        }

        private static byte[] BuildGifQuantizationTable(int levels)
        {
            var table = new byte[256];
            var safeLevels = Math.Clamp(levels, 2, 6);
            var step = 255d / (safeLevels - 1);

            for (var i = 0; i < table.Length; i++)
            {
                var level = (int)Math.Round(i / step);
                level = Math.Clamp(level, 0, safeLevels - 1);
                var value = (int)Math.Round(level * step);
                table[i] = (byte)Math.Clamp(value, 0, 255);
            }

            return table;
        }

        internal static SKBitmap QuantizeGifColors(SKBitmap sourceBitmap, byte[] quantizationTable)
        {
            var result = sourceBitmap.Copy();
            var pixels = result.Pixels;

            for (var i = 0; i < pixels.Length; i++)
            {
                var color = pixels[i];
                if (color.Alpha == 0)
                {
                    continue;
                }

                pixels[i] = new SKColor(
                    quantizationTable[color.Red],
                    quantizationTable[color.Green],
                    quantizationTable[color.Blue],
                    color.Alpha);
            }

            result.Pixels = pixels;
            return result;
        }

        internal static SKBitmap CreatePreparedBitmap(SKBitmap sourceBitmap, ConversionOptions options)
        {
            var (targetWidth, targetHeight) = CalculateTargetDimensions(sourceBitmap.Width, sourceBitmap.Height, options);

            if (targetWidth == sourceBitmap.Width && targetHeight == sourceBitmap.Height)
            {
                return sourceBitmap.Copy();
            }

            var info = new SKImageInfo(targetWidth, targetHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);
            var result = new SKBitmap(info);
            using var canvas = new SKCanvas(result);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(sourceBitmap, SKRect.Create(targetWidth, targetHeight), paint);
            canvas.Flush();

            return result;
        }

        internal static (int Width, int Height) CalculateTargetDimensions(int sourceWidth, int sourceHeight, ConversionOptions options)
        {
            if (sourceWidth < 1 || sourceHeight < 1)
            {
                return (1, 1);
            }

            return options.ResizeMode switch
            {
                ResizeMode.FixedWidth =>
                    (Math.Max(1, options.ResizeWidth), Math.Max(1, (int)Math.Round(sourceHeight * (options.ResizeWidth / (double)sourceWidth)))),
                ResizeMode.FixedHeight =>
                    (Math.Max(1, (int)Math.Round(sourceWidth * (options.ResizeHeight / (double)sourceHeight))), Math.Max(1, options.ResizeHeight)),
                ResizeMode.ScalePercent =>
                    (
                        Math.Max(1, (int)Math.Round(sourceWidth * options.ResizePercent / 100d)),
                        Math.Max(1, (int)Math.Round(sourceHeight * options.ResizePercent / 100d))
                    ),
                ResizeMode.CustomSize => (Math.Max(1, options.ResizeWidth), Math.Max(1, options.ResizeHeight)),
                _ => (sourceWidth, sourceHeight)
            };
        }

        private static void ConvertToIco(SKBitmap sourceBitmap, string destinationPath)
        {
            using var iconBitmap = PrepareBitmapForIco(sourceBitmap);
            using var image = SKImage.FromBitmap(iconBitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
            if (pngData is null)
            {
                throw new InvalidOperationException("Failed to encode image.");
            }

            var pngBytes = pngData.ToArray();

            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream);

            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);

            writer.Write(iconBitmap.Width == 256 ? (byte)0 : (byte)iconBitmap.Width);
            writer.Write(iconBitmap.Height == 256 ? (byte)0 : (byte)iconBitmap.Height);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)pngBytes.Length);
            writer.Write((uint)22);
            writer.Write(pngBytes);
        }

        private static void ConvertToBmp(SKBitmap sourceBitmap, string destinationPath)
        {
            if (sourceBitmap.Width < 1 || sourceBitmap.Height < 1)
            {
                throw new InvalidOperationException("Invalid image dimensions for BMP conversion.");
            }

            const int fileHeaderSize = 14;
            const int dibHeaderSize = 40;
            const int bitsPerPixel = 32;
            const int bytesPerPixel = bitsPerPixel / 8;

            int rowStride;
            int pixelDataSize;
            int fileSize;
            checked
            {
                rowStride = sourceBitmap.Width * bytesPerPixel;
                pixelDataSize = rowStride * sourceBitmap.Height;
                fileSize = fileHeaderSize + dibHeaderSize + pixelDataSize;
            }

            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream);

            writer.Write((ushort)0x4D42);
            writer.Write(fileSize);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(fileHeaderSize + dibHeaderSize);

            writer.Write(dibHeaderSize);
            writer.Write(sourceBitmap.Width);
            writer.Write(-sourceBitmap.Height);
            writer.Write((ushort)1);
            writer.Write((ushort)bitsPerPixel);
            writer.Write(0);
            writer.Write(pixelDataSize);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            for (var y = 0; y < sourceBitmap.Height; y++)
            {
                for (var x = 0; x < sourceBitmap.Width; x++)
                {
                    var color = sourceBitmap.GetPixel(x, y);
                    writer.Write(color.Blue);
                    writer.Write(color.Green);
                    writer.Write(color.Red);
                    writer.Write(color.Alpha);
                }
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void ConvertToGif(SKBitmap sourceBitmap, string destinationPath)
        {
            using var image = SKImage.FromBitmap(sourceBitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
            if (pngData is null)
            {
                throw new InvalidOperationException("Failed to encode image for GIF conversion.");
            }

            using var pngStream = new MemoryStream(pngData.ToArray());
            using var gifImage = System.Drawing.Image.FromStream(
                pngStream,
                useEmbeddedColorManagement: true,
                validateImageData: true);
            gifImage.Save(destinationPath, System.Drawing.Imaging.ImageFormat.Gif);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void ConvertToTiff(SKBitmap sourceBitmap, string destinationPath)
        {
            using var image = SKImage.FromBitmap(sourceBitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
            if (pngData is null)
            {
                throw new InvalidOperationException("Failed to encode image for TIFF conversion.");
            }

            using var pngStream = new MemoryStream(pngData.ToArray());
            using var tiffImage = System.Drawing.Image.FromStream(
                pngStream,
                useEmbeddedColorManagement: true,
                validateImageData: true);
            tiffImage.Save(destinationPath, System.Drawing.Imaging.ImageFormat.Tiff);
        }

        private static SKBitmap PrepareBitmapForIco(SKBitmap sourceBitmap)
        {
            const int maxIconSize = 256;

            if (sourceBitmap.Width < 1 || sourceBitmap.Height < 1)
            {
                throw new InvalidOperationException("Invalid image dimensions for ICO conversion.");
            }

            var targetSize = Math.Min(maxIconSize, Math.Max(sourceBitmap.Width, sourceBitmap.Height));
            var scale = Math.Min((float)targetSize / sourceBitmap.Width, (float)targetSize / sourceBitmap.Height);
            var drawWidth = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale));
            var drawHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));
            var offsetX = (targetSize - drawWidth) / 2f;
            var offsetY = (targetSize - drawHeight) / 2f;

            var info = new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
            var iconBitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(iconBitmap);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.Clear(SKColors.Transparent);
            var destination = SKRect.Create(offsetX, offsetY, drawWidth, drawHeight);
            canvas.DrawBitmap(sourceBitmap, destination, paint);
            canvas.Flush();

            return iconBitmap;
        }

        internal static void ConvertToSvg(string inputPath, string destinationPath, ConversionOptions options)
        {
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) && options.ResizeMode == ResizeMode.None && !options.SvgUseBackground)
            {
                File.Copy(inputPath, destinationPath, overwrite: true);
                return;
            }

            var (useBackground, backgroundColor) = ResolveConfiguredBackground(inputPath, options);
            using var bitmap = DecodeToBitmap(inputPath, useBackground, backgroundColor);
            if (bitmap is null)
            {
                throw new InvalidOperationException("Unsupported or corrupted image file.");
            }

            using var prepared = CreatePreparedBitmap(bitmap, options);

            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var canvas = SKSvgCanvas.Create(SKRect.Create(prepared.Width, prepared.Height), stream);

            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(prepared, 0, 0);
            canvas.Flush();
        }

        internal static SKBitmap? DecodeToBitmap(string inputPath, bool svgUseBackground, string? svgBackgroundColor)
        {
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return DecodeSvgToBitmap(inputPath, svgUseBackground, svgBackgroundColor);
            }

            if (PsdImportService.IsPsdFile(inputPath))
            {
                return AppServices.PsdRenderService.TryCreateBitmap(inputPath, svgUseBackground, svgBackgroundColor, out _);
            }

            if (ExecutableIconService.IsExecutableIconSource(inputPath))
            {
                return RenderIconBitmap(ExecutableIconService.TryExtractPrimaryIconBitmap(inputPath), svgUseBackground, svgBackgroundColor);
            }

            if (ShortcutIconService.IsShortcutIconSource(inputPath))
            {
                return RenderIconBitmap(ShortcutIconService.TryExtractPrimaryIconBitmap(inputPath), svgUseBackground, svgBackgroundColor);
            }

            var decoded = SKBitmap.Decode(inputPath);
            decoded ??= ImageConversionService.TryDecodeWithSystemDrawing(inputPath);
            if (decoded is null)
            {
                return null;
            }

            if (!svgUseBackground)
            {
                return decoded;
            }

            using (decoded)
            {
                return CloneBitmapWithOptionalBackground(decoded, useBackground: true, svgBackgroundColor);
            }
        }

        private static SKBitmap DecodeSvgToBitmap(string inputPath, bool svgUseBackground, string? svgBackgroundColor)
        {
            var svg = new SKSvg();
            var picture = svg.Load(inputPath);
            if (picture is null)
            {
                throw new InvalidOperationException("Invalid SVG file.");
            }

            var bounds = picture.CullRect;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(bitmap);
            if (svgUseBackground)
            {
                canvas.Clear(ParseBackgroundColor(svgBackgroundColor));
            }
            else
            {
                canvas.Clear(SKColors.Transparent);
            }

            var matrix = SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture, ref matrix);
            canvas.Flush();

            return bitmap;
        }

        private static SKBitmap? RenderIconBitmap(SKBitmap? sourceBitmap, bool useBackground, string? backgroundColor)
        {
            if (sourceBitmap is null)
            {
                return null;
            }

            using (sourceBitmap)
            {
                return CloneBitmapWithOptionalBackground(sourceBitmap, useBackground, backgroundColor);
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

        private static SKColor ParseBackgroundColor(string? svgBackgroundColor)
        {
            if (!string.IsNullOrWhiteSpace(svgBackgroundColor) && SKColor.TryParse(svgBackgroundColor, out var parsed))
            {
                return parsed;
            }

            return SKColors.White;
        }

        private static bool IsIconSource(string inputPath)
        {
            return ExecutableIconService.IsExecutableIconSource(inputPath) ||
                   ShortcutIconService.IsShortcutIconSource(inputPath);
        }

        internal static (bool UseBackground, string BackgroundColor) ResolveConfiguredBackground(string inputPath, ConversionOptions options)
        {
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var forceBackground = !ImageConversionService.OutputFormatSupportsTransparency(options.OutputFormat);
                return (
                    options.SvgUseBackground || forceBackground,
                    options.SvgBackgroundColor);
            }

            if (PsdImportService.IsPsdFile(inputPath))
            {
                if (!AppServices.PsdRenderService.TryReadDocumentInfo(inputPath, out var info, out _) || info is null || !info.HasTransparency)
                {
                    return (false, "#FFFFFFFF");
                }

                var forceBackground = !ImageConversionService.OutputFormatSupportsTransparency(options.OutputFormat);
                return (options.SvgUseBackground || forceBackground, options.SvgBackgroundColor);
            }

            if (IsIconSource(inputPath))
            {
                var useBackground = !ImageConversionService.OutputFormatSupportsTransparency(options.OutputFormat) || !options.IconUseTransparency;
                return (useBackground, options.IconBackgroundColor);
            }

            var useRasterBackground = options.SvgUseBackground || !ImageConversionService.OutputFormatSupportsTransparency(options.OutputFormat);
            return useRasterBackground
                ? (true, options.SvgBackgroundColor)
                : (false, "#FFFFFFFF");
        }

        internal static (bool UseBackground, string BackgroundColor) ResolvePdfBackground(string inputPath, ConversionOptions options)
        {
            var extension = Path.GetExtension(inputPath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveConfiguredBackground(inputPath, options);
            }

            if (PsdImportService.IsPsdFile(inputPath))
            {
                if (!AppServices.PsdRenderService.TryReadDocumentInfo(inputPath, out var info, out _) || info is null || !info.HasTransparency)
                {
                    return (true, "#FFFFFFFF");
                }

                return (true, options.SvgBackgroundColor);
            }

            if (IsIconSource(inputPath))
            {
                return ResolveConfiguredBackground(inputPath, options);
            }

            return ResolveConfiguredBackground(inputPath, options);
        }

    }
}
