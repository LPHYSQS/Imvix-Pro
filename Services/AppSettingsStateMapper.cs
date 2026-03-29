using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImvixPro.Services
{
    internal static class AppSettingsStateMapper
    {
        public static ApplicationPreferences ResolveApplicationPreferences(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (settings.ApplicationPreferences is { } preferences)
            {
                return CloneApplicationPreferences(preferences);
            }

            return new ApplicationPreferences
            {
                LanguageCode = settings.LanguageCode,
                ThemeCode = settings.ThemeCode,
                DefaultOutputFormat = settings.DefaultOutputFormat,
                DefaultCompressionMode = settings.DefaultCompressionMode,
                DefaultQuality = settings.DefaultQuality,
                DefaultResizeMode = settings.DefaultResizeMode,
                DefaultResizeWidth = settings.DefaultResizeWidth,
                DefaultResizeHeight = settings.DefaultResizeHeight,
                DefaultResizePercent = settings.DefaultResizePercent,
                DefaultRenameMode = settings.DefaultRenameMode,
                DefaultRenamePrefix = settings.DefaultRenamePrefix,
                DefaultRenameSuffix = settings.DefaultRenameSuffix,
                DefaultRenameStartNumber = settings.DefaultRenameStartNumber,
                DefaultRenameNumberDigits = settings.DefaultRenameNumberDigits,
                DefaultOutputDirectory = settings.DefaultOutputDirectory,
                UseSourceFolderByDefault = settings.UseSourceFolderByDefault,
                HasOutputDirectoryRule = settings.HasOutputDirectoryRule,
                OutputDirectoryRule = settings.OutputDirectoryRule,
                IncludeSubfoldersOnFolderImport = settings.IncludeSubfoldersOnFolderImport,
                DefaultListSortMode = settings.DefaultListSortMode,
                MaxParallelism = settings.MaxParallelism,
                AutoOpenOutputDirectory = settings.AutoOpenOutputDirectory,
                AllowOverwrite = settings.AllowOverwrite,
                SvgUseBackground = settings.SvgUseBackground,
                SvgBackgroundColor = settings.SvgBackgroundColor,
                IconUseTransparency = settings.IconUseTransparency,
                IconBackgroundColor = settings.IconBackgroundColor,
                DefaultGifHandlingMode = settings.DefaultGifHandlingMode,
                DefaultGifSpecificFrameIndex = settings.DefaultGifSpecificFrameIndex,
                AiEnhancementEnabled = settings.AiEnhancementEnabled,
                DefaultAiEnhancementScale = settings.DefaultAiEnhancementScale,
                DefaultAiEnhancementModel = settings.DefaultAiEnhancementModel,
                DefaultAiExecutionMode = settings.DefaultAiExecutionMode,
                Presets = ClonePresets(settings.Presets),
                KeepRunningInTray = settings.KeepRunningInTray,
                RunOnStartup = settings.RunOnStartup
            };
        }

        public static PreviewToolState ResolvePreviewToolState(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (settings.PreviewToolState is { } previewToolState)
            {
                return previewToolState.Clone();
            }

            return new PreviewToolState
            {
                AiMattingModel = settings.DefaultAiMattingModel,
                AiMattingDevice = settings.DefaultAiMattingDevice,
                AiMattingOutputFormat = settings.DefaultAiMattingOutputFormat,
                AiMattingBackgroundMode = settings.DefaultAiMattingBackgroundMode,
                AiMattingBackgroundColor = settings.DefaultAiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = settings.AiMattingEdgeOptimizationEnabled,
                AiMattingEdgeOptimizationStrength = settings.DefaultAiMattingEdgeOptimizationStrength,
                AiMattingResolutionMode = settings.DefaultAiMattingResolutionMode
            };
        }

        public static WatchProfile ResolveWatchProfile(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (settings.WatchProfile is { } watchProfile)
            {
                return watchProfile.Clone();
            }

            return new WatchProfile
            {
                IsEnabled = settings.WatchModeEnabled,
                InputDirectory = settings.WatchInputDirectory,
                OutputDirectory = settings.WatchOutputDirectory,
                IncludeSubfolders = settings.WatchIncludeSubfolders,
                JobDefinition = new ConversionJobDefinition
                {
                    Name = "Watch Profile",
                    Options = BuildLegacyWatchOptions(settings)
                }
            };
        }

        public static AppSettings CreateSettings(
            AppSettings existing,
            ApplicationPreferences preferences,
            PreviewToolState previewToolState,
            WatchProfile watchProfile)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(previewToolState);
            ArgumentNullException.ThrowIfNull(watchProfile);

            return new AppSettings
            {
                ApplicationPreferences = CloneApplicationPreferences(preferences),
                PreviewToolState = previewToolState.Clone(),
                WatchProfile = watchProfile.Clone(),

                LanguageCode = preferences.LanguageCode,
                ThemeCode = preferences.ThemeCode,
                DefaultOutputFormat = preferences.DefaultOutputFormat,
                DefaultCompressionMode = preferences.DefaultCompressionMode,
                DefaultQuality = preferences.DefaultQuality,
                DefaultResizeMode = preferences.DefaultResizeMode,
                DefaultResizeWidth = preferences.DefaultResizeWidth,
                DefaultResizeHeight = preferences.DefaultResizeHeight,
                DefaultResizePercent = preferences.DefaultResizePercent,
                DefaultRenameMode = preferences.DefaultRenameMode,
                DefaultRenamePrefix = preferences.DefaultRenamePrefix,
                DefaultRenameSuffix = preferences.DefaultRenameSuffix,
                DefaultRenameStartNumber = preferences.DefaultRenameStartNumber,
                DefaultRenameNumberDigits = preferences.DefaultRenameNumberDigits,
                DefaultOutputDirectory = preferences.DefaultOutputDirectory,
                UseSourceFolderByDefault = preferences.UseSourceFolderByDefault,
                HasOutputDirectoryRule = preferences.HasOutputDirectoryRule,
                OutputDirectoryRule = preferences.OutputDirectoryRule,
                AutoResultFolderName = existing.AutoResultFolderName,
                IncludeSubfoldersOnFolderImport = preferences.IncludeSubfoldersOnFolderImport,
                DefaultListSortMode = preferences.DefaultListSortMode,
                MaxParallelism = preferences.MaxParallelism,
                AutoOpenOutputDirectory = preferences.AutoOpenOutputDirectory,
                AllowOverwrite = preferences.AllowOverwrite,
                SvgUseBackground = preferences.SvgUseBackground,
                SvgBackgroundColor = preferences.SvgBackgroundColor,
                IconUseTransparency = preferences.IconUseTransparency,
                IconBackgroundColor = preferences.IconBackgroundColor,
                DefaultGifHandlingMode = preferences.DefaultGifHandlingMode,
                DefaultGifSpecificFrameIndex = preferences.DefaultGifSpecificFrameIndex,
                AiPanelEnabled = true,
                HasAiPanelVisibilityPreference = existing.HasAiPanelVisibilityPreference,
                AiEnhancementEnabled = preferences.AiEnhancementEnabled,
                DefaultAiEnhancementScale = preferences.DefaultAiEnhancementScale,
                DefaultAiEnhancementModel = preferences.DefaultAiEnhancementModel,
                DefaultAiExecutionMode = preferences.DefaultAiExecutionMode,
                DefaultAiMattingModel = previewToolState.AiMattingModel,
                DefaultAiMattingDevice = previewToolState.AiMattingDevice,
                DefaultAiMattingOutputFormat = previewToolState.AiMattingOutputFormat,
                DefaultAiMattingBackgroundMode = previewToolState.AiMattingBackgroundMode,
                DefaultAiMattingBackgroundColor = previewToolState.AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = previewToolState.AiMattingEdgeOptimizationEnabled,
                DefaultAiMattingEdgeOptimizationStrength = previewToolState.AiMattingEdgeOptimizationStrength,
                DefaultAiMattingResolutionMode = previewToolState.AiMattingResolutionMode,
                Presets = ClonePresets(preferences.Presets),
                WatchModeEnabled = watchProfile.IsEnabled,
                WatchInputDirectory = watchProfile.InputDirectory,
                WatchOutputDirectory = watchProfile.OutputDirectory,
                WatchIncludeSubfolders = watchProfile.IncludeSubfolders,
                KeepRunningInTray = preferences.KeepRunningInTray,
                RunOnStartup = preferences.RunOnStartup,
                HasWindowPlacement = existing.HasWindowPlacement,
                WindowPositionX = existing.WindowPositionX,
                WindowPositionY = existing.WindowPositionY,
                WindowWidth = existing.WindowWidth,
                WindowHeight = existing.WindowHeight
            };
        }

        private static ApplicationPreferences CloneApplicationPreferences(ApplicationPreferences source)
        {
            return new ApplicationPreferences
            {
                LanguageCode = source.LanguageCode,
                ThemeCode = source.ThemeCode,
                DefaultOutputFormat = source.DefaultOutputFormat,
                DefaultCompressionMode = source.DefaultCompressionMode,
                DefaultQuality = source.DefaultQuality,
                DefaultResizeMode = source.DefaultResizeMode,
                DefaultResizeWidth = source.DefaultResizeWidth,
                DefaultResizeHeight = source.DefaultResizeHeight,
                DefaultResizePercent = source.DefaultResizePercent,
                DefaultRenameMode = source.DefaultRenameMode,
                DefaultRenamePrefix = source.DefaultRenamePrefix,
                DefaultRenameSuffix = source.DefaultRenameSuffix,
                DefaultRenameStartNumber = source.DefaultRenameStartNumber,
                DefaultRenameNumberDigits = source.DefaultRenameNumberDigits,
                DefaultOutputDirectory = source.DefaultOutputDirectory,
                UseSourceFolderByDefault = source.UseSourceFolderByDefault,
                HasOutputDirectoryRule = source.HasOutputDirectoryRule,
                OutputDirectoryRule = source.OutputDirectoryRule,
                IncludeSubfoldersOnFolderImport = source.IncludeSubfoldersOnFolderImport,
                DefaultListSortMode = source.DefaultListSortMode,
                MaxParallelism = source.MaxParallelism,
                AutoOpenOutputDirectory = source.AutoOpenOutputDirectory,
                AllowOverwrite = source.AllowOverwrite,
                SvgUseBackground = source.SvgUseBackground,
                SvgBackgroundColor = source.SvgBackgroundColor,
                IconUseTransparency = source.IconUseTransparency,
                IconBackgroundColor = source.IconBackgroundColor,
                DefaultGifHandlingMode = source.DefaultGifHandlingMode,
                DefaultGifSpecificFrameIndex = source.DefaultGifSpecificFrameIndex,
                AiEnhancementEnabled = source.AiEnhancementEnabled,
                DefaultAiEnhancementScale = source.DefaultAiEnhancementScale,
                DefaultAiEnhancementModel = source.DefaultAiEnhancementModel,
                DefaultAiExecutionMode = source.DefaultAiExecutionMode,
                Presets = ClonePresets(source.Presets),
                KeepRunningInTray = source.KeepRunningInTray,
                RunOnStartup = source.RunOnStartup
            };
        }

        private static List<ConversionPreset> ClonePresets(IReadOnlyList<ConversionPreset>? presets)
        {
            if (presets is null || presets.Count == 0)
            {
                return [];
            }

            return presets.Select(ClonePreset).ToList();
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
                OutputDirectoryRule = source.OutputDirectoryRule,
                OutputDirectory = source.OutputDirectory,
                AutoResultFolderName = source.AutoResultFolderName,
                AllowOverwrite = source.AllowOverwrite,
                SvgUseBackground = source.SvgUseBackground,
                SvgBackgroundColor = source.SvgBackgroundColor,
                IconUseTransparency = source.IconUseTransparency,
                IconBackgroundColor = source.IconBackgroundColor,
                GifHandlingMode = source.GifHandlingMode,
                GifSpecificFrameIndex = source.GifSpecificFrameIndex,
                AiEnhancementEnabled = source.AiEnhancementEnabled,
                AiEnhancementScale = source.AiEnhancementScale,
                AiEnhancementModel = source.AiEnhancementModel,
                AiExecutionMode = source.AiExecutionMode
            };
        }

        private static ConversionOptions BuildLegacyWatchOptions(AppSettings settings)
        {
            return new ConversionOptions
            {
                OutputFormat = settings.DefaultOutputFormat,
                CompressionMode = settings.DefaultCompressionMode,
                Quality = settings.DefaultQuality,
                ResizeMode = settings.DefaultResizeMode,
                ResizeWidth = settings.DefaultResizeWidth,
                ResizeHeight = settings.DefaultResizeHeight,
                ResizePercent = settings.DefaultResizePercent,
                RenameMode = settings.DefaultRenameMode,
                RenamePrefix = settings.DefaultRenamePrefix,
                RenameSuffix = settings.DefaultRenameSuffix,
                RenameStartNumber = settings.DefaultRenameStartNumber,
                RenameNumberDigits = settings.DefaultRenameNumberDigits,
                OutputDirectoryRule = OutputDirectoryRule.SpecificFolder,
                OutputDirectory = settings.WatchOutputDirectory,
                AutoResultFolderName = settings.AutoResultFolderName,
                AllowOverwrite = settings.AllowOverwrite,
                SvgUseBackground = settings.SvgUseBackground,
                SvgBackgroundColor = settings.SvgBackgroundColor,
                IconUseTransparency = settings.IconUseTransparency,
                IconBackgroundColor = settings.IconBackgroundColor,
                GifHandlingMode = settings.DefaultGifHandlingMode,
                GifSpecificFrameIndex = settings.DefaultGifSpecificFrameIndex,
                AiEnhancementEnabled = settings.AiEnhancementEnabled,
                AiEnhancementScale = settings.DefaultAiEnhancementScale,
                AiEnhancementModel = settings.DefaultAiEnhancementModel,
                AiExecutionMode = settings.DefaultAiExecutionMode,
                LanguageCode = settings.LanguageCode,
                MaxDegreeOfParallelism = settings.MaxParallelism
            };
        }
    }
}
