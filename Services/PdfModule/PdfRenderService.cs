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
                canvas.Clear(new SKColor(240, 244, 249));

                using var shadowPaint = new SKPaint
                {
                    Color = new SKColor(188, 198, 212, 90),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(22, 26, 198, 270), 20, 20), shadowPaint);

                using var pagePaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(18, 18, 194, 262), 18, 18), pagePaint);

                using var accentPaint = new SKPaint
                {
                    Color = new SKColor(217, 72, 47),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(18, 18, 194, 70), 18, 18), accentPaint);

                using var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    TextSize = 32,
                    FakeBoldText = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText("PDF", 106, 53, textPaint);

                using var bodyPaint = new SKPaint
                {
                    Color = new SKColor(232, 237, 244),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(46, 102, 166, 224), 30, 30), bodyPaint);

                using var lockPaint = new SKPaint
                {
                    Color = new SKColor(71, 85, 105),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 12,
                    StrokeCap = SKStrokeCap.Round
                };
                canvas.DrawArc(new SKRect(68, 112, 144, 188), 180, -180, false, lockPaint);

                using var lockBodyPaint = new SKPaint
                {
                    Color = new SKColor(71, 85, 105),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(58, 154, 154, 224), 18, 18), lockBodyPaint);

                using var keyholePaint = new SKPaint
                {
                    Color = new SKColor(240, 244, 249),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawCircle(106, 184, 10, keyholePaint);
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(100, 184, 112, 206), 6, 6), keyholePaint);

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
