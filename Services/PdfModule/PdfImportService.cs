using Avalonia.Media.Imaging;
using ImvixPro.Models;
using SkiaSharp;
using System;
using System.IO;

namespace ImvixPro.Services.PdfModule
{
    public sealed class PdfImportService
    {
        private readonly PdfRenderService _pdfRenderService = new();
        private readonly PdfSecurityService _pdfSecurityService = AppServices.PdfSecurityService;

        public static bool IsPdfFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryCreate(string filePath, out ImageItemViewModel? item, out string? error, bool generateThumbnail = true)
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

                if (!_pdfSecurityService.TryInspect(filePath, out var security, out error))
                {
                    return false;
                }

                Bitmap? thumbnail = null;
                if (generateThumbnail)
                {
                    try
                    {
                        thumbnail = security.IsUnlocked
                            ? _pdfRenderService.TryCreatePreview(filePath, 0, 140)
                            : _pdfRenderService.TryCreateLockedPreview(140);
                    }
                    catch (Exception ex)
                    {
                        AppServices.Logger.LogDebug(nameof(PdfImportService), $"Failed to generate a thumbnail for PDF '{filePath}'. Import will continue without it.", ex);
                    }
                }

                item = ImageItemViewModel.CreateImported(
                    filePath,
                    fileInfo.Length,
                    security.DocumentInfo.FirstPageWidth,
                    security.DocumentInfo.FirstPageHeight,
                    thumbnail,
                    gifFrameCount: 1,
                    pdfPageCount: security.DocumentInfo.PageCount,
                    isPdfDocument: true,
                    isEncrypted: security.IsEncrypted,
                    isUnlocked: security.IsUnlocked);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
