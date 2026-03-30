using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public ObservableCollection<ImageItemViewModel> Images { get; } = [];

        public ObservableCollection<ConversionFailure> FailedConversions { get; } = [];

        public ObservableCollection<ConversionPreset> Presets { get; } = [];

        public IReadOnlyList<OutputImageFormat> OutputFormats { get; } = Enum.GetValues<OutputImageFormat>();

        public ObservableCollection<EnumOption<CompressionMode>> CompressionModes { get; } = [];

        public ObservableCollection<EnumOption<ResizeMode>> ResizeModes { get; } = [];

        public ObservableCollection<EnumOption<RenameMode>> RenameModes { get; } = [];

        public ObservableCollection<EnumOption<GifHandlingMode>> GifHandlingModes { get; } = [];

        public ObservableCollection<LanguageOption> Languages { get; } = [];

        public ObservableCollection<ThemeOption> Themes { get; } = [];

        public bool HasImages => Images.Count > 0;

        public bool IsEmpty => Images.Count == 0;

        public bool HasFailures => FailedConversions.Count > 0;

        public bool HasPresets => Presets.Count > 0;

        public bool IsSvgBackgroundColorVisible =>
            IsSvgPreviewVisible &&
            (SvgUseBackground || ShouldForceBackgroundColorEditorForSelectedImage());

        public bool IsIconPreviewVisible => SelectedImage is not null && IsIconSource(SelectedImage.FilePath);

        public bool IsIconTransparencyToggleVisible => IsIconPreviewVisible && ImageConversionService.OutputFormatSupportsTransparency(SelectedOutputFormat);

        public bool IsIconBackgroundColorVisible => IsIconPreviewVisible &&
                                                   (!ImageConversionService.OutputFormatSupportsTransparency(SelectedOutputFormat) || !IconUseTransparency);

        public bool IsQualityEditable => SelectedCompressionMode == CompressionMode.Custom;

        public bool IsResizeWidthVisible => SelectedResizeMode is ResizeMode.FixedWidth or ResizeMode.CustomSize;

        public bool IsResizeHeightVisible => SelectedResizeMode is ResizeMode.FixedHeight or ResizeMode.CustomSize;

        public bool IsResizePercentVisible => SelectedResizeMode == ResizeMode.ScalePercent;

        public bool IsRenamePrefixVisible => SelectedRenameMode == RenameMode.Prefix;

        public bool IsRenameSuffixVisible => SelectedRenameMode == RenameMode.Suffix;

        public bool IsRenameNumberVisible => SelectedRenameMode == RenameMode.AutoNumber;

        public bool IsGifPreviewVisible => SelectedImage is not null &&
                                           SelectedImage.Extension.Equals("GIF", StringComparison.OrdinalIgnoreCase) &&
                                           SelectedOutputFormat != OutputImageFormat.Gif;

        public bool IsGifTrimRangeVisible => SelectedImage is not null &&
                                             SelectedImage.IsAnimatedGif &&
                                             SelectedImage.Extension.Equals("GIF", StringComparison.OrdinalIgnoreCase) &&
                                             SelectedOutputFormat == OutputImageFormat.Gif;

        public bool IsSvgPreviewVisible => IsBackgroundFillSource(SelectedImage);

        private bool CanClearImages()
        {
            return Images.Count > 0 && !IsConverting;
        }

        private bool CanStartConversion()
        {
            return Images.Count > 0 && !IsConverting;
        }

        private bool CanSavePreset()
        {
            return !string.IsNullOrWhiteSpace(PresetNameInput);
        }

        private bool CanApplyPreset()
        {
            return SelectedPreset is not null;
        }

        private bool CanDeletePreset()
        {
            return SelectedPreset is not null;
        }

        private void OnImagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(IsEmpty));
            StartConversionCommand.NotifyCanExecuteChanged();
            ClearImagesCommand.NotifyCanExecuteChanged();
        }

        private void OnFailedConversionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasFailures));
        }

        private void OnPresetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasPresets));
            ApplyPresetCommand.NotifyCanExecuteChanged();
            DeletePresetCommand.NotifyCanExecuteChanged();
        }
    }
}
