namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public string DetailText => T("Detail");

        public string CurrentLanguageCode => _localizationService.CurrentLanguageCode;
    }
}
