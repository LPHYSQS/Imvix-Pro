using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImvixPro.Services
{
    public sealed class ConversionPlanningService
    {
        public ConversionPlan BuildPlan(IReadOnlyList<ImageItemViewModel> images, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentNullException.ThrowIfNull(options);

            var aiEligibleCount = options.AiEnhancementEnabled
                ? images.Count(AiImageEnhancementService.IsEligible)
                : 0;

            var expandsGifFrames = images.Any(image =>
                image.IsAnimatedGif &&
                options.OutputFormat != OutputImageFormat.Gif &&
                options.GifHandlingMode == GifHandlingMode.AllFrames);

            var expandsPdfPages = images.Any(image =>
                image.IsPdfDocument &&
                options.OutputFormat != OutputImageFormat.Pdf &&
                options.PdfImageExportMode == PdfImageExportMode.AllPages);

            var conversionWorkItems = images.Sum(image => ImageConversionService.EstimateWorkItemCount(image, options));

            return new ConversionPlan(
                options.AiEnhancementEnabled,
                options.AiEnhancementEnabled && aiEligibleCount > 0,
                images.Count,
                aiEligibleCount,
                expandsGifFrames,
                expandsPdfPages,
                conversionWorkItems + aiEligibleCount);
        }
    }
}
