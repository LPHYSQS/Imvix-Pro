using ImvixPro.Models;
using System.Collections.Generic;

namespace ImvixPro.AI.Matting.UI
{
    public static class AiMattingFormatCatalog
    {
        private static readonly OutputImageFormat[] TransparentFormats =
        [
            OutputImageFormat.Png,
            OutputImageFormat.Webp,
            OutputImageFormat.Tiff
        ];

        public static IReadOnlyList<OutputImageFormat> SupportedTransparentFormats => TransparentFormats;

        public static bool IsSupportedTransparentFormat(OutputImageFormat format)
        {
            foreach (var supported in TransparentFormats)
            {
                if (supported == format)
                {
                    return true;
                }
            }

            return false;
        }

        public static string BuildDisplayName(OutputImageFormat format)
        {
            return format switch
            {
                OutputImageFormat.Webp => "WEBP",
                OutputImageFormat.Tiff => "TIFF",
                _ => "PNG"
            };
        }

        public static OutputImageFormat Normalize(OutputImageFormat format)
        {
            return IsSupportedTransparentFormat(format)
                ? format
                : OutputImageFormat.Png;
        }
    }
}
