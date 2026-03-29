using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;

namespace ImvixPro.ViewModels
{
    public sealed record MainWindowViewModelServices(
        SettingsService SettingsService,
        LocalizationService LocalizationService,
        ImageConversionService ImageConversionService,
        ImageAnalysisService ImageAnalysisService,
        ConversionPlanningService ConversionPlanningService,
        ConversionStatusSummaryService ConversionStatusSummaryService,
        ConversionTextPresenter ConversionTextPresenter,
        ConversionSummaryCoordinator ConversionSummaryCoordinator,
        WatchProfilePlanningService WatchProfilePlanningService,
        ConversionPipelineService ConversionPipelineService,
        ConversionHistoryService ConversionHistoryService,
        ConversionLogService ConversionLogService,
        FolderWatchService FolderWatchService,
        SystemIntegrationService SystemIntegrationService,
        PdfSecurityService PdfSecurityService,
        PdfImportService PdfImportService,
        PsdImportService PsdImportService,
        PdfRenderService PdfRenderService,
        PsdRenderService PsdRenderService,
        AppLogger Logger);
}
