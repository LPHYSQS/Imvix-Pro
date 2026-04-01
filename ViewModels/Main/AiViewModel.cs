using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImvixPro.AI.Inpainting.Models;
using ImvixPro.AI.Matting.Inference;
using ImvixPro.AI.Matting.Models;
using ImvixPro.AI.Matting.UI;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public ObservableCollection<EnumOption<int>> AiEnhancementScales { get; } = [];

        public ObservableCollection<AiEnhancementModelOption> AiEnhancementModels { get; } = [];

        public ObservableCollection<EnumOption<AiExecutionMode>> AiExecutionModes { get; } = [];

        public ObservableCollection<AiMattingModelOption> AiMattingModels { get; } = [];

        public ObservableCollection<EnumOption<AiMattingDevice>> AiMattingDevices { get; } = [];

        public ObservableCollection<EnumOption<OutputImageFormat>> AiMattingOutputFormats { get; } = [];

        public ObservableCollection<EnumOption<AiMattingBackgroundMode>> AiMattingBackgroundModes { get; } = [];

        public ObservableCollection<EnumOption<AiMattingResolutionMode>> AiMattingResolutionModes { get; } = [];

        public string AiEnhancementToggleHintText => T("AiEnhancementToggleHint");

        public string AiEnhancementTabText => T("AiEnhancementTab");

        public string AiEnhancementPanelTitleText => T("AiEnhancementPanelTitle");

        public string AiEnhancementDescriptionText => T("AiEnhancementDescription");

        public string AiScaleText => T("AiScale");

        public string AiEnhancementScaleDisplayText => string.Create(CultureInfo.InvariantCulture, $"{AiEnhancementScale}x");

        public string AiEnhancementScaleStepsText => string.Join("  ", AiEnhancementScales.Select(option => option.DisplayName));

        public string AiModelText => T("AiModel");

        public string AiExecutionModeText => T("AiExecutionMode");

        public string AiExecutionHintText => T("AiExecutionHint");

        public string AiInputSupportHintText => T("AiInputSupportHint");

        public string AiEnhancementPerformanceHintText => T("AiScalePerformanceHint");

        public bool HasSelectedAiEnhancementModelInfo => SelectedAiEnhancementModelOption is not null;

        public string SelectedAiEnhancementModelSeriesText => SelectedAiEnhancementModelOption is null
            ? string.Empty
            : FormatT("AiModelSelectedSeriesTemplate", SelectedAiEnhancementModelOption.SeriesDisplayName);

        public string SelectedAiEnhancementModelDescriptionText => SelectedAiEnhancementModelOption?.Description ?? string.Empty;

        public bool HasSelectedAiEnhancementModelRestriction => SelectedAiEnhancementModelOption?.IsCommercialUseRestricted == true;

        public string SelectedAiEnhancementModelRestrictionText => HasSelectedAiEnhancementModelRestriction
            ? T("AiModelSelectedRestriction_NonCommercial")
            : string.Empty;

        public bool HasAiEnhancementModelFallbackWarning => !string.IsNullOrWhiteSpace(AiEnhancementModelFallbackWarningText);

        public bool HasAiEnhancementPerformanceHint => AiEnhancementEnabled && AiEnhancementScale > 5;

        public string AiMattingPanelTitleText => T("AiMattingPanelTitle");

        public string AiMattingDescriptionText => T("AiMattingDescription");

        public string AiMattingPreviewOnlyHintText => T("AiMattingPreviewOnlyHint");

        public string AiMattingModelText => T("AiMattingModel");

        public string AiMattingDeviceText => T("AiMattingDevice");

        public string AiMattingOutputFormatText => T("AiMattingOutputFormat");

        public string AiMattingBackgroundText => T("AiMattingBackground");

        public string AiMattingBackgroundColorText => T("AiMattingBackgroundColor");

        public string AiMattingEdgeOptimizationText => T("AiMattingEdgeOptimization");

        public string AiMattingEdgeStrengthText => T("AiMattingEdgeStrength");

        public string AiMattingEdgeStrengthDisplayText => string.Create(CultureInfo.InvariantCulture, $"{AiMattingEdgeOptimizationStrength}%");

        public string AiMattingResolutionStrategyText => T("AiMattingResolutionStrategy");

        public bool HasSelectedAiMattingModelInfo => SelectedAiMattingModelOption is not null;

        public string SelectedAiMattingModelDescriptionText => SelectedAiMattingModelOption?.Description ?? string.Empty;

        public bool IsAiMattingBackgroundColorVisible => SelectedAiMattingBackgroundMode == AiMattingBackgroundMode.SolidColor;

        public string AiEraserPanelTitleText => T("AiEraserPanelTitle");

        public string AiEraserDescriptionText => T("AiEraserDescription");

        public string AiEraserPreviewOnlyHintText => T("AiEraserPreviewOnlyHint");

        public string AiEraserDefaultBrushSizeText => T("AiEraserDefaultBrushSize");

        public string AiEraserDefaultBrushSizeHintText => T("AiEraserDefaultBrushSizeHint");

        public string AiEraserDefaultBrushSizeDisplayText => string.Create(CultureInfo.InvariantCulture, $"{AiEraserDefaultBrushSize}px");

        public string AiEraserMaskExpansionText => T("AiEraserMaskExpansion");

        public string AiEraserMaskExpansionHintText => T("AiEraserMaskExpansionHint");

        public string AiEraserMaskExpansionDisplayText => string.Create(CultureInfo.InvariantCulture, $"{AiEraserMaskExpansionPixels}px");

        public string AiEraserEdgeBlendText => T("AiEraserEdgeBlend");

        public string AiEraserEdgeBlendHintText => T("AiEraserEdgeBlendHint");

        public string AiEraserEdgeBlendDisplayText => string.Create(CultureInfo.InvariantCulture, $"{AiEraserEdgeBlendStrength}%");

        [ObservableProperty]
        private bool aiPanelEnabled;

        [ObservableProperty]
        private bool aiEnhancementEnabled;

        [ObservableProperty]
        private int aiEnhancementScale = 2;

        [ObservableProperty]
        private AiEnhancementModel selectedAiEnhancementModel = AiEnhancementModel.UpscaylStandard;

        [ObservableProperty]
        private AiExecutionMode selectedAiExecutionMode = AiExecutionMode.Auto;

        [ObservableProperty]
        private EnumOption<int>? selectedAiEnhancementScaleOption;

        [ObservableProperty]
        private AiEnhancementModelOption? selectedAiEnhancementModelOption;

        [ObservableProperty]
        private EnumOption<AiExecutionMode>? selectedAiExecutionModeOption;

        [ObservableProperty]
        private string aiEnhancementModelFallbackWarningText = string.Empty;

        [ObservableProperty]
        private AiMattingModel selectedAiMattingModel = AiMattingModel.GeneralClassic;

        [ObservableProperty]
        private AiMattingDevice selectedAiMattingDevice = AiMattingDevice.Cpu;

        [ObservableProperty]
        private OutputImageFormat selectedAiMattingOutputFormat = OutputImageFormat.Png;

        [ObservableProperty]
        private AiMattingBackgroundMode selectedAiMattingBackgroundMode = AiMattingBackgroundMode.Transparent;

        [ObservableProperty]
        private string aiMattingBackgroundColor = "#FFFFFFFF";

        [ObservableProperty]
        private bool aiMattingEdgeOptimizationEnabled = true;

        [ObservableProperty]
        private int aiMattingEdgeOptimizationStrength = 35;

        [ObservableProperty]
        private AiMattingResolutionMode selectedAiMattingResolutionMode = AiMattingResolutionMode.Original;

        [ObservableProperty]
        private AiMattingModelOption? selectedAiMattingModelOption;

        [ObservableProperty]
        private EnumOption<AiMattingDevice>? selectedAiMattingDeviceOption;

        [ObservableProperty]
        private EnumOption<OutputImageFormat>? selectedAiMattingOutputFormatOption;

        [ObservableProperty]
        private EnumOption<AiMattingBackgroundMode>? selectedAiMattingBackgroundModeOption;

        [ObservableProperty]
        private EnumOption<AiMattingResolutionMode>? selectedAiMattingResolutionModeOption;

        [ObservableProperty]
        private int aiEraserDefaultBrushSize = AiEraserSettings.DefaultBrushSize;

        [ObservableProperty]
        private int aiEraserMaskExpansionPixels = AiEraserSettings.DefaultMaskExpansionPixels;

        [ObservableProperty]
        private int aiEraserEdgeBlendStrength = AiEraserSettings.DefaultEdgeBlendStrength;

        private void InitializeAiFeatures(ApplicationPreferences preferences, PreviewToolState previewToolState)
        {
            AiPanelEnabled = true;
            AiEnhancementEnabled = preferences.AiEnhancementEnabled;
            AiEnhancementScale = AiEnhancementModelCatalog.NormalizeRequestedOutputScale(preferences.DefaultAiEnhancementScale);
            SelectedAiEnhancementModel = preferences.DefaultAiEnhancementModel;
            SelectedAiExecutionMode = preferences.DefaultAiExecutionMode;

            SelectedAiMattingModel = previewToolState.AiMattingModel;
            SelectedAiMattingDevice = previewToolState.AiMattingDevice;
            SelectedAiMattingOutputFormat = AiMattingFormatCatalog.Normalize(previewToolState.AiMattingOutputFormat);
            SelectedAiMattingBackgroundMode = previewToolState.AiMattingBackgroundMode;
            AiMattingBackgroundColor = string.IsNullOrWhiteSpace(previewToolState.AiMattingBackgroundColor)
                ? "#FFFFFFFF"
                : previewToolState.AiMattingBackgroundColor;
            AiMattingEdgeOptimizationEnabled = previewToolState.AiMattingEdgeOptimizationEnabled;
            AiMattingEdgeOptimizationStrength = Math.Clamp(previewToolState.AiMattingEdgeOptimizationStrength, 0, 100);
            SelectedAiMattingResolutionMode = previewToolState.AiMattingResolutionMode;
            AiEraserDefaultBrushSize = AiEraserSettings.NormalizeBrushSize(previewToolState.AiEraserDefaultBrushSize);
            AiEraserMaskExpansionPixels = AiEraserSettings.NormalizeMaskExpansionPixels(previewToolState.AiEraserMaskExpansionPixels);
            AiEraserEdgeBlendStrength = AiEraserSettings.NormalizeEdgeBlendStrength(previewToolState.AiEraserEdgeBlendStrength);
        }

        [RelayCommand]
        private void OpenAiEnhancementPanel()
        {
            AiPanelEnabled = true;
            RightPanelTabIndex = 2;
        }

        private void RefreshAiOptionCollections()
        {
            RebuildEnumOptions(
                AiEnhancementScales,
                Enumerable.Range(
                        AiEnhancementModelCatalog.MinRequestedOutputScale,
                        AiEnhancementModelCatalog.MaxRequestedOutputScale - AiEnhancementModelCatalog.MinRequestedOutputScale + 1)
                    .ToArray(),
                value => $"{value}x");

            AiEnhancementModels.Clear();
            foreach (var definition in AiEnhancementModelCatalog.Definitions)
            {
                var seriesDisplayName = T(definition.SeriesTranslationKey);
                var displayName = T(definition.DisplayTranslationKey);
                var description = T(definition.DescriptionTranslationKey);
                AiEnhancementModels.Add(new AiEnhancementModelOption(
                    definition.Model,
                    seriesDisplayName,
                    displayName,
                    description,
                    definition.IsCommercialUseRestricted));
            }

            RebuildEnumOptions(
                AiExecutionModes,
                Enum.GetValues<AiExecutionMode>(),
                value => T($"AiExecutionMode_{value}"));

            AiMattingModels.Clear();
            foreach (var definition in AiMattingModelCatalog.GetDefinitions())
            {
                AiMattingModels.Add(new AiMattingModelOption(
                    definition.Model,
                    T(definition.DisplayTranslationKey),
                    T(definition.DescriptionTranslationKey),
                    definition.IsOptional));
            }

            RebuildEnumOptions(
                AiMattingDevices,
                Enum.GetValues<AiMattingDevice>(),
                value => T($"AiMattingDevice_{value}"));

            RebuildEnumOptions(
                AiMattingOutputFormats,
                AiMattingFormatCatalog.SupportedTransparentFormats.ToArray(),
                AiMattingFormatCatalog.BuildDisplayName);

            RebuildEnumOptions(
                AiMattingBackgroundModes,
                Enum.GetValues<AiMattingBackgroundMode>(),
                value => T($"AiMattingBackground_{value}"));

            RebuildEnumOptions(
                AiMattingResolutionModes,
                Enum.GetValues<AiMattingResolutionMode>(),
                value => T($"AiMattingResolution_{value}"));

            SelectedAiEnhancementScaleOption = AiEnhancementScales.FirstOrDefault(option => option.Value == AiEnhancementScale);
            SelectedAiEnhancementModelOption = AiEnhancementModels.FirstOrDefault(option => option.Value == SelectedAiEnhancementModel);
            SelectedAiExecutionModeOption = AiExecutionModes.FirstOrDefault(option => option.Value == SelectedAiExecutionMode);

            SelectedAiMattingModelOption = AiMattingModels.FirstOrDefault(option => option.Value == SelectedAiMattingModel);
            SelectedAiMattingDeviceOption = AiMattingDevices.FirstOrDefault(option => option.Value == SelectedAiMattingDevice);
            SelectedAiMattingOutputFormatOption = AiMattingOutputFormats.FirstOrDefault(option => option.Value == SelectedAiMattingOutputFormat);
            SelectedAiMattingBackgroundModeOption = AiMattingBackgroundModes.FirstOrDefault(option => option.Value == SelectedAiMattingBackgroundMode);
            SelectedAiMattingResolutionModeOption = AiMattingResolutionModes.FirstOrDefault(option => option.Value == SelectedAiMattingResolutionMode);

            RefreshSelectedAiModelState();
            RefreshSelectedAiMattingModelState();
            OnPropertyChanged(nameof(AiEnhancementScaleStepsText));
            OnPropertyChanged(nameof(AiMattingEdgeStrengthDisplayText));
            OnPropertyChanged(nameof(IsAiMattingBackgroundColorVisible));
        }

        private int GetAiUnsupportedInputCount(System.Collections.Generic.IReadOnlyList<ImageItemViewModel> images)
        {
            if (!AiEnhancementEnabled)
            {
                return 0;
            }

            return images.Count(image => !AiImageEnhancementService.IsEligible(image));
        }

        private void RefreshLocalizedPropertiesAi()
        {
            OnPropertyChanged(nameof(AiEnhancementToggleHintText));
            OnPropertyChanged(nameof(AiEnhancementTabText));
            OnPropertyChanged(nameof(AiEnhancementPanelTitleText));
            OnPropertyChanged(nameof(AiEnhancementDescriptionText));
            OnPropertyChanged(nameof(AiScaleText));
            OnPropertyChanged(nameof(AiEnhancementScaleDisplayText));
            OnPropertyChanged(nameof(AiEnhancementScaleStepsText));
            OnPropertyChanged(nameof(AiModelText));
            OnPropertyChanged(nameof(AiExecutionModeText));
            OnPropertyChanged(nameof(AiExecutionHintText));
            OnPropertyChanged(nameof(AiInputSupportHintText));
            OnPropertyChanged(nameof(AiEnhancementPerformanceHintText));
            OnPropertyChanged(nameof(ConversionFeedbackTitleText));
            OnPropertyChanged(nameof(ConversionFeedbackDescriptionText));
            OnPropertyChanged(nameof(ConversionFeedbackHardwareHintText));
            OnPropertyChanged(nameof(ConversionFeedbackCloseHintText));
            OnPropertyChanged(nameof(HasSelectedAiEnhancementModelInfo));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelSeriesText));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelDescriptionText));
            OnPropertyChanged(nameof(HasSelectedAiEnhancementModelRestriction));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelRestrictionText));
            OnPropertyChanged(nameof(HasAiEnhancementModelFallbackWarning));
            OnPropertyChanged(nameof(HasAiEnhancementPerformanceHint));
            OnPropertyChanged(nameof(ShowAiConversionFeedback));
            OnPropertyChanged(nameof(AiMattingPanelTitleText));
            OnPropertyChanged(nameof(AiMattingDescriptionText));
            OnPropertyChanged(nameof(AiMattingPreviewOnlyHintText));
            OnPropertyChanged(nameof(AiMattingModelText));
            OnPropertyChanged(nameof(AiMattingDeviceText));
            OnPropertyChanged(nameof(AiMattingOutputFormatText));
            OnPropertyChanged(nameof(AiMattingBackgroundText));
            OnPropertyChanged(nameof(AiMattingBackgroundColorText));
            OnPropertyChanged(nameof(AiMattingEdgeOptimizationText));
            OnPropertyChanged(nameof(AiMattingEdgeStrengthText));
            OnPropertyChanged(nameof(AiMattingEdgeStrengthDisplayText));
            OnPropertyChanged(nameof(AiMattingResolutionStrategyText));
            OnPropertyChanged(nameof(HasSelectedAiMattingModelInfo));
            OnPropertyChanged(nameof(SelectedAiMattingModelDescriptionText));
            OnPropertyChanged(nameof(IsAiMattingBackgroundColorVisible));
            OnPropertyChanged(nameof(AiEraserPanelTitleText));
            OnPropertyChanged(nameof(AiEraserDescriptionText));
            OnPropertyChanged(nameof(AiEraserPreviewOnlyHintText));
            OnPropertyChanged(nameof(AiEraserDefaultBrushSizeText));
            OnPropertyChanged(nameof(AiEraserDefaultBrushSizeHintText));
            OnPropertyChanged(nameof(AiEraserDefaultBrushSizeDisplayText));
            OnPropertyChanged(nameof(AiEraserMaskExpansionText));
            OnPropertyChanged(nameof(AiEraserMaskExpansionHintText));
            OnPropertyChanged(nameof(AiEraserMaskExpansionDisplayText));
            OnPropertyChanged(nameof(AiEraserEdgeBlendText));
            OnPropertyChanged(nameof(AiEraserEdgeBlendHintText));
            OnPropertyChanged(nameof(AiEraserEdgeBlendDisplayText));
            RefreshAiOptionCollections();
        }

        partial void OnAiPanelEnabledChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            if (!value)
            {
                AiPanelEnabled = true;
                return;
            }

            PersistSettings();
        }

        partial void OnAiEnhancementEnabledChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            OnPropertyChanged(nameof(ShowAiConversionFeedback));
            RefreshAiModelAvailabilityWarning();
            OnPropertyChanged(nameof(HasAiEnhancementPerformanceHint));
            PersistSettings();
        }

        partial void OnAiEnhancementScaleChanged(int value)
        {
            var normalized = AiEnhancementModelCatalog.NormalizeRequestedOutputScale(value);
            if (value != normalized)
            {
                AiEnhancementScale = normalized;
                return;
            }

            var option = AiEnhancementScales.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiEnhancementScaleOption, option))
            {
                SelectedAiEnhancementScaleOption = option;
            }

            RefreshAiModelAvailabilityWarning();
            OnPropertyChanged(nameof(AiEnhancementScaleDisplayText));
            OnPropertyChanged(nameof(HasAiEnhancementPerformanceHint));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiEnhancementScaleOptionChanged(EnumOption<int>? value)
        {
            if (value is not null && AiEnhancementScale != value.Value)
            {
                AiEnhancementScale = value.Value;
            }
        }

        partial void OnSelectedAiEnhancementModelChanged(AiEnhancementModel value)
        {
            var option = AiEnhancementModels.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiEnhancementModelOption, option))
            {
                SelectedAiEnhancementModelOption = option;
            }

            RefreshSelectedAiModelState();
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiEnhancementModelOptionChanged(AiEnhancementModelOption? value)
        {
            if (value is not null && SelectedAiEnhancementModel != value.Value)
            {
                SelectedAiEnhancementModel = value.Value;
            }
        }

        partial void OnSelectedAiExecutionModeChanged(AiExecutionMode value)
        {
            var option = AiExecutionModes.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiExecutionModeOption, option))
            {
                SelectedAiExecutionModeOption = option;
            }

            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiExecutionModeOptionChanged(EnumOption<AiExecutionMode>? value)
        {
            if (value is not null && SelectedAiExecutionMode != value.Value)
            {
                SelectedAiExecutionMode = value.Value;
            }
        }

        partial void OnAiEnhancementModelFallbackWarningTextChanged(string value)
        {
            OnPropertyChanged(nameof(HasAiEnhancementModelFallbackWarning));
        }

        partial void OnSelectedAiMattingModelChanged(AiMattingModel value)
        {
            var option = AiMattingModels.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiMattingModelOption, option))
            {
                SelectedAiMattingModelOption = option;
            }

            RefreshSelectedAiMattingModelState();
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingModelOptionChanged(AiMattingModelOption? value)
        {
            if (value is not null && SelectedAiMattingModel != value.Value)
            {
                SelectedAiMattingModel = value.Value;
            }
        }

        partial void OnSelectedAiMattingDeviceChanged(AiMattingDevice value)
        {
            var option = AiMattingDevices.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiMattingDeviceOption, option))
            {
                SelectedAiMattingDeviceOption = option;
            }

            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingDeviceOptionChanged(EnumOption<AiMattingDevice>? value)
        {
            if (value is not null && SelectedAiMattingDevice != value.Value)
            {
                SelectedAiMattingDevice = value.Value;
            }
        }

        partial void OnSelectedAiMattingOutputFormatChanged(OutputImageFormat value)
        {
            var normalized = AiMattingFormatCatalog.Normalize(value);
            if (value != normalized)
            {
                SelectedAiMattingOutputFormat = normalized;
                return;
            }

            var option = AiMattingOutputFormats.FirstOrDefault(item => item.Value == normalized);
            if (!ReferenceEquals(SelectedAiMattingOutputFormatOption, option))
            {
                SelectedAiMattingOutputFormatOption = option;
            }

            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingOutputFormatOptionChanged(EnumOption<OutputImageFormat>? value)
        {
            if (value is not null && SelectedAiMattingOutputFormat != value.Value)
            {
                SelectedAiMattingOutputFormat = value.Value;
            }
        }

        partial void OnSelectedAiMattingBackgroundModeChanged(AiMattingBackgroundMode value)
        {
            var option = AiMattingBackgroundModes.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiMattingBackgroundModeOption, option))
            {
                SelectedAiMattingBackgroundModeOption = option;
            }

            OnPropertyChanged(nameof(IsAiMattingBackgroundColorVisible));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingBackgroundModeOptionChanged(EnumOption<AiMattingBackgroundMode>? value)
        {
            if (value is not null && SelectedAiMattingBackgroundMode != value.Value)
            {
                SelectedAiMattingBackgroundMode = value.Value;
            }
        }

        partial void OnAiMattingBackgroundColorChanged(string value)
        {
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnAiMattingEdgeOptimizationEnabledChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnAiMattingEdgeOptimizationStrengthChanged(int value)
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (value != normalized)
            {
                AiMattingEdgeOptimizationStrength = normalized;
                return;
            }

            OnPropertyChanged(nameof(AiMattingEdgeStrengthDisplayText));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingResolutionModeChanged(AiMattingResolutionMode value)
        {
            var option = AiMattingResolutionModes.FirstOrDefault(item => item.Value == value);
            if (!ReferenceEquals(SelectedAiMattingResolutionModeOption, option))
            {
                SelectedAiMattingResolutionModeOption = option;
            }

            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnSelectedAiMattingResolutionModeOptionChanged(EnumOption<AiMattingResolutionMode>? value)
        {
            if (value is not null && SelectedAiMattingResolutionMode != value.Value)
            {
                SelectedAiMattingResolutionMode = value.Value;
            }
        }

        partial void OnAiEraserDefaultBrushSizeChanged(int value)
        {
            var normalized = AiEraserSettings.NormalizeBrushSize(value);
            if (value != normalized)
            {
                AiEraserDefaultBrushSize = normalized;
                return;
            }

            OnPropertyChanged(nameof(AiEraserDefaultBrushSizeDisplayText));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnAiEraserMaskExpansionPixelsChanged(int value)
        {
            var normalized = AiEraserSettings.NormalizeMaskExpansionPixels(value);
            if (value != normalized)
            {
                AiEraserMaskExpansionPixels = normalized;
                return;
            }

            OnPropertyChanged(nameof(AiEraserMaskExpansionDisplayText));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        partial void OnAiEraserEdgeBlendStrengthChanged(int value)
        {
            var normalized = AiEraserSettings.NormalizeEdgeBlendStrength(value);
            if (value != normalized)
            {
                AiEraserEdgeBlendStrength = normalized;
                return;
            }

            OnPropertyChanged(nameof(AiEraserEdgeBlendDisplayText));
            if (!_isLoadingSettings)
            {
                PersistSettings();
            }
        }

        private void RefreshSelectedAiModelState()
        {
            OnPropertyChanged(nameof(HasSelectedAiEnhancementModelInfo));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelSeriesText));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelDescriptionText));
            OnPropertyChanged(nameof(HasSelectedAiEnhancementModelRestriction));
            OnPropertyChanged(nameof(SelectedAiEnhancementModelRestrictionText));
            RefreshAiModelAvailabilityWarning();
        }

        private void RefreshSelectedAiMattingModelState()
        {
            OnPropertyChanged(nameof(HasSelectedAiMattingModelInfo));
            OnPropertyChanged(nameof(SelectedAiMattingModelDescriptionText));
        }

        private void RefreshAiModelAvailabilityWarning()
        {
            if (!AiEnhancementEnabled)
            {
                AiEnhancementModelFallbackWarningText = string.Empty;
                return;
            }

            var modelsDirectory = RuntimeAssetLocator.AiEnhancementModelsDirectory;
            if (!AiEnhancementModelCatalog.TryResolveModelSelection(
                    modelsDirectory,
                    SelectedAiEnhancementModel,
                    AiEnhancementScale,
                    out var resolvedSelection) ||
                !resolvedSelection.UsedDefaultFallback)
            {
                AiEnhancementModelFallbackWarningText = string.Empty;
                return;
            }

            AiEnhancementModelFallbackWarningText = string.Format(
                CultureInfo.CurrentCulture,
                T("AiModelFallbackToDefaultTemplate"),
                BuildLocalizedAiModelDisplayName(SelectedAiEnhancementModel),
                BuildLocalizedAiModelDisplayName(AiEnhancementModel.General));
        }

        private string BuildLocalizedAiModelDisplayName(AiEnhancementModel model)
        {
            return AiEnhancementModelCatalog.BuildListDisplayName(model, T);
        }
    }
}
