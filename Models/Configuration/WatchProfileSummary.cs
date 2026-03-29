namespace ImvixPro.Models
{
    public sealed class WatchProfileSummary
    {
        public WatchProfileSummary(
            OutputImageFormat outputFormat,
            string outputDirectory,
            int aiEnhancementScale,
            ResizeMode resizeMode,
            GifHandlingMode gifHandlingMode,
            bool allowOverwrite,
            ConversionRuleSummary ruleSummary)
        {
            OutputFormat = outputFormat;
            OutputDirectory = outputDirectory;
            AiEnhancementScale = aiEnhancementScale;
            ResizeMode = resizeMode;
            GifHandlingMode = gifHandlingMode;
            AllowOverwrite = allowOverwrite;
            RuleSummary = ruleSummary ?? throw new System.ArgumentNullException(nameof(ruleSummary));
        }

        public OutputImageFormat OutputFormat { get; }

        public string OutputDirectory { get; }

        public bool HasOutputDirectory => !string.IsNullOrWhiteSpace(OutputDirectory);

        public ConversionRuleSummary RuleSummary { get; }

        public bool AiEnhancementEnabled => RuleSummary.Ai.IsEnabled;

        public int AiEnhancementScale { get; }

        public ResizeMode ResizeMode { get; }

        public bool UsesResize => ResizeMode != ResizeMode.None;

        public GifHandlingMode GifHandlingMode { get; }

        public bool UsesConfiguredGifHandling => !ExpandsGifFramesDuringWatch && GifHandlingMode != GifHandlingMode.FirstFrame;

        public bool AllowOverwrite { get; }

        public bool ExpandsGifFramesDuringWatch => RuleSummary.Expansion.ExpandsGifFrames;

        public bool ExpandsPdfPagesDuringWatch => RuleSummary.Expansion.ExpandsPdfPages;
    }
}
