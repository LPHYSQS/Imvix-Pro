using ImvixPro.Services;

namespace ImvixPro.ViewModels
{
    public sealed record FileDetailViewModelServices(
        FileDetailService DetailService,
        AppLogger Logger);
}
