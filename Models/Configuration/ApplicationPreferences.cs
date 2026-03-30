using System;
using System.Collections.Generic;

namespace ImvixPro.Models
{
    public sealed class ApplicationPreferences
    {
        public string LanguageCode { get; set; } = "System";

        public string ThemeCode { get; set; } = "System";

        public OutputImageFormat DefaultOutputFormat { get; set; } = OutputImageFormat.Png;

        public CompressionMode DefaultCompressionMode { get; set; } = CompressionMode.Custom;

        public int DefaultQuality { get; set; } = 90;

        public ResizeMode DefaultResizeMode { get; set; } = ResizeMode.None;

        public int DefaultResizeWidth { get; set; } = 1280;

        public int DefaultResizeHeight { get; set; } = 720;

        public int DefaultResizePercent { get; set; } = 100;

        public RenameMode DefaultRenameMode { get; set; } = RenameMode.KeepOriginal;

        public string DefaultRenamePrefix { get; set; } = string.Empty;

        public string DefaultRenameSuffix { get; set; } = string.Empty;

        public int DefaultRenameStartNumber { get; set; } = 1;

        public int DefaultRenameNumberDigits { get; set; } = 4;

        public string DefaultOutputDirectory { get; set; } = string.Empty;

        public bool UseSourceFolderByDefault { get; set; } = true;

        public bool HasOutputDirectoryRule { get; set; }

        public OutputDirectoryRule OutputDirectoryRule { get; set; } = OutputDirectoryRule.SourceFolder;

        public bool IncludeSubfoldersOnFolderImport { get; set; } = true;

        public ListSortMode DefaultListSortMode { get; set; } = ListSortMode.NameAscending;

        public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount);

        public bool AutoOpenOutputDirectory { get; set; } = true;

        public bool AllowOverwrite { get; set; }

        public bool SvgUseBackground { get; set; }

        public string SvgBackgroundColor { get; set; } = "#FFFFFFFF";

        public bool IconUseTransparency { get; set; } = true;

        public string IconBackgroundColor { get; set; } = "#FFFFFFFF";

        public GifHandlingMode DefaultGifHandlingMode { get; set; } = GifHandlingMode.FirstFrame;

        public int DefaultGifSpecificFrameIndex { get; set; }

        public bool AiEnhancementEnabled { get; set; }

        public int DefaultAiEnhancementScale { get; set; } = 2;

        public AiEnhancementModel DefaultAiEnhancementModel { get; set; } = AiEnhancementModel.General;

        public AiExecutionMode DefaultAiExecutionMode { get; set; } = AiExecutionMode.Auto;

        public List<ConversionPreset> Presets { get; set; } = [];

        public bool KeepRunningInTray { get; set; }

        public bool RunOnStartup { get; set; }

        public bool WindowsContextMenuEnabled { get; set; }
    }
}
