using ImvixPro.AI.Matting.Inference;
using ImvixPro.Models;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using ImvixPro.ViewModels;
using ImvixPro.Views;
using System;

namespace ImvixPro.Services
{
    public static class AppServices
    {
        private static readonly Lazy<AppLogger> LoggerFactory = new(static () => new AppLogger());
        private static readonly Lazy<SettingsService> SettingsServiceFactory = new(static () => new SettingsService());
        private static readonly Lazy<LocalizationService> LocalizationServiceFactory = new(static () => new LocalizationService());
        private static readonly Lazy<ImageConversionService> ImageConversionServiceFactory = new(static () => new ImageConversionService());
        private static readonly Lazy<ImageAnalysisService> ImageAnalysisServiceFactory = new(static () => new ImageAnalysisService());
        private static readonly Lazy<AiImageEnhancementService> AiImageEnhancementServiceFactory = new(static () => new AiImageEnhancementService());
        private static readonly Lazy<AiMattingService> AiMattingServiceFactory = new(static () => new AiMattingService(LoggerFactory.Value));
        private static readonly Lazy<AiPreviewComparisonService> AiPreviewComparisonServiceFactory = new(static () => new AiPreviewComparisonService());
        private static readonly Lazy<DisplayRefreshRateService> DisplayRefreshRateServiceFactory = new(static () => new DisplayRefreshRateService());
        private static readonly Lazy<PreviewOcrService> PreviewOcrServiceFactory = new(static () => new PreviewOcrService());
        private static readonly Lazy<ConversionPipelineService> ConversionPipelineServiceFactory = new(static () => new ConversionPipelineService(ImageConversionServiceFactory.Value, AiImageEnhancementServiceFactory.Value, LoggerFactory.Value));
        private static readonly Lazy<ConversionHistoryService> ConversionHistoryServiceFactory = new(static () => new ConversionHistoryService());
        private static readonly Lazy<ConversionLogService> ConversionLogServiceFactory = new(static () => new ConversionLogService());
        private static readonly Lazy<FolderWatchService> FolderWatchServiceFactory = new(static () => new FolderWatchService());
        private static readonly Lazy<SystemIntegrationService> SystemIntegrationServiceFactory = new(static () => new SystemIntegrationService());
        private static readonly Lazy<PdfSecurityService> PdfSecurityServiceFactory = new(static () => new PdfSecurityService());
        private static readonly Lazy<PdfImportService> PdfImportServiceFactory = new(static () => new PdfImportService());
        private static readonly Lazy<PsdImportService> PsdImportServiceFactory = new(static () => new PsdImportService());
        private static readonly Lazy<PdfRenderService> PdfRenderServiceFactory = new(static () => new PdfRenderService());
        private static readonly Lazy<PsdRenderService> PsdRenderServiceFactory = new(static () => new PsdRenderService());
        private static readonly Lazy<PsdDetailService> PsdDetailServiceFactory = new(static () => new PsdDetailService());

        public static AppLogger Logger => LoggerFactory.Value;

        public static SettingsService SettingsService => SettingsServiceFactory.Value;

        public static LocalizationService LocalizationService => LocalizationServiceFactory.Value;

        public static ImageConversionService ImageConversionService => ImageConversionServiceFactory.Value;

        public static ImageAnalysisService ImageAnalysisService => ImageAnalysisServiceFactory.Value;

        public static AiImageEnhancementService AiImageEnhancementService => AiImageEnhancementServiceFactory.Value;

        public static AiMattingService AiMattingService => AiMattingServiceFactory.Value;

        public static AiPreviewComparisonService AiPreviewComparisonService => AiPreviewComparisonServiceFactory.Value;

        public static DisplayRefreshRateService DisplayRefreshRateService => DisplayRefreshRateServiceFactory.Value;

        public static PreviewOcrService PreviewOcrService => PreviewOcrServiceFactory.Value;

        public static ConversionPipelineService ConversionPipelineService => ConversionPipelineServiceFactory.Value;

        public static ConversionHistoryService ConversionHistoryService => ConversionHistoryServiceFactory.Value;

        public static ConversionLogService ConversionLogService => ConversionLogServiceFactory.Value;

        public static FolderWatchService FolderWatchService => FolderWatchServiceFactory.Value;

        public static SystemIntegrationService SystemIntegrationService => SystemIntegrationServiceFactory.Value;

        public static PdfSecurityService PdfSecurityService => PdfSecurityServiceFactory.Value;

        public static PdfImportService PdfImportService => PdfImportServiceFactory.Value;

        public static PsdImportService PsdImportService => PsdImportServiceFactory.Value;

        public static PdfRenderService PdfRenderService => PdfRenderServiceFactory.Value;

        public static PsdRenderService PsdRenderService => PsdRenderServiceFactory.Value;

        public static PsdDetailService PsdDetailService => PsdDetailServiceFactory.Value;

        public static MainWindowViewModelServices CreateMainWindowViewModelServices()
        {
            return new MainWindowViewModelServices(
                SettingsService,
                LocalizationService,
                ImageConversionService,
                ImageAnalysisService,
                ConversionPipelineService,
                ConversionHistoryService,
                ConversionLogService,
                FolderWatchService,
                SystemIntegrationService,
                PdfSecurityService,
                PdfImportService,
                PsdImportService,
                PdfRenderService,
                PsdRenderService,
                Logger);
        }

        public static ImagePreviewWindowServices CreateImagePreviewWindowServices()
        {
            return new ImagePreviewWindowServices(
                PdfRenderService,
                PreviewOcrService,
                static () => new LocalizationService(),
                AiImageEnhancementService,
                AiMattingService,
                AiPreviewComparisonService,
                DisplayRefreshRateService,
                ImageConversionService,
                Logger);
        }

        public static FileDetailWindowServices CreateFileDetailWindowServices()
        {
            return new FileDetailWindowServices(CreateFileDetailViewModel);
        }

        public static MainWindowServices CreateMainWindowServices()
        {
            return new MainWindowServices(
                SettingsService,
                Logger,
                CreateImagePreviewWindowServices(),
                CreateFileDetailWindowServices());
        }

        public static LocalizationService CreateLocalizationService(string? languageCode = null)
        {
            var localizationService = new LocalizationService();
            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                localizationService.SetLanguage(languageCode);
            }

            return localizationService;
        }

        public static FileDetailService CreateFileDetailService(string languageCode)
        {
            return new FileDetailService(
                languageCode,
                CreateLocalizationService(languageCode),
                PdfRenderService,
                PdfSecurityService,
                PsdRenderService,
                PsdDetailService,
                ImageAnalysisService,
                Logger);
        }

        public static FileDetailViewModelServices CreateFileDetailViewModelServices(string languageCode)
        {
            return new FileDetailViewModelServices(
                CreateFileDetailService(languageCode),
                Logger);
        }

        public static FileDetailViewModel CreateFileDetailViewModel(ImageItemViewModel item, string languageCode)
        {
            return new FileDetailViewModel(
                item,
                CreateFileDetailViewModelServices(languageCode));
        }
    }
}
