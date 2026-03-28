using ImvixPro.Services;

namespace ImvixPro.Views
{
    public sealed record MainWindowServices(
        SettingsService SettingsService,
        AppLogger Logger,
        ImagePreviewWindowServices ImagePreviewWindowServices,
        FileDetailWindowServices FileDetailWindowServices);
}
