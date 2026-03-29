using Avalonia.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using ImvixPro.Models;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ImvixPro.Services.PdfModule
{
    public sealed class PdfRenderService
    {
        private const double PreviewMinScale = 0.4d;
        private const double PreviewMaxScale = 4d;
        private const double ExportBaseScale = 2d;
        private const double ExportMaxScale = 6d;

        public bool TryReadDocumentInfo(string filePath, out PdfDocumentInfo info, out string? error)
        {
            info = default;
            error = null;

            try
            {
                var resolvedPath = AppServices.PdfSecurityService.ResolveAccessiblePath(filePath);
                using var docReader = DocLib.Instance.GetDocReader(resolvedPath, new PageDimensions(1d));
                var pageCount = docReader.GetPageCount();
                if (pageCount <= 0)
                {
                    error = "PDF contains no readable pages.";
                    return false;
                }

                using var pageReader = docReader.GetPageReader(0);
                info = new PdfDocumentInfo(
                    pageCount,
                    Math.Max(1, pageReader.GetPageWidth()),
                    Math.Max(1, pageReader.GetPageHeight()));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public Bitmap? TryCreatePreview(string filePath, int pageIndex, int maxWidth)
        {
            try
            {
                using var rendered = TryRenderPageBitmap(filePath, pageIndex, Math.Max(1, maxWidth));
                if (rendered is null)
                {
                    return null;
                }

                using var image = SKImage.FromBitmap(rendered);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                if (data is null)
                {
                    return null;
                }

                using var memory = new MemoryStream(data.ToArray());
                return Avalonia.Media.Imaging.Bitmap.DecodeToWidth(memory, Math.Max(1, maxWidth));
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(PdfRenderService), $"Failed to create a preview bitmap for PDF '{filePath}' page {pageIndex}.", ex);
                return null;
            }
        }

        public Bitmap? TryCreateLockedPreview(int maxWidth)
        {
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(220, 300));
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(242, 246, 251));

                using var shadowPaint = new SKPaint
                {
                    Color = new SKColor(61, 92, 131, 28),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(30, 28, 190, 278), 24, 24), shadowPaint);

                using var pagePaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(24, 22, 184, 272), 22, 22), pagePaint);

                using var pageBorderPaint = new SKPaint
                {
                    Color = new SKColor(214, 225, 237),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(24, 22, 184, 272), 22, 22), pageBorderPaint);

                using var titleBadgePaint = new SKPaint
                {
                    Color = new SKColor(232, 239, 248),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(50, 36, 158, 74), 18, 18), titleBadgePaint);

                using var textPaint = new SKPaint
                {
                    Color = new SKColor(69, 92, 124),
                    IsAntialias = true,
                    TextSize = 24,
                    FakeBoldText = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText("PDF", 104, 62, textPaint);

                using var cardPaint = new SKPaint
                {
                    Color = new SKColor(244, 248, 252),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(54, 98, 154, 228), 28, 28), cardPaint);

                using var cardBorderPaint = new SKPaint
                {
                    Color = new SKColor(215, 224, 236),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(54, 98, 154, 228), 28, 28), cardBorderPaint);

                using var badgePaint = new SKPaint
                {
                    Color = new SKColor(248, 251, 255),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(82, 116, 126, 160), 16, 16), badgePaint);

                using var badgeBorderPaint = new SKPaint
                {
                    Color = new SKColor(202, 218, 233),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(82, 116, 126, 160), 16, 16), badgeBorderPaint);

                using var lockPaint = new SKPaint
                {
                    Color = new SKColor(76, 102, 139),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 6,
                    StrokeCap = SKStrokeCap.Round
                };
                canvas.DrawArc(new SKRect(90, 122, 118, 148), 180, -180, false, lockPaint);

                using var lockBodyPaint = new SKPaint
                {
                    Color = new SKColor(76, 102, 139),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(89, 136, 119, 156), 8, 8), lockBodyPaint);

                using var keyholePaint = new SKPaint
                {
                    Color = new SKColor(242, 246, 251),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawCircle(104, 144, 3.6f, keyholePaint);
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(101, 144, 107, 151), 3, 3), keyholePaint);

                using var linePaint = new SKPaint
                {
                    Color = new SKColor(205, 216, 229),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 8,
                    StrokeCap = SKStrokeCap.Round
                };
                canvas.DrawLine(80, 182, 128, 182, linePaint);
                canvas.DrawLine(72, 198, 136, 198, linePaint);
                canvas.DrawLine(82, 214, 126, 214, linePaint);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                if (data is null)
                {
                    return null;
                }

                using var stream = new MemoryStream(data.ToArray());
                return Bitmap.DecodeToWidth(stream, Math.Max(1, maxWidth));
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(PdfRenderService), "Failed to generate the locked PDF placeholder preview.", ex);
                return null;
            }
        }

        public SKBitmap? TryRenderPageBitmap(string filePath, int pageIndex, int targetWidth)
        {
            try
            {
                if (!TryReadPageMetrics(filePath, pageIndex, out _, out var clampedPageIndex, out var sourceWidth, out _, out _))
                {
                    return null;
                }

                var width = Math.Max(1, targetWidth);
                var scale = Math.Clamp(width / (double)Math.Max(1, sourceWidth), PreviewMinScale, PreviewMaxScale);
                return RenderScaledPage(filePath, clampedPageIndex, scale);
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(PdfRenderService), $"Failed to render PDF '{filePath}' page {pageIndex} to a bitmap.", ex);
                return null;
            }
        }

        public SKBitmap RenderPageForExport(string filePath, int pageIndex, int minimumWidth)
        {
            if (!TryReadPageMetrics(filePath, pageIndex, out _, out var clampedPageIndex, out var sourceWidth, out _, out var error))
            {
                throw new InvalidOperationException(error ?? "Unable to read PDF page.");
            }

            var requestedWidth = Math.Max(1, minimumWidth);
            var renderWidth = Math.Max(requestedWidth, (int)Math.Ceiling(sourceWidth * ExportBaseScale));
            var scale = Math.Clamp(renderWidth / (double)Math.Max(1, sourceWidth), 1d, ExportMaxScale);
            return RenderScaledPage(filePath, clampedPageIndex, scale);
        }

        public int ClampPageIndex(ImageItemViewModel? image, int pageIndex)
        {
            if (image is null || !image.IsPdfDocument)
            {
                return 0;
            }

            return Math.Clamp(pageIndex, 0, Math.Max(0, image.PdfPageCount - 1));
        }

        private static bool TryReadPageMetrics(
            string filePath,
            int requestedPageIndex,
            out int pageCount,
            out int clampedPageIndex,
            out int width,
            out int height,
            out string? error)
        {
            pageCount = 0;
            clampedPageIndex = 0;
            width = 0;
            height = 0;
            error = null;

            try
            {
                var resolvedPath = AppServices.PdfSecurityService.ResolveAccessiblePath(filePath);
                using var docReader = DocLib.Instance.GetDocReader(resolvedPath, new PageDimensions(1d));
                pageCount = Math.Max(0, docReader.GetPageCount());
                if (pageCount == 0)
                {
                    error = "PDF contains no readable pages.";
                    return false;
                }

                clampedPageIndex = Math.Clamp(requestedPageIndex, 0, pageCount - 1);
                using var pageReader = docReader.GetPageReader(clampedPageIndex);
                width = Math.Max(1, pageReader.GetPageWidth());
                height = Math.Max(1, pageReader.GetPageHeight());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static SKBitmap RenderScaledPage(string filePath, int pageIndex, double scale)
        {
            var resolvedPath = AppServices.PdfSecurityService.ResolveAccessiblePath(filePath);
            using var docReader = DocLib.Instance.GetDocReader(resolvedPath, new PageDimensions(scale));
            using var pageReader = docReader.GetPageReader(pageIndex);

            var width = Math.Max(1, pageReader.GetPageWidth());
            var height = Math.Max(1, pageReader.GetPageHeight());
            var bytes = pageReader.GetImage();
            if (bytes is null || bytes.Length == 0)
            {
                throw new InvalidOperationException("Unable to render PDF page.");
            }

            return CreateBitmapFromBgraBytes(bytes, width, height);
        }

        private static SKBitmap CreateBitmapFromBgraBytes(byte[] bytes, int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var destination = bitmap.GetPixels();
            var length = Math.Min(bytes.Length, width * height * 4);
            Marshal.Copy(bytes, 0, destination, length);
            return bitmap;
        }
    }
}
