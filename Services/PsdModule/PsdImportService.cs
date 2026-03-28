using Avalonia.Media.Imaging;
using ImvixPro.Models;
using System;
using System.IO;

namespace ImvixPro.Services.PsdModule
{
    public sealed class PsdImportService
    {
        public const string UnsupportedPsdErrorCode = "psd-unsupported";

        private readonly PsdRenderService _psdRenderService = new();

        public static bool IsPsdFile(string filePath)
        {
            return PsdRenderService.IsPsdFile(filePath);
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

                if (!_psdRenderService.TryReadDocumentInfo(filePath, out var info, out error) || info is null)
                {
                    return false;
                }

                Bitmap? thumbnail = null;
                if (generateThumbnail)
                {
                    try
                    {
                        thumbnail = _psdRenderService.TryCreatePreview(filePath, 140, useBackground: false, backgroundColor: null);
                    }
                    catch
                    {
                        // Keep import usable when thumbnail generation fails.
                    }
                }

                item = ImageItemViewModel.CreateImported(
                    filePath,
                    fileInfo.Length,
                    info.Width,
                    info.Height,
                    thumbnail,
                    gifFrameCount: 1);

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
