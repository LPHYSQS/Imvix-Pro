using ImvixPro.AI.Matting.Inference;
using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using System;

namespace ImvixPro.Views
{
    public sealed record ImagePreviewWindowServices(
        PdfRenderService PdfRenderService,
        PreviewOcrService PreviewOcrService,
        Func<LocalizationService> CreateLocalizationService,
        AiImageEnhancementService AiImageEnhancementService,
        AiMattingService AiMattingService,
        AiPreviewComparisonService AiPreviewComparisonService,
        DisplayRefreshRateService DisplayRefreshRateService,
        ImageConversionService ImageConversionService,
        AppLogger Logger);
}
