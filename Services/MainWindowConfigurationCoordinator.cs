using ImvixPro.AI.Inpainting.Models;
using ImvixPro.AI.Matting.Models;
using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImvixPro.Services
{
    public sealed class MainWindowConfigurationSnapshot
    {
        public string LanguageCode { get; init; } = "en-US";

        public string SelectedLanguageCode { get; init; } = "System";

        public string SelectedThemeCode { get; init; } = "System";

        public OutputImageFormat OutputFormat { get; init; } = OutputImageFormat.Png;

        public CompressionMode CompressionMode { get; init; } = CompressionMode.Custom;

        public int Quality { get; init; } = 90;

        public ResizeMode ResizeMode { get; init; } = ResizeMode.None;

        public int ResizeWidth { get; init; } = 1280;

        public int ResizeHeight { get; init; } = 720;

        public int ResizePercent { get; init; } = 100;

        public RenameMode RenameMode { get; init; } = RenameMode.KeepOriginal;

        public string RenamePrefix { get; init; } = string.Empty;

        public string RenameSuffix { get; init; } = string.Empty;

        public int RenameStartNumber { get; init; } = 1;

        public int RenameNumberDigits { get; init; } = 4;

        public string OutputDirectory { get; init; } = string.Empty;

        public bool UseSourceFolder { get; init; } = true;

        public bool IncludeSubfoldersOnFolderImport { get; init; } = true;

        public bool AutoOpenOutputDirectory { get; init; } = true;

        public bool AllowOverwrite { get; init; }

        public bool SvgUseBackground { get; init; }

        public string SvgBackgroundColor { get; init; } = "#FFFFFFFF";

        public bool IconUseTransparency { get; init; } = true;

        public string IconBackgroundColor { get; init; } = "#FFFFFFFF";

        public GifHandlingMode GifHandlingMode { get; init; } = GifHandlingMode.FirstFrame;

        public int GifSpecificFrameIndex { get; init; }

        public bool AiEnhancementEnabled { get; init; }

        public int AiEnhancementScale { get; init; } = 2;

        public AiEnhancementModel AiEnhancementModel { get; init; } = AiEnhancementModel.General;

        public AiExecutionMode AiExecutionMode { get; init; } = AiExecutionMode.Auto;

        public PdfImageExportMode PdfImageExportMode { get; init; } = PdfImageExportMode.AllPages;

        public PdfDocumentExportMode PdfDocumentExportMode { get; init; } = PdfDocumentExportMode.AllPages;

        public int PdfPageIndex { get; init; }

        public int MaxParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);

        public IReadOnlyDictionary<string, int> GifSpecificFrameSelections { get; init; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, GifFrameRangeSelection> GifFrameRanges { get; init; } =
            new Dictionary<string, GifFrameRangeSelection>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, int> PdfPageSelections { get; init; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, PdfPageRangeSelection> PdfPageRanges { get; init; } =
            new Dictionary<string, PdfPageRangeSelection>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, bool> PdfUnlockStates { get; init; } =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public AiMattingModel AiMattingModel { get; init; } = AiMattingModel.GeneralClassic;

        public AiMattingDevice AiMattingDevice { get; init; } = AiMattingDevice.Cpu;

        public OutputImageFormat AiMattingOutputFormat { get; init; } = OutputImageFormat.Png;

        public AiMattingBackgroundMode AiMattingBackgroundMode { get; init; } = AiMattingBackgroundMode.Transparent;

        public string AiMattingBackgroundColor { get; init; } = "#FFFFFFFF";

        public bool AiMattingEdgeOptimizationEnabled { get; init; } = true;

        public int AiMattingEdgeOptimizationStrength { get; init; } = 35;

        public AiMattingResolutionMode AiMattingResolutionMode { get; init; } = AiMattingResolutionMode.Original;

        public int AiEraserDefaultBrushSize { get; init; } = AiEraserSettings.DefaultBrushSize;

        public int AiEraserMaskExpansionPixels { get; init; } = AiEraserSettings.DefaultMaskExpansionPixels;

        public int AiEraserEdgeBlendStrength { get; init; } = AiEraserSettings.DefaultEdgeBlendStrength;

        public bool WatchModeEnabled { get; init; }

        public string WatchInputDirectory { get; init; } = string.Empty;

        public string WatchOutputDirectory { get; init; } = string.Empty;

        public bool WatchIncludeSubfolders { get; init; } = true;

        public bool KeepRunningInTray { get; init; }

        public bool RunOnStartup { get; init; }

        public bool WindowsContextMenuEnabled { get; init; }

        public IReadOnlyList<ConversionPreset> Presets { get; init; } = [];
    }

    public sealed class MainWindowConfigurationCoordinator
    {
        public ConversionOptions BuildConversionOptions(MainWindowConfigurationSnapshot snapshot, bool forWatch = false)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new ConversionOptions
            {
                OutputFormat = snapshot.OutputFormat,
                CompressionMode = snapshot.CompressionMode,
                Quality = snapshot.Quality,
                ResizeMode = snapshot.ResizeMode,
                ResizeWidth = snapshot.ResizeWidth,
                ResizeHeight = snapshot.ResizeHeight,
                ResizePercent = snapshot.ResizePercent,
                RenameMode = snapshot.RenameMode,
                RenamePrefix = snapshot.RenamePrefix,
                RenameSuffix = snapshot.RenameSuffix,
                RenameStartNumber = snapshot.RenameStartNumber,
                RenameNumberDigits = snapshot.RenameNumberDigits,
                OutputDirectoryRule = forWatch
                    ? OutputDirectoryRule.SpecificFolder
                    : snapshot.UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                OutputDirectory = forWatch ? snapshot.WatchOutputDirectory : snapshot.OutputDirectory,
                AllowOverwrite = snapshot.AllowOverwrite,
                SvgUseBackground = snapshot.SvgUseBackground,
                SvgBackgroundColor = snapshot.SvgBackgroundColor,
                IconUseTransparency = snapshot.IconUseTransparency,
                IconBackgroundColor = snapshot.IconBackgroundColor,
                GifHandlingMode = snapshot.GifHandlingMode,
                GifSpecificFrameIndex = snapshot.GifSpecificFrameIndex,
                AiEnhancementEnabled = snapshot.AiEnhancementEnabled,
                AiEnhancementScale = snapshot.AiEnhancementScale,
                AiEnhancementModel = snapshot.AiEnhancementModel,
                AiExecutionMode = snapshot.AiExecutionMode,
                LanguageCode = snapshot.LanguageCode,
                GifSpecificFrameSelections = new Dictionary<string, int>(snapshot.GifSpecificFrameSelections, StringComparer.OrdinalIgnoreCase),
                GifFrameRanges = new Dictionary<string, GifFrameRangeSelection>(snapshot.GifFrameRanges, StringComparer.OrdinalIgnoreCase),
                PdfImageExportMode = snapshot.PdfImageExportMode,
                PdfDocumentExportMode = snapshot.PdfDocumentExportMode,
                PdfPageIndex = snapshot.PdfPageIndex,
                PdfPageSelections = new Dictionary<string, int>(snapshot.PdfPageSelections, StringComparer.OrdinalIgnoreCase),
                PdfPageRanges = new Dictionary<string, PdfPageRangeSelection>(snapshot.PdfPageRanges, StringComparer.OrdinalIgnoreCase),
                PdfUnlockStates = new Dictionary<string, bool>(snapshot.PdfUnlockStates, StringComparer.OrdinalIgnoreCase),
                MaxDegreeOfParallelism = snapshot.MaxParallelism
            };
        }

        public PreviewToolState BuildPreviewToolState(MainWindowConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new PreviewToolState
            {
                AiMattingModel = snapshot.AiMattingModel,
                AiMattingDevice = snapshot.AiMattingDevice,
                AiMattingOutputFormat = snapshot.AiMattingOutputFormat,
                AiMattingBackgroundMode = snapshot.AiMattingBackgroundMode,
                AiMattingBackgroundColor = snapshot.AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = snapshot.AiMattingEdgeOptimizationEnabled,
                AiMattingEdgeOptimizationStrength = snapshot.AiMattingEdgeOptimizationStrength,
                AiMattingResolutionMode = snapshot.AiMattingResolutionMode,
                AiEraserDefaultBrushSize = snapshot.AiEraserDefaultBrushSize,
                AiEraserMaskExpansionPixels = snapshot.AiEraserMaskExpansionPixels,
                AiEraserEdgeBlendStrength = snapshot.AiEraserEdgeBlendStrength
            };
        }

        public ConversionJobDefinition BuildJobDefinition(
            MainWindowConfigurationSnapshot snapshot,
            bool forWatch = false,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new ConversionJobDefinition
            {
                Name = name ?? string.Empty,
                Options = BuildConversionOptions(snapshot, forWatch)
            };
        }

        public PreviewSessionState BuildPreviewSessionState(MainWindowConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new PreviewSessionState
            {
                JobDefinition = BuildJobDefinition(snapshot),
                PreviewToolState = BuildPreviewToolState(snapshot)
            };
        }

        public ApplicationPreferences BuildApplicationPreferences(AppSettings existingSettings, MainWindowConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(existingSettings);
            ArgumentNullException.ThrowIfNull(snapshot);

            var existingPreferences = AppSettingsStateMapper.ResolveApplicationPreferences(existingSettings);

            return new ApplicationPreferences
            {
                LanguageCode = snapshot.SelectedLanguageCode,
                ThemeCode = snapshot.SelectedThemeCode,
                DefaultOutputFormat = snapshot.OutputFormat,
                DefaultCompressionMode = snapshot.CompressionMode,
                DefaultQuality = snapshot.Quality,
                DefaultResizeMode = snapshot.ResizeMode,
                DefaultResizeWidth = snapshot.ResizeWidth,
                DefaultResizeHeight = snapshot.ResizeHeight,
                DefaultResizePercent = snapshot.ResizePercent,
                DefaultRenameMode = snapshot.RenameMode,
                DefaultRenamePrefix = snapshot.RenamePrefix,
                DefaultRenameSuffix = snapshot.RenameSuffix,
                DefaultRenameStartNumber = snapshot.RenameStartNumber,
                DefaultRenameNumberDigits = snapshot.RenameNumberDigits,
                DefaultOutputDirectory = snapshot.OutputDirectory,
                UseSourceFolderByDefault = snapshot.UseSourceFolder,
                HasOutputDirectoryRule = true,
                OutputDirectoryRule = snapshot.UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                IncludeSubfoldersOnFolderImport = snapshot.IncludeSubfoldersOnFolderImport,
                DefaultListSortMode = existingPreferences.DefaultListSortMode,
                AutoOpenOutputDirectory = snapshot.AutoOpenOutputDirectory,
                AllowOverwrite = snapshot.AllowOverwrite,
                SvgUseBackground = snapshot.SvgUseBackground,
                SvgBackgroundColor = snapshot.SvgBackgroundColor,
                IconUseTransparency = snapshot.IconUseTransparency,
                IconBackgroundColor = snapshot.IconBackgroundColor,
                DefaultGifHandlingMode = snapshot.GifHandlingMode,
                DefaultGifSpecificFrameIndex = snapshot.GifSpecificFrameIndex,
                MaxParallelism = snapshot.MaxParallelism,
                AiEnhancementEnabled = snapshot.AiEnhancementEnabled,
                DefaultAiEnhancementScale = snapshot.AiEnhancementScale,
                DefaultAiEnhancementModel = snapshot.AiEnhancementModel,
                DefaultAiExecutionMode = snapshot.AiExecutionMode,
                Presets = snapshot.Presets.Select(ClonePreset).ToList(),
                KeepRunningInTray = snapshot.KeepRunningInTray,
                RunOnStartup = snapshot.RunOnStartup,
                WindowsContextMenuEnabled = snapshot.WindowsContextMenuEnabled
            };
        }

        public WatchProfile BuildWatchProfile(
            MainWindowConfigurationSnapshot snapshot,
            ConversionJobDefinition? watchJobDefinitionSnapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var jobDefinition = watchJobDefinitionSnapshot?.Clone() ?? BuildJobDefinition(snapshot, forWatch: true);
            var options = jobDefinition.ToConversionOptions();
            options.OutputDirectoryRule = OutputDirectoryRule.SpecificFolder;
            options.OutputDirectory = snapshot.WatchOutputDirectory;
            jobDefinition.Options = options;

            return new WatchProfile
            {
                IsEnabled = snapshot.WatchModeEnabled,
                InputDirectory = snapshot.WatchInputDirectory,
                OutputDirectory = snapshot.WatchOutputDirectory,
                IncludeSubfolders = snapshot.WatchIncludeSubfolders,
                JobDefinition = jobDefinition
            };
        }

        public ConversionPreset BuildPreset(MainWindowConfigurationSnapshot snapshot, string name)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new ConversionPreset
            {
                Name = name,
                OutputFormat = snapshot.OutputFormat,
                CompressionMode = snapshot.CompressionMode,
                Quality = snapshot.Quality,
                ResizeMode = snapshot.ResizeMode,
                ResizeWidth = snapshot.ResizeWidth,
                ResizeHeight = snapshot.ResizeHeight,
                ResizePercent = snapshot.ResizePercent,
                RenameMode = snapshot.RenameMode,
                RenamePrefix = snapshot.RenamePrefix,
                RenameSuffix = snapshot.RenameSuffix,
                RenameStartNumber = snapshot.RenameStartNumber,
                RenameNumberDigits = snapshot.RenameNumberDigits,
                GifHandlingMode = snapshot.GifHandlingMode,
                GifSpecificFrameIndex = snapshot.GifSpecificFrameIndex,
                AiEnhancementEnabled = snapshot.AiEnhancementEnabled,
                AiEnhancementScale = snapshot.AiEnhancementScale,
                AiEnhancementModel = snapshot.AiEnhancementModel,
                AiExecutionMode = snapshot.AiExecutionMode,
                OutputDirectoryRule = snapshot.UseSourceFolder ? OutputDirectoryRule.SourceFolder : OutputDirectoryRule.SpecificFolder,
                OutputDirectory = snapshot.OutputDirectory,
                AllowOverwrite = snapshot.AllowOverwrite,
                SvgUseBackground = snapshot.SvgUseBackground,
                SvgBackgroundColor = snapshot.SvgBackgroundColor,
                IconUseTransparency = snapshot.IconUseTransparency,
                IconBackgroundColor = snapshot.IconBackgroundColor
            };
        }

        public static ConversionPreset ClonePreset(ConversionPreset source)
        {
            ArgumentNullException.ThrowIfNull(source);

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
                OutputDirectoryRule = source.OutputDirectoryRule,
                OutputDirectory = source.OutputDirectory,
                AllowOverwrite = source.AllowOverwrite,
                SvgUseBackground = source.SvgUseBackground,
                SvgBackgroundColor = source.SvgBackgroundColor,
                IconUseTransparency = source.IconUseTransparency,
                IconBackgroundColor = source.IconBackgroundColor,
                AutoResultFolderName = source.AutoResultFolderName
            };
        }
    }
}
