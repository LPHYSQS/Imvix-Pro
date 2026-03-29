using ImvixPro.Models;
using System;

namespace ImvixPro.Services
{
    public sealed class WatchProfilePlanningService
    {
        public WatchProfileSummary BuildSummary(WatchProfile watchProfile)
        {
            ArgumentNullException.ThrowIfNull(watchProfile);

            var options = BuildBaseOptions(watchProfile);

            return new WatchProfileSummary(
                options.OutputFormat,
                watchProfile.OutputDirectory,
                options.AiEnhancementEnabled,
                options.AiEnhancementScale,
                options.ResizeMode,
                options.GifHandlingMode,
                options.AllowOverwrite,
                expandsGifFramesDuringWatch: options.OutputFormat != OutputImageFormat.Gif,
                expandsPdfPagesDuringWatch: options.OutputFormat != OutputImageFormat.Pdf);
        }

        public ConversionOptions BuildExecutionOptions(WatchProfile watchProfile, ImageItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(watchProfile);
            ArgumentNullException.ThrowIfNull(item);

            var options = BuildBaseOptions(watchProfile);

            if (item.IsAnimatedGif &&
                item.GifFrameCount > 1 &&
                options.OutputFormat != OutputImageFormat.Gif)
            {
                options.GifHandlingMode = GifHandlingMode.AllFrames;
                options.GifSpecificFrameIndex = 0;
                options.GifSpecificFrameSelections.Clear();
            }

            if (item.IsPdfDocument &&
                item.PdfPageCount > 1 &&
                options.OutputFormat != OutputImageFormat.Pdf)
            {
                options.PdfImageExportMode = PdfImageExportMode.AllPages;
                options.PdfPageIndex = 0;
                options.PdfPageSelections.Clear();
            }

            return options;
        }

        private static ConversionOptions BuildBaseOptions(WatchProfile watchProfile)
        {
            var options = watchProfile.JobDefinition?.ToConversionOptions() ?? new ConversionOptions();
            options.OutputDirectoryRule = OutputDirectoryRule.SpecificFolder;
            options.OutputDirectory = watchProfile.OutputDirectory;
            return options;
        }
    }
}
