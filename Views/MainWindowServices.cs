using ImvixPro.Services;

namespace ImvixPro.Views
{
    internal sealed record MainWindowServices(
        SettingsService SettingsService,
        AppLogger Logger,
        MainWindowShellCoordinator ShellCoordinator);
}
