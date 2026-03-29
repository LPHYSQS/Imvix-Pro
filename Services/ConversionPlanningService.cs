using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImvixPro.Services
{
    public sealed class ConversionPlanningService
    {
        private const int GifPdfLargeFrameWarningThreshold = 500;
        private readonly ImageAnalysisService _imageAnalysisService;

        public ConversionPlanningService(ImageAnalysisService imageAnalysisService)
        {
            _imageAnalysisService = imageAnalysisService ?? throw new ArgumentNullException(nameof(imageAnalysisService));
        }

        public ConversionPlan BuildPlan(IReadOnlyList<ImageItemViewModel> images, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentNullException.ThrowIfNull(options);

            var ruleSummary = BuildRuleSummary(images, options);
            var diagnostics = BuildDiagnosticsSummary(images, options, ruleSummary);
            var conversionWorkItems = images.Sum(image => ImageConversionService.EstimateWorkItemCount(image, options));
            var aiEligibleCount = ruleSummary.Ai.EligibleInputCount.GetValueOrDefault();

            return new ConversionPlan(
                ruleSummary,
                diagnostics,
                conversionWorkItems,
                conversionWorkItems + aiEligibleCount);
        }

        public ConversionRuleSummary BuildRuleSummary(IReadOnlyList<ImageItemViewModel> images, ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentNullException.ThrowIfNull(options);

            var aiEligibleCount = options.AiEnhancementEnabled
                ? images.Count(AiImageEnhancementService.IsEligible)
                : 0;

            var gifFrameExpansionInputCount = images.Count(image =>
                image.IsAnimatedGif &&
                options.OutputFormat != OutputImageFormat.Gif &&
                options.GifHandlingMode == GifHandlingMode.AllFrames);

            var pdfPageExpansionInputCount = images.Count(image =>
                image.IsPdfDocument &&
                ImageConversionService.EstimateWorkItemCount(image, options) > 1);

            var forcedBackgroundFillInputCount =
                ImageConversionService.OutputFormatSupportsTransparency(options.OutputFormat)
                    ? 0
                    : images.Count(RequiresForcedBackgroundFill);

            return new ConversionRuleSummary(
                new AiRuleSummary(
                    options.AiEnhancementEnabled,
                    skipsUnsupportedInputs: options.AiEnhancementEnabled,
                    totalInputCount: images.Count,
                    eligibleInputCount: aiEligibleCount),
                new ExpansionRuleSummary(
                    expandsGifFrames: gifFrameExpansionInputCount > 0,
                    gifFrameExpansionInputCount,
                    expandsPdfPages: pdfPageExpansionInputCount > 0,
                    pdfPageExpansionInputCount),
                forcedBackgroundFillInputCount);
        }

        public ConversionRuleSummary BuildWatchRuleSummary(ConversionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new ConversionRuleSummary(
                new AiRuleSummary(
                    options.AiEnhancementEnabled,
                    skipsUnsupportedInputs: options.AiEnhancementEnabled),
                new ExpansionRuleSummary(
                    expandsGifFrames: options.OutputFormat != OutputImageFormat.Gif,
                    gifFrameExpansionInputCount: null,
                    expandsPdfPages: options.OutputFormat != OutputImageFormat.Pdf,
                    pdfPageExpansionInputCount: null),
                forcedBackgroundFillInputCount: null);
        }

        public void ApplyWatchScenarioRules(ConversionOptions options, ImageItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(item);

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
        }

        private bool RequiresForcedBackgroundFill(ImageItemViewModel image)
        {
            return !image.IsPdfDocument &&
                   _imageAnalysisService.HasTransparency(image);
        }

        private static bool HasHighCompressionRisk(ConversionOptions options)
        {
            return options.OutputFormat is OutputImageFormat.Jpeg or OutputImageFormat.Webp &&
                   (options.CompressionMode == CompressionMode.HighCompression ||
                    (options.CompressionMode == CompressionMode.Custom && options.Quality <= 45));
        }

        private static int GetLargeGifPdfFrameCount(IReadOnlyList<ImageItemViewModel> images, ConversionOptions options)
        {
            if (options.OutputFormat != OutputImageFormat.Pdf ||
                options.GifHandlingMode != GifHandlingMode.AllFrames)
            {
                return 0;
            }

            return images
                .Where(image => image.IsAnimatedGif && image.GifFrameCount > GifPdfLargeFrameWarningThreshold)
                .Select(image => image.GifFrameCount)
                .DefaultIfEmpty(0)
                .Max();
        }

        private static int CountLockedPdfInputs(IReadOnlyList<ImageItemViewModel> images)
        {
            return images.Count(image => image.NeedsPdfUnlock);
        }

        private static ConversionDiagnosticsSummary BuildDiagnosticsSummary(
            IReadOnlyList<ImageItemViewModel> images,
            ConversionOptions options,
            ConversionRuleSummary ruleSummary)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(ruleSummary);

            return new ConversionDiagnosticsSummary(
                HasHighCompressionRisk(options),
                GetLargeGifPdfFrameCount(images, options),
                CountLockedPdfInputs(images),
                new EstimateDisclaimerSummary(
                    ruleSummary.Ai.UsesAiPreprocessing,
                    ruleSummary.Expansion.HasExpandedOutputs));
        }
    }
}
