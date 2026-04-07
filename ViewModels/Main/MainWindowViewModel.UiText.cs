using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public string ProgressPercentText => $"{ProgressPercent:0}%";

        public string AppDisplayName => AppIdentity.DisplayName;

        public string WindowTitle => AppIdentity.DisplayName;

        public string ImportButtonText => T("ImportImages");

        public string ImportFolderButtonText => T("ImportFolder");

        public string ClearButtonText => T("ClearList");

        public string OutputFormatText => T("OutputFormat");

        public string OutputFolderText => T("OutputFolder");

        public string OutputDirectoryHintText => T("OutputDirectoryHint");

        public string ChooseFolderButtonText => T("ChooseFolder");

        public string StartConversionButtonText => T("StartConversion");

        public string SettingsButtonText => T("Settings");

        public string ImageListText => T("ImageList");

        public string DropHintTitleText => T("DropHintTitle");

        public string DropHintDescriptionText => T("DropHintDescription");

        public string ImportSupportedFilesText => T("ImportSupportedFiles");

        public string PreviewTabText => T("PreviewTab");

        public string SettingsTabText => T("SettingsTab");

        public string NoPreviewText => T("NoPreview");

        public string PreviewWindowHintText => T("PreviewWindowHint");

        public string IntelligentFeatureDisclaimerTitleText => T("IntelligentFeatureDisclaimerTitle");

        public string PreviewIntelligentFeatureDisclaimerText => T("PreviewIntelligentFeatureDisclaimer");

        public string AiSettingsIntelligentFeatureDisclaimerText => T("AiSettingsIntelligentFeatureDisclaimer");

        public string StatusLabelText => T("StatusLabel");

        public string CurrentFileLabelText => T("CurrentFile");

        public string RemainingLabelText => T("Remaining");

        public string ProgressLabelText => T("ConversionProgress");

        public bool ShowAiConversionFeedback => IsConverting && AiEnhancementEnabled;

        public string ConversionFeedbackTitleText => T("ConversionFeedbackTitle");

        public string ConversionFeedbackDescriptionText => T("ConversionFeedbackDescription");

        public string ConversionFeedbackHardwareHintText => T("ConversionFeedbackHardwareHint");

        public string ConversionFeedbackCloseHintText => T("ConversionFeedbackCloseHint");

        public string LanguageLabelText => T("Language");

        public string LanguageSystemHintText => T("LanguageSystemHint");

        public bool IsSystemLanguageSelected => SelectedLanguage?.Code.Equals(LanguageCodeSystem, StringComparison.OrdinalIgnoreCase) ?? false;

        public string ThemeLabelText => T("Theme");

        public string DefaultOutputFolderLabelText => T("DefaultOutputFolder");

        public string UseSourceFolderText => T("UseSourceFolder");

        public string IncludeSubfoldersOnImportText => T("IncludeSubfoldersOnImport");

        public string AutoOpenOutputFolderText => T("AutoOpenOutputFolder");

        public string AllowOverwriteText => T("AllowOverwrite");

        public string DefaultOutputFormatText => T("DefaultOutputFormat");

        public string CompressionModeText => T("CompressionMode");

        public string CompressionModeHintText => T("CompressionModeHint");

        public string QualityText => T("Quality");

        public string ResizeSettingsText => T("ResizeSettings");

        public string ResizeModeText => T("ResizeMode");

        public string ResizeWidthText => T("ResizeWidth");

        public string ResizeHeightText => T("ResizeHeight");

        public string ResizePercentText => T("ResizePercent");

        public string RenameSettingsText => T("RenameSettings");

        public string RenameModeText => T("RenameMode");

        public string RenamePrefixText => T("RenamePrefix");

        public string RenameSuffixText => T("RenameSuffix");

        public string RenameStartNumberText => T("RenameStartNumber");

        public string RenameDigitsText => T("RenameDigits");

        public string RenameHintText => T("RenameHint");

        public string GifHandlingText => T("GifHandling");

        public string GifTrimRangeText => T("GifTrimRange");

        public string GifAnimatedLabelText => T("GifAnimatedLabel");

        public string GifFrameCountTemplateText => T("GifFrameCountTemplate");

        public string PresetSettingsText => T("PresetSettings");

        public string PresetNameText => T("PresetName");

        public string PresetNameHintText => T("PresetNameHint");

        public string SavePresetText => T("SavePreset");

        public string ApplyPresetText => T("ApplyPreset");

        public string DeletePresetText => T("DeletePreset");

        public string SvgSettingsText => ShouldUseBackgroundFillTextForSelectedImage ? T("BackgroundFill") : T("SvgSettings");

        public string SvgUseBackgroundText => ShouldForceBackgroundColorEditorForSelectedImage()
            ? T("BackgroundFillRequiredToggle")
            : ShouldUseBackgroundFillTextForSelectedImage
                ? T("BackgroundFill")
                : T("SvgUseBackground");

        public string SvgBackgroundColorText => T("SvgBackgroundColor");

        public string SvgBackgroundColorRgbText => T("SvgBackgroundColorRgb");

        public string SvgColorPickerText => T("SvgColorPicker");

        public string SvgBackgroundColorHintText => T("SvgBackgroundColorHint");

        public string SvgBackgroundRequiredHintText => T("BackgroundFillRequiredHint");

        public string IconSettingsText => T("IconSettings");

        public string IconUseTransparencyText => T("IconUseTransparency");

        public string FailedFilesText => T("FailedFiles");

        public string RemoveText => T("Remove");

        public string ImportDialogTitle => T("ImportDialogTitle");

        public string ImportFolderDialogTitle => T("ImportFolderDialogTitle");

        public string SelectFolderDialogTitle => T("SelectFolderDialogTitle");

        public string ConversionSummaryTitleText => T("ConversionSummaryTitle");

        public string SummaryTotalText => T("SummaryTotal");

        public string SummarySuccessText => T("SummarySuccess");

        public string SummaryFailedText => T("SummaryFailed");

        public string SummaryDurationText => T("SummaryDuration");

        public string CloseText => T("Close");

        public string AlreadyRunningTitleText => T("AlreadyRunningTitle");

        public string AlreadyRunningMessageText => T("AlreadyRunningMessage");

        public string AboutButtonText => T("AboutButton");

        public string ContactAuthorButtonText => T("ContactAuthor");

        public string ContactAuthorTitleText => T("ContactAuthor");

        public string ContactAuthorEmailLabelText => T("ContactAuthorEmailLabel");

        public string ContactAuthorCopyText => T("ContactAuthorCopy");

        public string ContactAuthorEmailText => ContactAuthorEmail;

        public string ContactAuthorNoteText => FormatT("ContactAuthorNote", AppVersionText);

        public string AboutWindowTitleText => T("AboutWindowTitle");

        public string AboutVersionLabelText => T("AboutVersionLabel");

        public string AboutTaglineText => T("AboutTagline");

        public string AboutSummaryText => T("AboutSummary");

        public string AboutFeatureSectionTitleText => T("AboutFeatureSectionTitle");

        public string AboutFeatureSectionBodyText => T("AboutFeatureSectionBody");

        public string AboutTechSectionTitleText => T("AboutTechSectionTitle");

        public string AboutTechSectionBodyText => T("AboutTechSectionBody");

        public string AboutIdeasSectionTitleText => T("AboutIdeasSectionTitle");

        public string AboutIdeasSectionBodyText => T("AboutIdeasSectionBody");

        private bool ShouldUseBackgroundFillTextForSelectedImage =>
            SelectedImage is not null &&
            !SelectedImage.Extension.Equals("SVG", StringComparison.OrdinalIgnoreCase) &&
            IsBackgroundFillSource(SelectedImage);

        public string AboutAuthorLabelText => T("AboutAuthorLabel");

        public string AboutLinksLabelText => T("AboutLinksLabel");

        public string AboutWebsiteLabelText => T("AboutWebsiteLabel");

        public string AboutWebsiteButtonText => T("AboutWebsiteButton");

        public string AboutRepositoryLabelText => T("AboutRepositoryLabel");

        public string AboutRepositoryButtonText => T("AboutRepositoryButton");

        public string AboutAuthorNameText => "\u5DF2\u901D\u60C5\u6B87";

        public string AppVersionText => $"v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "2.0.1"}";

        public string VersionBadgeHoverText => T("VersionBadgeHover");

        public string VersionBadgeToolTipText => FormatT("VersionBadgeToolTipFormat", AppVersionText);

        public string FooterVersionBadgeText => _isVersionBadgeHovered ? VersionBadgeHoverText : AppVersionText;

        public double FooterVersionBadgeFontSize => _isVersionBadgeHovered ? 14d : 15d;

        public string VersionNotesWindowTitleText => FormatT("VersionNotesWindowTitleFormat", AppVersionText);

        public string VersionNotesHeaderText => FormatT("VersionNotesHeaderFormat", AppVersionText);

        public string VersionNotesSummaryText => T("VersionNotesSummary");

        public string VersionNotesFixesTitleText => T("VersionNotesFixesTitle");

        public string VersionNotesFixesBodyText => T("VersionNotesFixesBody");

        public string VersionNotesFeaturesTitleText => T("VersionNotesFeaturesTitle");

        public string VersionNotesFeaturesBodyText => T("VersionNotesFeaturesBody");

        public bool IsRightToLeftLayout => UiFlowDirection == FlowDirection.RightToLeft;

        public bool IsLeftToRightLayout => !IsRightToLeftLayout;
    }
}
