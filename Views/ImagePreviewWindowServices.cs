using ImvixPro.AI.Matting.Inference;
using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using System;

namespace ImvixPro.Views
{
    internal sealed record ImagePreviewWindowServices(
        PdfRenderService PdfRenderService,
        PreviewOcrToolController PreviewOcrToolController,
        PreviewQrToolController PreviewQrToolController,
        PreviewBarcodeToolController PreviewBarcodeToolController,
        Func<LocalizationService> CreateLocalizationService,
        AiImageEnhancementService AiImageEnhancementService,
        AiMattingService AiMattingService,
        AiPreviewComparisonService AiPreviewComparisonService,
        DisplayRefreshRateService DisplayRefreshRateService,
        ImageConversionService ImageConversionService,
        AppLogger Logger);
}
