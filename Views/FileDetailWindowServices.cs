using ImvixPro.Models;
using ImvixPro.ViewModels;
using System;

namespace ImvixPro.Views
{
    public sealed record FileDetailWindowServices(
        Func<ImageItemViewModel, string, FileDetailViewModel> CreateViewModel);
}
