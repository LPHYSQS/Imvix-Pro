using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PsdModule;
using System;
using System.IO;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly PsdRenderService _psdRenderService;

        public string ImportPsdText => T("ImportPsd");

        public bool IsSvgBackgroundToggleVisible => IsSvgPreviewVisible;

        public bool IsSvgBackgroundToggleEnabled => IsSvgPreviewVisible && !ShouldForceBackgroundColorEditorForSelectedImage();

        public bool IsSvgBackgroundRequiredHintVisible => IsSvgPreviewVisible && ShouldForceBackgroundColorEditorForSelectedImage();

        public bool SvgBackgroundToggleValue
        {
            get => ShouldForceBackgroundColorEditorForSelectedImage() || SvgUseBackground;
            set
            {
                if (ShouldForceBackgroundColorEditorForSelectedImage())
                {
                    OnPropertyChanged(nameof(SvgBackgroundToggleValue));
                    return;
                }

                SvgUseBackground = value;
            }
        }

        private bool IsSelectedBackgroundFillSourceWithoutNativeTransparencyOutput =>
            SelectedImage is not null &&
            IsBackgroundFillSource(SelectedImage);

        private bool IsBackgroundFillSource(ImageItemViewModel? image)
        {
            if (image is null)
            {
                return false;
            }

            if (image.Extension.Equals("SVG", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsIconSource(image.FilePath) || image.IsAnimatedGif)
            {
                return false;
            }

            return _imageAnalysisService.HasTransparency(image);
        }

        private bool IsBackgroundFillSource(string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsIconSource(filePath))
            {
                return false;
            }

            var importedItem = Images.FirstOrDefault(item => item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (importedItem is not null)
            {
                if (importedItem.IsAnimatedGif)
                {
                    return false;
                }

                return _imageAnalysisService.HasTransparency(importedItem);
            }

            if (PsdImportService.IsPsdFile(filePath))
            {
                return _psdRenderService.TryReadDocumentInfo(filePath, out var info, out _) &&
                       info is not null &&
                       info.HasTransparency;
            }

            return false;
        }

        private bool ShouldForceBackgroundColorEditorForSelectedImage()
        {
            return IsSelectedBackgroundFillSourceWithoutNativeTransparencyOutput &&
                   !ImageConversionService.OutputFormatSupportsTransparency(SelectedOutputFormat);
        }

        private bool ShouldForceBackgroundColorEditor(string filePath)
        {
            return IsBackgroundFillSource(filePath) &&
                   !ImageConversionService.OutputFormatSupportsTransparency(SelectedOutputFormat);
        }

        private string TranslateInputError(string? error)
        {
            return error switch
            {
                PsdImportService.UnsupportedPsdErrorCode => T("UnsupportedPsd"),
                null or "" => T("UnknownReason"),
                _ => error
            };
        }
    }
}
