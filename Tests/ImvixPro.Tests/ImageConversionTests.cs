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

    [Fact]
    public void RuntimeStatusSummary_SeparatesGifPdfFrameProgressFromCompletionState()
    {
        var summaryService = new ConversionStatusSummaryService();
        var runtime = summaryService.CreateProgressRuntimeStatus(
            new ConversionProgress(
                processedCount: 12,
                totalCount: 24,
                processedFileCount: 0,
                totalFileCount: 1,
                fileName: "animated.gif",
                currentFileProcessedCount: 12,
                currentFileTotalCount: 48,
                isFileCompleted: false,
                succeeded: false,
                error: null,
                stage: ConversionStage.Conversion),
            new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Pdf
            },
            remainingCount: 1,
            progressPercent: 50);

        Assert.Equal("StatusProcessingGifFrames", runtime.StatusKey);
        Assert.Equal("animated.gif", runtime.CurrentItemName);
        Assert.True(runtime.HasCurrentSubItemProgress);
        Assert.Equal(12, runtime.CurrentSubItemIndex);
        Assert.Equal(48, runtime.CurrentSubItemCount);
    }

    [Fact]
    public void CompletionSummaryModel_ReusesSharedCountsAcrossDialogAndHistory()
    {
        var summaryService = new ConversionStatusSummaryService();
        var presenter = new ConversionTextPresenter();
        var completion = summaryService.CreateCompletionSummary(
            new ConversionSummary(
                totalCount: 9,
                processedCount: 7,
                successCount: 5,
                failures: [new ConversionFailure("a.png", "failed"), new ConversionFailure("b.png", "failed")],
                outputDirectories: [],
                duration: TimeSpan.FromSeconds(18),
                wasCanceled: true),
            ConversionTriggerSource.Manual,
            OutputImageFormat.Webp,
            new SizeEstimateResult(true, 2_048, 1_024, 1_536),
            @"C:\logs\failures.txt");

        Assert.Equal(ConversionTriggerSource.Manual, completion.Source);
        Assert.Equal(OutputImageFormat.Webp, completion.OutputFormat);
        Assert.Equal(9, completion.TotalCount);
        Assert.Equal(7, completion.ProcessedCount);
        Assert.Equal(5, completion.SuccessCount);
        Assert.Equal(2, completion.FailureCount);
        Assert.True(completion.WasCanceled);
        Assert.Equal(2, completion.RemainingCount);
        Assert.True(completion.HasFailureLog);

        var dialogText = presenter.BuildCompletionSummaryText(completion, TranslateTestText);
        var historySummary = presenter.BuildHistorySummaryText(completion, TranslateTestText);
        var historyDetail = presenter.BuildHistoryDetailText(completion, TranslateTestText);

        Assert.Contains("Total: 9", dialogText);
        Assert.Contains("Processed: 7", dialogText);
        Assert.Contains("Canceled: Yes", dialogText);
        Assert.Equal("Manual · WEBP · Processed 7/9 before cancellation", historySummary);
        Assert.Equal("Input 2.0 KB · Estimate 1.0 KB - 1.5 KB · Duration 00:18.00", historyDetail);
    }

    [Fact]
    public void WatchRuntimeStatusSummary_FormatsProcessingThroughSharedPresenter()
    {
        var summaryService = new ConversionStatusSummaryService();
        var presenter = new ConversionTextPresenter();
        var runtime = summaryService.CreateProgressRuntimeStatus(
            new ConversionProgress(
                processedCount: 12,
                totalCount: 24,
                processedFileCount: 0,
                totalFileCount: 1,
                fileName: "animated.gif",
                currentFileProcessedCount: 12,
                currentFileTotalCount: 48,
                isFileCompleted: false,
                succeeded: false,
                error: null,
                stage: ConversionStage.Conversion),
            new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Pdf
            },
            remainingCount: 1,
            progressPercent: 50);
        var watchStatus = summaryService.CreateWatchProcessingStatus(
            runtime,
            @"C:\watch-in",
            processedCount: 3,
            failureCount: 1);

        Assert.Equal("Processing watched file: animated.gif - frame 12 / 48", presenter.BuildWatchStatusText(watchStatus, TranslateTestText));
        Assert.Equal("Processed: 3  Failed: 1", presenter.BuildWatchMetricsText(watchStatus, TranslateTestText));
    }

    [Fact]
    public void WatchRuntimeStatusSummary_FormatsFailureCompletionThroughSharedPresenter()
    {
        var summaryService = new ConversionStatusSummaryService();
        var presenter = new ConversionTextPresenter();
        var completion = summaryService.CreateCompletionSummary(
            new ConversionSummary(
                totalCount: 1,
                processedCount: 1,
                successCount: 0,
                failures: [new ConversionFailure("sample.png", "disk full")],
                outputDirectories: [],
                duration: TimeSpan.FromSeconds(3),
                wasCanceled: false),
            ConversionTriggerSource.Watch,
            OutputImageFormat.Png,
            new SizeEstimateResult(true, 4_096, 4_096, 4_096),
            string.Empty);
        var watchStatus = summaryService.CreateWatchCompletionStatus(
            completion,
            "sample.png",
            "disk full",
            @"C:\watch-in",
            processedCount: 0,
            failureCount: 4);

        Assert.Equal("sample.png failed: disk full", presenter.BuildWatchStatusText(watchStatus, TranslateTestText));
    }

    private static string TranslateTestText(string key)
    {
        return key switch
        {
            "NoCurrentFile" => "None",
            "GifPdfProgressTemplate" => "{0} - frame {1} / {2}",
            "SummaryTotal" => "Total",
            "SummaryProcessed" => "Processed",
            "SummarySuccess" => "Success",
            "SummaryFailed" => "Failed",
            "SummaryCanceled" => "Canceled",
            "SummaryDuration" => "Duration",
            "YesText" => "Yes",
            "HistorySourceManual" => "Manual",
            "HistorySourceWatch" => "Watch",
            "HistorySummaryCanceledTemplate" => "{0} · {1} · Processed {2}/{3} before cancellation",
            "HistorySummaryTemplate" => "{0} · {1} · {2} total / {3} success / {4} failed",
            "HistoryDetailTemplate" => "Input {0} · Estimate {1} · Duration {2}",
            "WatchStatusStopped" => "Watch mode stopped",
            "WatchStatusWaiting" => "Watch mode is ready. Enable it after selecting folders.",
            "WatchStatusRunning" => "Watching: {0}",
            "WatchStatusProcessing" => "Processing watched file: {0}",
            "WatchStatusSingleFailureTemplate" => "{0} failed: {1}",
            "WatchStatusProcessedTemplate" => "{0} converted successfully",
            "WatchStatusErrorTemplate" => "Watch mode error: {0}",
            "WatchMetricsTemplate" => "Processed: {0}  Failed: {1}",
            "StatusCompletedWithFailures" => "Completed with failures",
            "StatusCanceled" => "Canceled",
            _ => key
        };
    }
}
