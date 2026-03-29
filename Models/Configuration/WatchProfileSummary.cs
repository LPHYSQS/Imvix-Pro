namespace ImvixPro.Models
{
    public sealed class WatchProfileSummary
    {
        public WatchProfileSummary(
            OutputImageFormat outputFormat,
            string outputDirectory,
            bool aiEnhancementEnabled,
            int aiEnhancementScale,
            ResizeMode resizeMode,
            GifHandlingMode gifHandlingMode,
            bool allowOverwrite,
            bool expandsGifFramesDuringWatch,
            bool expandsPdfPagesDuringWatch)
        {
            OutputFormat = outputFormat;
            OutputDirectory = outputDirectory;
            AiEnhancementEnabled = aiEnhancementEnabled;
            AiEnhancementScale = aiEnhancementScale;
            ResizeMode = resizeMode;
            GifHandlingMode = gifHandlingMode;
            AllowOverwrite = allowOverwrite;
            ExpandsGifFramesDuringWatch = expandsGifFramesDuringWatch;
            ExpandsPdfPagesDuringWatch = expandsPdfPagesDuringWatch;
        }

        public OutputImageFormat OutputFormat { get; }

        public string OutputDirectory { get; }

        public bool HasOutputDirectory => !string.IsNullOrWhiteSpace(OutputDirectory);

        public bool AiEnhancementEnabled { get; }

        public int AiEnhancementScale { get; }

        public ResizeMode ResizeMode { get; }

        public bool UsesResize => ResizeMode != ResizeMode.None;

        public GifHandlingMode GifHandlingMode { get; }

        public bool UsesConfiguredGifHandling => !ExpandsGifFramesDuringWatch && GifHandlingMode != GifHandlingMode.FirstFrame;

        public bool AllowOverwrite { get; }

        public bool ExpandsGifFramesDuringWatch { get; }

        public bool ExpandsPdfPagesDuringWatch { get; }
    }
}
