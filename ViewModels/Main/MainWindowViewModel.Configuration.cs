using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        private void PersistSettings()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            var existing = _settingsService.Load();

            _settingsService.Save(new AppSettings
            {
                LanguageCode = SelectedLanguage?.Code ?? LanguageCodeSystem,
                ThemeCode = SelectedTheme?.Code ?? ThemeCodeSystem,
                DefaultOutputFormat = SelectedOutputFormat,
                DefaultCompressionMode = SelectedCompressionMode,
                DefaultQuality = Quality,
                DefaultResizeMode = SelectedResizeMode,
                DefaultResizeWidth = ResizeWidth,
                DefaultResizeHeight = ResizeHeight,
                DefaultResizePercent = ResizePercent,
                DefaultRenameMode = SelectedRenameMode,
                DefaultRenamePrefix = RenamePrefix,
                DefaultRenameSuffix = RenameSuffix,
                DefaultRenameStartNumber = RenameStartNumber,
                DefaultRenameNumberDigits = RenameNumberDigits,
                DefaultGifHandlingMode = SelectedGifHandlingMode,
                DefaultGifSpecificFrameIndex = SelectedGifSpecificFrameIndex,
                AiEnhancementEnabled = AiEnhancementEnabled,
                DefaultAiEnhancementScale = AiEnhancementScale,
                DefaultAiEnhancementModel = SelectedAiEnhancementModel,
                DefaultAiExecutionMode = SelectedAiExecutionMode,
                DefaultAiMattingModel = SelectedAiMattingModel,
                DefaultAiMattingDevice = SelectedAiMattingDevice,
                DefaultAiMattingOutputFormat = SelectedAiMattingOutputFormat,
                DefaultAiMattingBackgroundMode = SelectedAiMattingBackgroundMode,
                DefaultAiMattingBackgroundColor = AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = AiMattingEdgeOptimizationEnabled,
                DefaultAiMattingEdgeOptimizationStrength = AiMattingEdgeOptimizationStrength,
                DefaultAiMattingResolutionMode = SelectedAiMattingResolutionMode,
                DefaultOutputDirectory = OutputDirectory,
                UseSourceFolderByDefault = UseSourceFolder,
                HasOutputDirectoryRule = true,
                OutputDirectoryRule = UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                IncludeSubfoldersOnFolderImport = IncludeSubfoldersOnFolderImport,
                AutoOpenOutputDirectory = AutoOpenOutputDirectory,
                AllowOverwrite = AllowOverwrite,
                SvgUseBackground = SvgUseBackground,
                SvgBackgroundColor = EffectiveSvgBackgroundColor,
                IconUseTransparency = IconUseTransparency,
                IconBackgroundColor = EffectiveIconBackgroundColor,
                MaxParallelism = _maxParallelism,
                Presets = Presets.Select(ClonePreset).ToList(),
                WatchModeEnabled = WatchModeEnabled,
                WatchInputDirectory = WatchInputDirectory,
                WatchOutputDirectory = WatchOutputDirectory,
                WatchIncludeSubfolders = WatchIncludeSubfolders,
                KeepRunningInTray = KeepRunningInTray,
                RunOnStartup = RunOnStartup,
                HasWindowPlacement = existing.HasWindowPlacement,
                WindowPositionX = existing.WindowPositionX,
                WindowPositionY = existing.WindowPositionY,
                WindowWidth = existing.WindowWidth,
                WindowHeight = existing.WindowHeight
            });

            RefreshConversionInsights();
        }

        private static bool ResolveUseSourceFolder(AppSettings settings)
        {
            if (settings.HasOutputDirectoryRule)
            {
                return settings.OutputDirectoryRule == OutputDirectoryRule.SourceFolder;
            }

            return settings.UseSourceFolderByDefault;
        }

        private ConversionPreset BuildPreset(string name)
        {
            return new ConversionPreset
            {
                Name = name,
                OutputFormat = SelectedOutputFormat,
                CompressionMode = SelectedCompressionMode,
                Quality = Quality,
                ResizeMode = SelectedResizeMode,
                ResizeWidth = ResizeWidth,
                ResizeHeight = ResizeHeight,
                ResizePercent = ResizePercent,
                RenameMode = SelectedRenameMode,
                RenamePrefix = RenamePrefix,
                RenameSuffix = RenameSuffix,
                RenameStartNumber = RenameStartNumber,
                RenameNumberDigits = RenameNumberDigits,
                GifHandlingMode = SelectedGifHandlingMode,
                GifSpecificFrameIndex = SelectedGifSpecificFrameIndex,
                AiEnhancementEnabled = AiEnhancementEnabled,
                AiEnhancementScale = AiEnhancementScale,
                AiEnhancementModel = SelectedAiEnhancementModel,
                AiExecutionMode = SelectedAiExecutionMode,
                AiMattingModel = SelectedAiMattingModel,
                AiMattingDevice = SelectedAiMattingDevice,
                AiMattingOutputFormat = SelectedAiMattingOutputFormat,
                AiMattingBackgroundMode = SelectedAiMattingBackgroundMode,
                AiMattingBackgroundColor = AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = AiMattingEdgeOptimizationEnabled,
                AiMattingEdgeOptimizationStrength = AiMattingEdgeOptimizationStrength,
                AiMattingResolutionMode = SelectedAiMattingResolutionMode,
                OutputDirectoryRule = UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                OutputDirectory = OutputDirectory,
                AllowOverwrite = AllowOverwrite,
                SvgUseBackground = SvgUseBackground,
                SvgBackgroundColor = EffectiveSvgBackgroundColor,
                IconUseTransparency = IconUseTransparency,
                IconBackgroundColor = EffectiveIconBackgroundColor
            };
        }

        private static ConversionPreset ClonePreset(ConversionPreset source)
        {
            return new ConversionPreset
            {
                Name = source.Name,
                OutputFormat = source.OutputFormat,
                CompressionMode = source.CompressionMode,
                Quality = source.Quality,
                ResizeMode = source.ResizeMode,
                ResizeWidth = source.ResizeWidth,
                ResizeHeight = source.ResizeHeight,
                ResizePercent = source.ResizePercent,
                RenameMode = source.RenameMode,
                RenamePrefix = source.RenamePrefix,
                RenameSuffix = source.RenameSuffix,
                RenameStartNumber = source.RenameStartNumber,
                RenameNumberDigits = source.RenameNumberDigits,
                GifHandlingMode = source.GifHandlingMode,
                GifSpecificFrameIndex = source.GifSpecificFrameIndex,
                AiEnhancementEnabled = source.AiEnhancementEnabled,
                AiEnhancementScale = source.AiEnhancementScale,
                AiEnhancementModel = source.AiEnhancementModel,
                AiExecutionMode = source.AiExecutionMode,
                AiMattingModel = source.AiMattingModel,
                AiMattingDevice = source.AiMattingDevice,
                AiMattingOutputFormat = source.AiMattingOutputFormat,
                AiMattingBackgroundMode = source.AiMattingBackgroundMode,
                AiMattingBackgroundColor = source.AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = source.AiMattingEdgeOptimizationEnabled,
                AiMattingEdgeOptimizationStrength = source.AiMattingEdgeOptimizationStrength,
                AiMattingResolutionMode = source.AiMattingResolutionMode,
                OutputDirectoryRule = source.OutputDirectoryRule,
                OutputDirectory = source.OutputDirectory,
                AllowOverwrite = source.AllowOverwrite,
                SvgUseBackground = source.SvgUseBackground,
                SvgBackgroundColor = source.SvgBackgroundColor,
                IconUseTransparency = source.IconUseTransparency,
                IconBackgroundColor = source.IconBackgroundColor
            };
        }

        private void RefreshEnumOptions()
        {
            RebuildEnumOptions(
                CompressionModes,
                Enum.GetValues<CompressionMode>(),
                mode => T($"CompressionMode_{mode}"));

            RebuildEnumOptions(
                ResizeModes,
                Enum.GetValues<ResizeMode>(),
                mode => T($"ResizeMode_{mode}"));

            RebuildEnumOptions(
                RenameModes,
                Enum.GetValues<RenameMode>(),
                mode => T($"RenameMode_{mode}"));

            SelectedCompressionModeOption = CompressionModes.FirstOrDefault(x => EqualityComparer<CompressionMode>.Default.Equals(x.Value, SelectedCompressionMode));
            SelectedResizeModeOption = ResizeModes.FirstOrDefault(x => EqualityComparer<ResizeMode>.Default.Equals(x.Value, SelectedResizeMode));
            SelectedRenameModeOption = RenameModes.FirstOrDefault(x => EqualityComparer<RenameMode>.Default.Equals(x.Value, SelectedRenameMode));
            RefreshGifHandlingModeOptions();
            RefreshAiOptionCollections();
        }

        private void RefreshLanguageOptions(string? selectedCode)
        {
            _isRefreshingLanguageOptions = true;
            if (Languages.Count == 0)
            {
                Languages.Add(new LanguageOption(LanguageCodeSystem, T("LanguageOption_System")));
                foreach (var language in SupportedLanguageSeeds)
                {
                    Languages.Add(new LanguageOption(language.Code, language.DisplayName));
                }
            }
            else
            {
                var systemOption = Languages.FirstOrDefault(x => x.Code.Equals(LanguageCodeSystem, StringComparison.OrdinalIgnoreCase));
                if (systemOption is not null)
                {
                    systemOption.DisplayName = T("LanguageOption_System");
                }
            }

            var desiredCode = string.IsNullOrWhiteSpace(selectedCode) ? LanguageCodeSystem : selectedCode;
            var match = Languages.FirstOrDefault(x => x.Code.Equals(desiredCode, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
            if (!ReferenceEquals(match, SelectedLanguage))
            {
                SelectedLanguage = match;
            }

            _isRefreshingLanguageOptions = false;
            OnPropertyChanged(nameof(IsSystemLanguageSelected));
        }

        private void RefreshThemeOptions(string? selectedCode)
        {
            Themes.Clear();
            Themes.Add(new ThemeOption(ThemeCodeDark, T("ThemeOption_Dark")));
            Themes.Add(new ThemeOption(ThemeCodeLight, T("ThemeOption_Light")));
            Themes.Add(new ThemeOption(ThemeCodeSystem, T("ThemeOption_System")));

            var desiredCode = string.IsNullOrWhiteSpace(selectedCode) ? ThemeCodeSystem : selectedCode;
            var match = Themes.FirstOrDefault(x => x.Code.Equals(desiredCode, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !ReferenceEquals(match, SelectedTheme))
            {
                SelectedTheme = match;
            }
        }

        private string ResolveEffectiveLanguageCode(string? selectedCode)
        {
            if (string.IsNullOrWhiteSpace(selectedCode) ||
                selectedCode.Equals(LanguageCodeSystem, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveSystemLanguageCode();
            }

            return ResolveSupportedLanguageCode(selectedCode);
        }

        private static string ResolveSystemLanguageCode()
        {
            return ResolveSupportedLanguageCode(CultureInfo.CurrentUICulture.Name);
        }

        private static string ResolveSupportedLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return FallbackLanguageCode;
            }

            foreach (var language in SupportedLanguageSeeds)
            {
                if (language.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                {
                    return language.Code;
                }
            }

            var normalized = code.Trim();
            if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                var lower = normalized.ToLowerInvariant();
                if (lower.Contains("hant") || lower.Contains("-tw") || lower.Contains("-hk") || lower.Contains("-mo"))
                {
                    return "zh-TW";
                }

                return "zh-CN";
            }

            var neutral = normalized.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(neutral))
            {
                foreach (var language in SupportedLanguageSeeds)
                {
                    if (language.Code.StartsWith(neutral, StringComparison.OrdinalIgnoreCase))
                    {
                        return language.Code;
                    }
                }
            }

            return FallbackLanguageCode;
        }

        private static void RebuildEnumOptions<T>(
            ObservableCollection<EnumOption<T>> options,
            IReadOnlyList<T> values,
            Func<T, string> textFactory)
            where T : struct
        {
            options.Clear();
            foreach (var value in values)
            {
                options.Add(new EnumOption<T>(value, textFactory(value)));
            }
        }

        private void SetStatus(string key)
        {
            _statusKey = key;
            StatusText = T(key);
        }

        public void SetVersionBadgeHover(bool isHovered)
        {
            if (_isVersionBadgeHovered == isHovered)
            {
                return;
            }

            _isVersionBadgeHovered = isHovered;
            OnPropertyChanged(nameof(FooterVersionBadgeText));
            OnPropertyChanged(nameof(FooterVersionBadgeFontSize));
        }

        private string T(string key)
        {
            return _localizationService.Translate(key);
        }

        public string TranslateText(string key)
        {
            return T(key);
        }

        private string FormatT(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        private static FlowDirection ResolveFlowDirection(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return FlowDirection.LeftToRight;
            }

            return languageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        private void ApplyTheme(string themeCode)
        {
            if (Application.Current is null)
            {
                return;
            }

            if (themeCode.Equals(ThemeCodeLight, StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                return;
            }

            if (themeCode.Equals(ThemeCodeSystem, StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Default;
                return;
            }

            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        }

        private void RefreshLocalizedProperties()
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ImportButtonText));
            OnPropertyChanged(nameof(ImportPsdText));
            OnPropertyChanged(nameof(ImportFolderButtonText));
            OnPropertyChanged(nameof(ClearButtonText));
            OnPropertyChanged(nameof(OutputFormatText));
            OnPropertyChanged(nameof(OutputFolderText));
            OnPropertyChanged(nameof(OutputDirectoryHintText));
            OnPropertyChanged(nameof(ChooseFolderButtonText));
            OnPropertyChanged(nameof(StartConversionButtonText));
            OnPropertyChanged(nameof(SettingsButtonText));
            OnPropertyChanged(nameof(ImageListText));
            OnPropertyChanged(nameof(DropHintTitleText));
            OnPropertyChanged(nameof(DropHintDescriptionText));
            OnPropertyChanged(nameof(ImportSupportedFilesText));
            OnPropertyChanged(nameof(PreviewTabText));
            OnPropertyChanged(nameof(SettingsTabText));
            OnPropertyChanged(nameof(NoPreviewText));
            OnPropertyChanged(nameof(PreviewWindowHintText));
            OnPropertyChanged(nameof(StatusLabelText));
            OnPropertyChanged(nameof(CurrentFileLabelText));
            OnPropertyChanged(nameof(RemainingLabelText));
            OnPropertyChanged(nameof(ProgressLabelText));
            OnPropertyChanged(nameof(LanguageLabelText));
            OnPropertyChanged(nameof(LanguageSystemHintText));
            OnPropertyChanged(nameof(ThemeLabelText));
            OnPropertyChanged(nameof(DefaultOutputFolderLabelText));
            OnPropertyChanged(nameof(UseSourceFolderText));
            OnPropertyChanged(nameof(IncludeSubfoldersOnImportText));
            OnPropertyChanged(nameof(AutoOpenOutputFolderText));
            OnPropertyChanged(nameof(AllowOverwriteText));
            OnPropertyChanged(nameof(DefaultOutputFormatText));
            OnPropertyChanged(nameof(CompressionModeText));
            OnPropertyChanged(nameof(CompressionModeHintText));
            OnPropertyChanged(nameof(QualityText));
            OnPropertyChanged(nameof(ResizeSettingsText));
            OnPropertyChanged(nameof(ResizeModeText));
            OnPropertyChanged(nameof(ResizeWidthText));
            OnPropertyChanged(nameof(ResizeHeightText));
            OnPropertyChanged(nameof(ResizePercentText));
            OnPropertyChanged(nameof(RenameSettingsText));
            OnPropertyChanged(nameof(RenameModeText));
            OnPropertyChanged(nameof(RenamePrefixText));
            OnPropertyChanged(nameof(RenameSuffixText));
            OnPropertyChanged(nameof(RenameStartNumberText));
            OnPropertyChanged(nameof(RenameDigitsText));
            OnPropertyChanged(nameof(RenameHintText));
            OnPropertyChanged(nameof(GifHandlingText));
            OnPropertyChanged(nameof(GifTrimRangeText));
            OnPropertyChanged(nameof(GifAnimatedLabelText));
            OnPropertyChanged(nameof(GifFrameCountTemplateText));
            OnPropertyChanged(nameof(PresetSettingsText));
            OnPropertyChanged(nameof(PresetNameText));
            OnPropertyChanged(nameof(PresetNameHintText));
            OnPropertyChanged(nameof(SavePresetText));
            OnPropertyChanged(nameof(ApplyPresetText));
            OnPropertyChanged(nameof(DeletePresetText));
            OnPropertyChanged(nameof(SvgSettingsText));
            OnPropertyChanged(nameof(SvgUseBackgroundText));
            OnPropertyChanged(nameof(SvgBackgroundColorText));
            OnPropertyChanged(nameof(SvgBackgroundColorRgbText));
            OnPropertyChanged(nameof(SvgColorPickerText));
            OnPropertyChanged(nameof(SvgBackgroundColorHintText));
            OnPropertyChanged(nameof(SvgBackgroundRequiredHintText));
            OnPropertyChanged(nameof(IconSettingsText));
            OnPropertyChanged(nameof(IconUseTransparencyText));
            OnPropertyChanged(nameof(FailedFilesText));
            OnPropertyChanged(nameof(DetailText));
            OnPropertyChanged(nameof(RemoveText));
            OnPropertyChanged(nameof(ImportDialogTitle));
            OnPropertyChanged(nameof(ImportFolderDialogTitle));
            OnPropertyChanged(nameof(SelectFolderDialogTitle));
            OnPropertyChanged(nameof(ConversionSummaryTitleText));
            OnPropertyChanged(nameof(SummaryTotalText));
            OnPropertyChanged(nameof(SummarySuccessText));
            OnPropertyChanged(nameof(SummaryFailedText));
            OnPropertyChanged(nameof(SummaryDurationText));
            OnPropertyChanged(nameof(CloseText));
            OnPropertyChanged(nameof(AlreadyRunningTitleText));
            OnPropertyChanged(nameof(AlreadyRunningMessageText));
            OnPropertyChanged(nameof(AboutButtonText));
            OnPropertyChanged(nameof(ContactAuthorButtonText));
            OnPropertyChanged(nameof(ContactAuthorTitleText));
            OnPropertyChanged(nameof(ContactAuthorEmailLabelText));
            OnPropertyChanged(nameof(ContactAuthorCopyText));
            OnPropertyChanged(nameof(ContactAuthorNoteText));
            OnPropertyChanged(nameof(AboutWindowTitleText));
            OnPropertyChanged(nameof(AboutVersionLabelText));
            OnPropertyChanged(nameof(AboutTaglineText));
            OnPropertyChanged(nameof(AboutSummaryText));
            OnPropertyChanged(nameof(AboutFeatureSectionTitleText));
            OnPropertyChanged(nameof(AboutFeatureSectionBodyText));
            OnPropertyChanged(nameof(AboutTechSectionTitleText));
            OnPropertyChanged(nameof(AboutTechSectionBodyText));
            OnPropertyChanged(nameof(AboutIdeasSectionTitleText));
            OnPropertyChanged(nameof(AboutIdeasSectionBodyText));
            OnPropertyChanged(nameof(AboutAuthorLabelText));
            OnPropertyChanged(nameof(AboutLinksLabelText));
            OnPropertyChanged(nameof(AboutWebsiteLabelText));
            OnPropertyChanged(nameof(AboutWebsiteButtonText));
            OnPropertyChanged(nameof(AboutRepositoryLabelText));
            OnPropertyChanged(nameof(AboutRepositoryButtonText));
            OnPropertyChanged(nameof(AboutAuthorNameText));
            OnPropertyChanged(nameof(AppVersionText));
            OnPropertyChanged(nameof(VersionBadgeHoverText));
            OnPropertyChanged(nameof(VersionBadgeToolTipText));
            OnPropertyChanged(nameof(FooterVersionBadgeText));
            OnPropertyChanged(nameof(VersionNotesWindowTitleText));
            OnPropertyChanged(nameof(VersionNotesHeaderText));
            OnPropertyChanged(nameof(VersionNotesSummaryText));
            OnPropertyChanged(nameof(VersionNotesFixesTitleText));
            OnPropertyChanged(nameof(VersionNotesFixesBodyText));
            OnPropertyChanged(nameof(VersionNotesFeaturesTitleText));
            OnPropertyChanged(nameof(VersionNotesFeaturesBodyText));
            RefreshLocalizedPropertiesAi();
            RefreshLocalizedPropertiesPreviewAi();
            RefreshLocalizedPropertiesV3();
            RefreshPdfLocalizedProperties();
            RefreshGifLabels();
            RefreshGifPdfLocalizedProperties();
        }

        private string EffectiveSvgBackgroundColor => ToHexColor(SvgBackgroundColorValue);

        private string EffectiveIconBackgroundColor => ToHexColor(IconBackgroundColorValue);

        private void UpdateSvgColorInputs(Color color)
        {
            _isSyncingSvgColorInputs = true;
            SvgBackgroundColor = ToHexColor(color);
            SvgBackgroundColorRgb = ToRgbColorText(color);
            SvgBackgroundColorValue = color;
            _isSyncingSvgColorInputs = false;
        }

        private void UpdateIconColorInputs(Color color)
        {
            _isSyncingIconColorInputs = true;
            IconBackgroundColor = ToHexColor(color);
            IconBackgroundColorRgb = ToRgbColorText(color);
            IconBackgroundColorValue = color;
            _isSyncingIconColorInputs = false;
        }

        private static bool TryParseSvgColor(string value, out Color color)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                color = default;
                return false;
            }

            return Color.TryParse(value.Trim(), out color);
        }

        private static bool TryParseRgbColor(string value, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(")", StringComparison.Ordinal))
            {
                text = text[4..^1];
            }

            var parts = text.Split([',', '\uFF0C'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!byte.TryParse(parts[0], out var r) ||
                !byte.TryParse(parts[1], out var g) ||
                !byte.TryParse(parts[2], out var b))
            {
                return false;
            }

            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        private static string ToHexColor(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static string ToRgbColorText(Color color)
        {
            return $"{color.R},{color.G},{color.B}";
        }

        private static bool IsIconSource(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}");
            }

            return string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds / 10:00}");
        }

        private void TryOpenOutputFolder(IReadOnlyList<string> outputFolders, bool hasOutput)
        {
            if (!hasOutput || !AutoOpenOutputDirectory)
            {
                return;
            }

            var folders = outputFolders
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (folders.Count == 0)
            {
                return;
            }

            var folderToOpen = folders[0];
            if (!Directory.Exists(folderToOpen))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderToOpen,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(nameof(MainWindowViewModel), $"Failed to open output folder '{folderToOpen}'.", ex);
            }
        }
    }
}


