using ImvixPro.Models;
using ImvixPro.Services;

namespace ImvixPro.Tests;

public sealed class ImageConversionTests
{
    [Theory]
    [InlineData(OutputImageFormat.Png, ".png", true)]
    [InlineData(OutputImageFormat.Jpeg, ".jpg", false)]
    [InlineData(OutputImageFormat.Webp, ".webp", true)]
    [InlineData(OutputImageFormat.Bmp, ".bmp", true)]
    [InlineData(OutputImageFormat.Gif, ".gif", false)]
    [InlineData(OutputImageFormat.Tiff, ".tiff", true)]
    [InlineData(OutputImageFormat.Ico, ".ico", true)]
    [InlineData(OutputImageFormat.Svg, ".svg", true)]
    public void OutputFormatMetadata_RemainsStable(OutputImageFormat format, string expectedExtension, bool supportsTransparency)
    {
        Assert.Equal(expectedExtension, ImageConversionService.GetFileExtension(format));
        Assert.Equal(supportsTransparency, ImageConversionService.OutputFormatSupportsTransparency(format));
    }

    [Fact]
    public void SupportedInputExtensions_ContainCoreFormats()
    {
        string[] expectedExtensions = [".png", ".jpg", ".gif", ".pdf", ".psd", ".exe", ".lnk"];

        foreach (var extension in expectedExtensions)
        {
            Assert.Contains(extension, ImageConversionService.SupportedInputExtensions);
        }
    }

    [Fact]
    public void WatchRuleSummary_ExposesSharedAiFallbackAndAutoExpansionPolicy()
    {
        var planningService = new ConversionPlanningService(new ImageAnalysisService());
        var ruleSummary = planningService.BuildWatchRuleSummary(new ConversionOptions
        {
            OutputFormat = OutputImageFormat.Webp,
            AiEnhancementEnabled = true
        });

        Assert.True(ruleSummary.Ai.IsEnabled);
        Assert.True(ruleSummary.Ai.SkipsUnsupportedInputs);
        Assert.False(ruleSummary.Ai.HasKnownCoverage);
        Assert.True(ruleSummary.Expansion.ExpandsGifFrames);
        Assert.True(ruleSummary.Expansion.ExpandsPdfPages);
    }

    [Theory]
    [InlineData(OutputImageFormat.Gif, false, true)]
    [InlineData(OutputImageFormat.Pdf, true, false)]
    public void WatchRuleSummary_RespectsOutputSpecificExpansionDefaults(
        OutputImageFormat outputFormat,
        bool expectedGifExpansion,
        bool expectedPdfExpansion)
    {
        var planningService = new ConversionPlanningService(new ImageAnalysisService());
        var ruleSummary = planningService.BuildWatchRuleSummary(new ConversionOptions
        {
            OutputFormat = outputFormat
        });

        Assert.Equal(expectedGifExpansion, ruleSummary.Expansion.ExpandsGifFrames);
        Assert.Equal(expectedPdfExpansion, ruleSummary.Expansion.ExpandsPdfPages);
    }

    [Fact]
    public void BuildPlan_ExposesHighCompressionAndAiEstimateDiagnostics()
    {
        var planningService = new ConversionPlanningService(new ImageAnalysisService());
        var image = ImageItemViewModel.CreateImported(
            @"C:\diagnostics\sample.png",
            fileSize: 1_024,
            pixelWidth: 800,
            pixelHeight: 600,
            thumbnail: null,
            gifFrameCount: 1);

        var plan = planningService.BuildPlan(
            [image],
            new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Webp,
                CompressionMode = CompressionMode.HighCompression,
                AiEnhancementEnabled = true
            });

        Assert.True(plan.HasHighCompressionRisk);
        Assert.True(plan.HasEstimateDisclaimer);
        Assert.True(plan.Diagnostics.EstimateDisclaimer.IncludesAiScaleAdjustment);
        Assert.False(plan.Diagnostics.EstimateDisclaimer.IncludesExpandedOutputs);
        Assert.False(plan.HasLockedPdfInputs);
    }

    [Fact]
    public void BuildPlan_ExposesLargeGifPdfAndLockedPdfDiagnostics()
    {
        var planningService = new ConversionPlanningService(new ImageAnalysisService());
        var animatedGif = ImageItemViewModel.CreateImported(
            @"C:\diagnostics\animated.gif",
            fileSize: 4_096,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 720);
        var lockedPdf = ImageItemViewModel.CreateImported(
            @"C:\diagnostics\locked.pdf",
            fileSize: 2_048,
            pixelWidth: 0,
            pixelHeight: 0,
            thumbnail: null,
            gifFrameCount: 1,
            pdfPageCount: 3,
            isPdfDocument: true,
            isEncrypted: true,
            isUnlocked: false);

        var plan = planningService.BuildPlan(
            [animatedGif, lockedPdf],
            new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Pdf,
                GifHandlingMode = GifHandlingMode.AllFrames,
                PdfImageExportMode = PdfImageExportMode.AllPages
            });

        Assert.True(plan.HasLargeGifPdfFrameRisk);
        Assert.Equal(720, plan.LargeGifPdfFrameCount);
        Assert.True(plan.HasLockedPdfInputs);
        Assert.Equal(1, plan.LockedPdfInputCount);
        Assert.True(plan.HasEstimateDisclaimer);
        Assert.False(plan.Diagnostics.EstimateDisclaimer.IncludesAiScaleAdjustment);
        Assert.True(plan.Diagnostics.EstimateDisclaimer.IncludesExpandedOutputs);
    }
}
