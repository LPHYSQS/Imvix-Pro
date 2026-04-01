using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ImvixPro.AI.Matting.Models;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.ViewModels;
using ImvixPro.Views;

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
    public void ConversionSummaryCoordinator_UnifiesDialogAndHistoryOutputsForManualCompletion()
    {
        var coordinator = new ConversionSummaryCoordinator(
            new ConversionStatusSummaryService(),
            new ConversionTextPresenter());
        var completion = new CompletionSummaryModel(
            ConversionTriggerSource.Manual,
            OutputImageFormat.Png,
            TotalCount: 3,
            ProcessedCount: 3,
            SuccessCount: 2,
            FailureCount: 1,
            Duration: TimeSpan.FromSeconds(5),
            WasCanceled: false,
            OriginalTotalBytes: 4_096,
            EstimatedMinBytes: 2_048,
            EstimatedMaxBytes: 3_072,
            FailureLogPath: @"C:\logs\manual.log");
        var timestamp = new DateTimeOffset(2026, 3, 30, 10, 15, 0, TimeSpan.Zero);

        var flow = coordinator.BuildCompletionFlow(
            completion,
            TranslateTestText,
            includeDialog: true,
            timestamp);

        Assert.Equal(completion, flow.Summary);
        Assert.True(flow.HasDialogRequest);
        Assert.Equal("Summary", flow.DialogRequest!.Title);
        Assert.Contains("Total: 3", flow.DialogRequest.SummaryText);
        Assert.Equal("Close", flow.DialogRequest.CloseButtonText);
        Assert.Equal(timestamp, flow.HistoryEntry.Timestamp);
        Assert.Equal(completion.FailureLogPath, flow.HistoryEntry.FailureLogPath);

        var historyItem = coordinator.CreateHistoryItem(flow.HistoryEntry, TranslateTestText);
        Assert.Equal("Manual · PNG · 3 total / 2 success / 1 failed", historyItem.SummaryText);
        Assert.Equal("Input 4.0 KB · Estimate 2.0 KB - 3.0 KB · Duration 00:05.00", historyItem.DetailText);
        Assert.True(historyItem.HasFailureLog);
    }

    [Fact]
    public void ConversionSummaryCoordinator_SkipsDialogForWatchCompletionWhileKeepingHistoryEntry()
    {
        var coordinator = new ConversionSummaryCoordinator(
            new ConversionStatusSummaryService(),
            new ConversionTextPresenter());
        var flow = coordinator.BuildCompletionFlow(
            new ConversionSummary(
                totalCount: 1,
                processedCount: 1,
                successCount: 1,
                failures: [],
                outputDirectories: [],
                duration: TimeSpan.FromSeconds(2),
                wasCanceled: false),
            ConversionTriggerSource.Watch,
            OutputImageFormat.Webp,
            new SizeEstimateResult(true, 1_024, 512, 768),
            failureLogPath: null,
            TranslateTestText,
            includeDialog: false,
            timestamp: new DateTimeOffset(2026, 3, 30, 12, 0, 0, TimeSpan.Zero));

        Assert.False(flow.HasDialogRequest);
        Assert.Equal(ConversionTriggerSource.Watch, flow.HistoryEntry.Source);
        Assert.Equal(OutputImageFormat.Webp, flow.HistoryEntry.OutputFormat);
        Assert.Equal(string.Empty, flow.HistoryEntry.FailureLogPath);
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

    [Fact]
    public void NotificationState_HoldsPendingDialogUntilTheShownRequestIsConsumed()
    {
        var state = new NotificationState();
        var dialogRequest = new ConversionSummaryDialogRequest("Summary", "Done", "Close");
        var flow = new ConversionSummaryFlowResult(
            new CompletionSummaryModel(
                ConversionTriggerSource.Manual,
                OutputImageFormat.Png,
                TotalCount: 1,
                ProcessedCount: 1,
                SuccessCount: 1,
                FailureCount: 0,
                Duration: TimeSpan.FromSeconds(1),
                WasCanceled: false,
                OriginalTotalBytes: 128,
                EstimatedMinBytes: 128,
                EstimatedMaxBytes: 128,
                FailureLogPath: @"C:\logs\manual.log"),
            new ConversionHistoryEntry
            {
                Timestamp = new DateTimeOffset(2026, 3, 30, 15, 0, 0, TimeSpan.Zero),
                Source = ConversionTriggerSource.Manual,
                OutputFormat = OutputImageFormat.Png
            },
            dialogRequest);

        state.ApplyCompletionFlow(flow);

        Assert.True(state.HasFailureLog);
        Assert.Equal(@"C:\logs\manual.log", state.LastFailureLogPath);
        Assert.True(state.HasPendingDialogRequest);
        Assert.Same(dialogRequest, state.PendingDialogRequest);

        state.ClearPendingDialogRequest(new ConversionSummaryDialogRequest("Summary", "Done", "Close"));
        Assert.True(state.HasPendingDialogRequest);

        state.ClearPendingDialogRequest(dialogRequest);
        Assert.False(state.HasPendingDialogRequest);
    }

    [Fact]
    public void NotificationState_ResetFailureLog_DoesNotDiscardPendingDialog()
    {
        var state = new NotificationState();
        var dialogRequest = new ConversionSummaryDialogRequest("Summary", "Done", "Close");

        state.ApplyCompletionFlow(new ConversionSummaryFlowResult(
            new CompletionSummaryModel(
                ConversionTriggerSource.Manual,
                OutputImageFormat.Webp,
                TotalCount: 2,
                ProcessedCount: 2,
                SuccessCount: 1,
                FailureCount: 1,
                Duration: TimeSpan.FromSeconds(2),
                WasCanceled: false,
                OriginalTotalBytes: 512,
                EstimatedMinBytes: 256,
                EstimatedMaxBytes: 384,
                FailureLogPath: @"C:\logs\failure.log"),
            new ConversionHistoryEntry
            {
                Timestamp = new DateTimeOffset(2026, 3, 30, 16, 0, 0, TimeSpan.Zero),
                Source = ConversionTriggerSource.Manual,
                OutputFormat = OutputImageFormat.Webp
            },
            dialogRequest));

        state.ResetFailureLog();

        Assert.False(state.HasFailureLog);
        Assert.Equal(string.Empty, state.LastFailureLogPath);
        Assert.True(state.HasPendingDialogRequest);
        Assert.Same(dialogRequest, state.PendingDialogRequest);
    }

    [Fact]
    public void TaskAnalysisState_ApplySnapshot_ReplacesFlattenedSidebarState()
    {
        var state = new TaskAnalysisState();

        state.Apply(
            new TaskAnalysisSnapshot(
                activeWarnings: ["warning-a", "warning-b"],
                conversionPlanHighlights: ["plan-a", "plan-b"],
                formatRecommendationText: "WEBP / JPEG",
                formatRecommendationReasonText: "photo",
                originalSizeSummaryText: "Input 2.0 KB",
                estimatedSizeSummaryText: "Estimate 1.0 KB - 1.5 KB",
                estimateDisclaimerText: "AI disclaimer"),
            TranslateTestText);

        Assert.Equal("Warnings", state.WarningsTitleText);
        Assert.Equal("Task analysis", state.TaskAnalysisTitleText);
        Assert.Equal("Recommended format", state.FormatRecommendationTitleText);
        Assert.Equal("Estimated size", state.EstimatedSizeTitleText);
        Assert.True(state.HasActiveWarnings);
        Assert.True(state.HasConversionPlanHighlights);
        Assert.True(state.HasFormatRecommendation);
        Assert.True(state.HasSizeEstimate);
        Assert.True(state.HasEstimateDisclaimer);
        Assert.Equal(2, state.ActiveWarnings.Count);
        Assert.Equal(2, state.ConversionPlanHighlights.Count);

        state.Apply(
            new TaskAnalysisSnapshot(
                activeWarnings: [],
                conversionPlanHighlights: [],
                formatRecommendationText: string.Empty,
                formatRecommendationReasonText: string.Empty,
                originalSizeSummaryText: string.Empty,
                estimatedSizeSummaryText: string.Empty,
                estimateDisclaimerText: string.Empty),
            TranslateTestText);

        Assert.False(state.HasActiveWarnings);
        Assert.False(state.HasConversionPlanHighlights);
        Assert.False(state.HasFormatRecommendation);
        Assert.False(state.HasSizeEstimate);
        Assert.False(state.HasEstimateDisclaimer);
        Assert.Empty(state.ActiveWarnings);
        Assert.Empty(state.ConversionPlanHighlights);
    }

    [Fact]
    public void PreviewSelectionState_ApplySnapshot_ReplacesFlattenedPreviewBindings()
    {
        var state = new PreviewSelectionState();

        state.Apply(new PreviewSelectionSnapshot
        {
            PreviewSelectionFileText = "sample.pdf",
            IsPreviewWindowHintVisible = true,
            IsPdfSelected = true,
            IsPdfNavigationVisible = true,
            CanGoToPreviousPdfPage = true,
            CanGoToNextPdfPage = false,
            IsSelectedPdfLocked = true,
            IsSelectedPdfUnlocked = false,
            PdfPageIndicatorText = "Page 2 / 9",
            PdfPageRangeIndicatorText = "Pages 2 - 4",
            PdfLockedPreviewTitleText = "Locked",
            PdfLockedPreviewDescriptionText = "Need password",
            PdfLockedActionHintText = "Unlock first",
            IsPdfImageExportVisible = true,
            IsPdfDocumentExportVisible = false,
            IsPdfImageModeSelectorVisible = true,
            IsPdfImagePageSliderVisible = true,
            IsPdfImageCurrentPageMode = true,
            PdfPageMinimum = 0,
            PdfPageMaximum = 8,
            SelectedPdfPageIndex = 1,
            IsGifPreviewVisible = true,
            IsGifHandlingSelectorVisible = true,
            IsGifPdfModeSelectorVisible = false,
            IsGifSpecificFrameControlsVisible = true,
            GifSpecificFrameMaximum = 23,
            GifSpecificFrameSliderValue = 7,
            CanAdjustGifSpecificFrame = true,
            IsGifSpecificFramePlaybackPaused = true,
            GifSpecificFrameCountdownText = "8 / 24",
            IsGifTrimRangeVisible = true,
            GifTrimRangeMinimum = 0,
            GifTrimRangeMaximum = 23,
            SelectedGifTrimStartIndex = 2,
            SelectedGifTrimEndIndex = 12,
            IsSvgPreviewVisible = true,
            IsSvgBackgroundToggleVisible = true,
            IsSvgBackgroundToggleEnabled = true,
            IsSvgBackgroundRequiredHintVisible = false,
            IsSvgBackgroundColorVisible = true,
            IsIconPreviewVisible = false,
            IsIconTransparencyToggleVisible = false,
            IsIconBackgroundColorVisible = false
        });

        Assert.Equal("sample.pdf", state.PreviewSelectionFileText);
        Assert.True(state.IsPreviewWindowHintVisible);
        Assert.True(state.IsPdfSelected);
        Assert.True(state.IsPdfNavigationVisible);
        Assert.True(state.CanGoToPreviousPdfPage);
        Assert.False(state.CanGoToNextPdfPage);
        Assert.True(state.IsSelectedPdfLocked);
        Assert.Equal("Page 2 / 9", state.PdfPageIndicatorText);
        Assert.True(state.IsPdfImageExportVisible);
        Assert.Equal(8, state.PdfPageMaximum);
        Assert.True(state.IsGifPreviewVisible);
        Assert.True(state.IsGifSpecificFrameControlsVisible);
        Assert.Equal(7d, state.GifSpecificFrameSliderValue);
        Assert.True(state.IsGifTrimRangeVisible);
        Assert.True(state.IsSvgPreviewVisible);
        Assert.True(state.IsSvgBackgroundColorVisible);
        Assert.False(state.IsIconPreviewVisible);

        state.Apply(new PreviewSelectionSnapshot());

        Assert.Equal(string.Empty, state.PreviewSelectionFileText);
        Assert.False(state.IsPreviewWindowHintVisible);
        Assert.False(state.IsPdfSelected);
        Assert.False(state.IsGifPreviewVisible);
        Assert.False(state.IsSvgPreviewVisible);
        Assert.False(state.IsIconPreviewVisible);
    }

    [Fact]
    public void PreviewRenderCoordinator_SelectedImageChanged_UsesPdfPreviewBranch()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext();
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\document.pdf",
            fileSize: 2_048,
            pixelWidth: 0,
            pixelHeight: 0,
            thumbnail: null,
            gifFrameCount: 1,
            pdfPageCount: 4,
            isPdfDocument: true);

        coordinator.HandleSelectedImageChanged(image, context);

        Assert.Equal(
            [
                "CancelPendingPdfPreviewRender",
                "CancelPendingSelectedPsdPreviewRender",
                "ClearSelectedPreview",
                "RefreshGifHandlingModeOptions",
                "RestoreGifSpecificFrameSelection:C:\\preview\\document.pdf",
                "RestoreGifTrimSelection:C:\\preview\\document.pdf",
                "RestorePdfSelection:C:\\preview\\document.pdf",
                "RefreshSelectedPdfPreview:True"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_SelectedImageChanged_UsesPsdPreviewLoaderWhenCoordinatorRequestsAsyncRender()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            ShouldLoadSelectedPsdPreviewAsyncResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\layered.psd",
            fileSize: 4_096,
            pixelWidth: 1600,
            pixelHeight: 900,
            thumbnail: null,
            gifFrameCount: 1);

        coordinator.HandleSelectedImageChanged(image, context);

        Assert.Equal(
            [
                "CancelPendingPdfPreviewRender",
                "CancelPendingSelectedPsdPreviewRender",
                "ClearSelectedPreview",
                "RefreshGifHandlingModeOptions",
                "RestoreGifSpecificFrameSelection:C:\\preview\\layered.psd",
                "RestoreGifTrimSelection:C:\\preview\\layered.psd",
                "RestorePdfSelection:C:\\preview\\layered.psd",
                "ShouldLoadSelectedPsdPreviewAsync:C:\\preview\\layered.psd",
                "RefreshSelectedPsdPreviewAsync:True:True"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_SelectedImageChanged_KeepsGifRenderFlowOutOfMainWindowViewModel()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            ShouldLoadGifPreviewFramesResult = false
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleSelectedImageChanged(image, context);

        Assert.Equal(
            [
                "CancelPendingPdfPreviewRender",
                "CancelPendingSelectedPsdPreviewRender",
                "ClearSelectedPreview",
                "RefreshGifHandlingModeOptions",
                "RestoreGifSpecificFrameSelection:C:\\preview\\animated.gif",
                "RestoreGifTrimSelection:C:\\preview\\animated.gif",
                "RestorePdfSelection:C:\\preview\\animated.gif",
                "PrepareSelectedAnimatedGifPreview:C:\\preview\\animated.gif",
                "ShouldLoadGifPreviewFrames",
                "IncrementGifPreviewRequestId"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_OutputFormatChanged_RefreshesGifBranchThroughCoordinator()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext();
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleOutputFormatChanged(image, context);

        Assert.Equal(
            [
                "RefreshGifHandlingModeOptions",
                "RestoreGifTrimSelection:C:\\preview\\animated.gif",
                "RefreshPdfUiState",
                "PrepareSelectedAnimatedGifPreview:C:\\preview\\animated.gif",
                "ShouldLoadGifPreviewFrames",
                "IncrementGifPreviewRequestId"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_GifHandlingModeChanged_RefreshesGifPreviewThroughCoordinator()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            ShouldLoadGifPreviewFramesResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleGifHandlingModeChanged(image, context, GifHandlingMode.SpecificFrame);

        Assert.Equal(
            [
                "RefreshGifPdfUiState",
                "RefreshGifSpecificFrameUiState",
                "RefreshGifTrimUiState",
                "WarmAllGifPreviewsIfNeeded",
                "PrepareSelectedAnimatedGifPreview:C:\\preview\\animated.gif",
                "ShouldLoadGifPreviewFrames",
                "LoadGifPreviewAsync:C:\\preview\\animated.gif"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_BackgroundSettingsChanged_RefreshesConfigurablePreviewOnly()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            ShouldRefreshSelectedConfigurablePreviewResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\transparent.png",
            fileSize: 2_048,
            pixelWidth: 800,
            pixelHeight: 600,
            thumbnail: null,
            gifFrameCount: 1);

        coordinator.HandleBackgroundSettingsChanged(image, context);

        Assert.Equal(
            [
                "ShouldRefreshSelectedConfigurablePreview:C:\\preview\\transparent.png",
                "RefreshSelectedConfigurablePreview"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_PdfPageSelectionChanged_RefreshesPdfPreviewThroughCoordinator()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext();
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\document.pdf",
            fileSize: 2_048,
            pixelWidth: 0,
            pixelHeight: 0,
            thumbnail: null,
            gifFrameCount: 1,
            pdfPageCount: 4,
            isPdfDocument: true);

        coordinator.HandlePdfPageSelectionChanged(image, context, preferImmediatePreview: false);

        Assert.Equal(
            [
                "RefreshSelectedPdfPreview:False"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_GifTrimSelectionChanged_LoadsGifPreviewWhenRangeCannotBeAppliedImmediately()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            TryApplySelectedGifTrimPreviewRangeResult = false
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleGifTrimSelectionChanged(image, context);

        Assert.Equal(
            [
                "TryApplySelectedGifTrimPreviewRange",
                "LoadGifPreviewAsync:C:\\preview\\animated.gif"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_GifSpecificFrameSelectionChanged_ReusesLoadedGifFramesWhenAvailable()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            TryApplySelectedGifSpecificFramePreviewResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleGifSpecificFrameSelectionChanged(image, context);

        Assert.Equal(
            [
                "TryApplySelectedGifSpecificFramePreview"
            ],
            context.Calls);
    }

    [Fact]
    public async Task PreviewRenderCoordinator_GifSpecificFramePlaybackToggle_StartsPlaybackFromLoadedFrames()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            CanToggleGifSpecificFramePlaybackResult = true,
            HasReadyGifSpecificFramePlaybackFramesResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        await coordinator.HandleGifSpecificFramePlaybackToggleAsync(image, context);

        Assert.Equal(
            [
                "IsGifSpecificFramePlaybackActive",
                "CanToggleGifSpecificFramePlayback",
                "SetGifSpecificFramePlaybackActive:True",
                "HasReadyGifSpecificFramePlaybackFrames",
                "StartGifSpecificFramePlayback"
            ],
            context.Calls);
    }

    [Fact]
    public void PreviewRenderCoordinator_GifSpecificFrameRestoreCompleted_ReusesLoadedPreviewWhenAvailable()
    {
        var coordinator = new PreviewRenderCoordinator();
        var context = new FakePreviewRenderContext
        {
            TryApplySelectedGifSpecificFramePreviewResult = true
        };
        var image = ImageItemViewModel.CreateImported(
            @"C:\preview\animated.gif",
            fileSize: 8_192,
            pixelWidth: 640,
            pixelHeight: 360,
            thumbnail: null,
            gifFrameCount: 24);

        coordinator.HandleGifSpecificFrameRestoreCompleted(image, context);

        Assert.Equal(
            [
                "TryApplySelectedGifSpecificFramePreview"
            ],
            context.Calls);
    }

    [Fact]
    public void MainWindowShellCoordinator_TryCreateImageRequestSource_PrefersSenderDataContext()
    {
        var senderImage = ImageItemViewModel.CreateImported(
            @"C:\preview\sender.png",
            fileSize: 1_024,
            pixelWidth: 400,
            pixelHeight: 300,
            thumbnail: null,
            gifFrameCount: 1);
        var fallbackImage = ImageItemViewModel.CreateImported(
            @"C:\preview\fallback.png",
            fileSize: 1_024,
            pixelWidth: 400,
            pixelHeight: 300,
            thumbnail: null,
            gifFrameCount: 1);
        var sender = new Button
        {
            DataContext = senderImage
        };

        var success = MainWindowShellCoordinator.TryCreateImageRequestSource(sender, fallbackImage, out var request);

        Assert.True(success);
        Assert.Same(senderImage, request.Image);
        Assert.Equal(ShellImageRequestOrigin.SenderDataContext, request.Origin);
    }

    [Fact]
    public void MainWindowShellCoordinator_TryCreateImageRequestSource_UsesSelectedImageFallback()
    {
        var fallbackImage = ImageItemViewModel.CreateImported(
            @"C:\preview\fallback.png",
            fileSize: 1_024,
            pixelWidth: 400,
            pixelHeight: 300,
            thumbnail: null,
            gifFrameCount: 1);

        var success = MainWindowShellCoordinator.TryCreateImageRequestSource(null, fallbackImage, out var request);

        Assert.True(success);
        Assert.Same(fallbackImage, request.Image);
        Assert.Equal(ShellImageRequestOrigin.SelectedImageFallback, request.Origin);
    }

    [Fact]
    public void MainWindowConfigurationCoordinator_BuildConversionOptions_SeparatesManualAndWatchOutputs()
    {
        var coordinator = new MainWindowConfigurationCoordinator();
        var snapshot = new MainWindowConfigurationSnapshot
        {
            LanguageCode = "en-US",
            OutputFormat = OutputImageFormat.Webp,
            CompressionMode = CompressionMode.HighCompression,
            Quality = 78,
            ResizeMode = ResizeMode.CustomSize,
            ResizeWidth = 1920,
            ResizeHeight = 1080,
            RenameMode = RenameMode.Prefix,
            RenamePrefix = "done_",
            RenameSuffix = "_final",
            OutputDirectory = @"C:\manual-output",
            UseSourceFolder = true,
            WatchOutputDirectory = @"D:\watch-output",
            GifHandlingMode = GifHandlingMode.SpecificFrame,
            GifSpecificFrameIndex = 3,
            GifSpecificFrameSelections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\sample.gif"] = 5
            },
            GifFrameRanges = new Dictionary<string, GifFrameRangeSelection>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\sample.gif"] = new GifFrameRangeSelection(1, 4)
            },
            PdfPageSelections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\sample.pdf"] = 2
            },
            PdfPageRanges = new Dictionary<string, PdfPageRangeSelection>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\sample.pdf"] = new PdfPageRangeSelection(0, 2)
            },
            PdfUnlockStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\sample.pdf"] = true
            }
        };

        var manual = coordinator.BuildConversionOptions(snapshot);
        var watch = coordinator.BuildConversionOptions(snapshot, forWatch: true);

        Assert.Equal(OutputDirectoryRule.SourceFolder, manual.OutputDirectoryRule);
        Assert.Equal(@"C:\manual-output", manual.OutputDirectory);
        Assert.Equal(OutputDirectoryRule.SpecificFolder, watch.OutputDirectoryRule);
        Assert.Equal(@"D:\watch-output", watch.OutputDirectory);
        Assert.Equal(5, manual.GifSpecificFrameSelections[@"C:\sample.gif"]);
        Assert.Equal(2, manual.PdfPageSelections[@"C:\sample.pdf"]);
        Assert.True(watch.PdfUnlockStates[@"C:\sample.pdf"]);
    }

    [Fact]
    public void MainWindowConfigurationCoordinator_BuildWatchProfile_ReusesFrozenJobDefinitionAndRefreshesOutputDirectory()
    {
        var coordinator = new MainWindowConfigurationCoordinator();
        var snapshot = new MainWindowConfigurationSnapshot
        {
            WatchModeEnabled = true,
            WatchInputDirectory = @"C:\watch-in",
            WatchOutputDirectory = @"D:\watch-out",
            WatchIncludeSubfolders = true,
            OutputFormat = OutputImageFormat.Webp
        };
        var frozenDefinition = new ConversionJobDefinition
        {
            Name = "Frozen Watch Job",
            Options = new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Jpeg,
                OutputDirectoryRule = OutputDirectoryRule.SourceFolder,
                OutputDirectory = @"C:\legacy-output",
                AllowOverwrite = true
            }
        };

        var profile = coordinator.BuildWatchProfile(snapshot, frozenDefinition);

        Assert.True(profile.IsEnabled);
        Assert.Equal(@"C:\watch-in", profile.InputDirectory);
        Assert.Equal(@"D:\watch-out", profile.OutputDirectory);
        Assert.Equal("Frozen Watch Job", profile.JobDefinition.Name);
        Assert.Equal(OutputImageFormat.Jpeg, profile.JobDefinition.Options.OutputFormat);
        Assert.Equal(OutputDirectoryRule.SpecificFolder, profile.JobDefinition.Options.OutputDirectoryRule);
        Assert.Equal(@"D:\watch-out", profile.JobDefinition.Options.OutputDirectory);
        Assert.True(profile.JobDefinition.Options.AllowOverwrite);
    }

    [Fact]
    public void MainWindowConfigurationCoordinator_BuildPreviewSessionState_KeepsPreviewToolStateSeparateFromTaskOptions()
    {
        var coordinator = new MainWindowConfigurationCoordinator();
        var snapshot = new MainWindowConfigurationSnapshot
        {
            LanguageCode = "ja-JP",
            OutputFormat = OutputImageFormat.Pdf,
            SvgUseBackground = true,
            SvgBackgroundColor = "#FF102030",
            AiMattingModel = AiMattingModel.GeneralClassic,
            AiMattingDevice = AiMattingDevice.Cpu,
            AiMattingOutputFormat = OutputImageFormat.Png,
            AiMattingBackgroundMode = AiMattingBackgroundMode.SolidColor,
            AiMattingBackgroundColor = "#FFABCDEF",
            AiMattingEdgeOptimizationEnabled = false,
            AiMattingEdgeOptimizationStrength = 12,
            AiMattingResolutionMode = AiMattingResolutionMode.Max1024
        };

        var session = coordinator.BuildPreviewSessionState(snapshot);

        Assert.Equal(OutputImageFormat.Pdf, session.JobDefinition.Options.OutputFormat);
        Assert.True(session.JobDefinition.Options.SvgUseBackground);
        Assert.Equal("#FF102030", session.JobDefinition.Options.SvgBackgroundColor);
        Assert.Equal(AiMattingBackgroundMode.SolidColor, session.PreviewToolState.AiMattingBackgroundMode);
        Assert.Equal("#FFABCDEF", session.PreviewToolState.AiMattingBackgroundColor);
        Assert.Equal(OutputImageFormat.Png, session.PreviewToolState.AiMattingOutputFormat);
        Assert.False(session.PreviewToolState.AiMattingEdgeOptimizationEnabled);
        Assert.Equal(AiMattingResolutionMode.Max1024, session.PreviewToolState.AiMattingResolutionMode);
    }

    [Fact]
    public void MainWindowConfigurationCoordinator_BuildApplicationPreferences_PreservesExistingPreferenceMetadataAndClonesPresets()
    {
        var coordinator = new MainWindowConfigurationCoordinator();
        var preset = new ConversionPreset
        {
            Name = "WebP",
            OutputFormat = OutputImageFormat.Webp,
            Quality = 80
        };
        var snapshot = new MainWindowConfigurationSnapshot
        {
            SelectedLanguageCode = "zh-CN",
            SelectedThemeCode = "Dark",
            OutputFormat = OutputImageFormat.Jpeg,
            CompressionMode = CompressionMode.HighCompression,
            Quality = 82,
            Presets = [preset],
            KeepRunningInTray = true,
            RunOnStartup = true
        };
        var existingSettings = new AppSettings
        {
            ApplicationPreferences = new ApplicationPreferences
            {
                DefaultListSortMode = ListSortMode.SizeDescending
            }
        };

        var preferences = coordinator.BuildApplicationPreferences(existingSettings, snapshot);

        Assert.Equal("zh-CN", preferences.LanguageCode);
        Assert.Equal("Dark", preferences.ThemeCode);
        Assert.Equal(OutputImageFormat.Jpeg, preferences.DefaultOutputFormat);
        Assert.Equal(ListSortMode.SizeDescending, preferences.DefaultListSortMode);
        Assert.Single(preferences.Presets);
        Assert.NotSame(preset, preferences.Presets[0]);
        Assert.Equal("WebP", preferences.Presets[0].Name);
        Assert.True(preferences.KeepRunningInTray);
        Assert.True(preferences.RunOnStartup);
    }

    [Fact]
    public void FreshStateDefaults_UseFullQualityAndUpscaylStandardModel()
    {
        var settings = new AppSettings();
        var preferences = AppSettingsStateMapper.ResolveApplicationPreferences(settings);
        var options = new ConversionOptions();
        var preset = new ConversionPreset();
        var snapshot = new MainWindowConfigurationSnapshot();
        var coordinator = new MainWindowConfigurationCoordinator();
        var snapshotOptions = coordinator.BuildConversionOptions(snapshot);

        Assert.Equal(100, settings.DefaultQuality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, settings.DefaultAiEnhancementModel);

        Assert.Equal(100, preferences.DefaultQuality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, preferences.DefaultAiEnhancementModel);

        Assert.Equal(100, options.Quality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, options.AiEnhancementModel);

        Assert.Equal(100, preset.Quality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, preset.AiEnhancementModel);

        Assert.Equal(100, snapshot.Quality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, snapshot.AiEnhancementModel);

        Assert.Equal(100, snapshotOptions.Quality);
        Assert.Equal(AiEnhancementModel.UpscaylStandard, snapshotOptions.AiEnhancementModel);
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
            "ConversionSummaryTitle" => "Summary",
            "Close" => "Close",
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
            "WarningsTitle" => "Warnings",
            "TaskAnalysisTitle" => "Task analysis",
            "FormatRecommendationTitle" => "Recommended format",
            "EstimatedSizeTitle" => "Estimated size",
            _ => key
        };
    }

    private sealed class FakePreviewRenderContext : IPreviewRenderContext
    {
        public List<string> Calls { get; } = [];

        public bool ShouldLoadSelectedPsdPreviewAsyncResult { get; set; }

        public bool ShouldLoadGifPreviewFramesResult { get; set; }

        public bool TryApplySelectedGifTrimPreviewRangeResult { get; set; }

        public bool TryApplySelectedGifSpecificFramePreviewResult { get; set; }

        public bool ShouldRefreshSelectedConfigurablePreviewResult { get; set; }

        public bool CanToggleGifSpecificFramePlaybackResult { get; set; }

        public bool HasReadyGifSpecificFramePlaybackFramesResult { get; set; }

        public void CancelPendingPdfPreviewRender()
        {
            Calls.Add("CancelPendingPdfPreviewRender");
        }

        public void CancelPendingSelectedPsdPreviewRender()
        {
            Calls.Add("CancelPendingSelectedPsdPreviewRender");
        }

        public void ClearSelectedPreview()
        {
            Calls.Add("ClearSelectedPreview");
        }

        public void RefreshGifHandlingModeOptions()
        {
            Calls.Add("RefreshGifHandlingModeOptions");
        }

        public void RestoreGifSpecificFrameSelection(ImageItemViewModel? image)
        {
            Calls.Add($"RestoreGifSpecificFrameSelection:{image?.FilePath}");
        }

        public void RestoreGifTrimSelection(ImageItemViewModel? image)
        {
            Calls.Add($"RestoreGifTrimSelection:{image?.FilePath}");
        }

        public void RestorePdfSelection(ImageItemViewModel? image)
        {
            Calls.Add($"RestorePdfSelection:{image?.FilePath}");
        }

        public void RefreshPdfUiState()
        {
            Calls.Add("RefreshPdfUiState");
        }

        public void RefreshGifPdfUiState()
        {
            Calls.Add("RefreshGifPdfUiState");
        }

        public void RefreshGifSpecificFrameUiState()
        {
            Calls.Add("RefreshGifSpecificFrameUiState");
        }

        public void RefreshGifTrimUiState()
        {
            Calls.Add("RefreshGifTrimUiState");
        }

        public void WarmAllGifPreviewsIfNeeded()
        {
            Calls.Add("WarmAllGifPreviewsIfNeeded");
        }

        public void RefreshSelectedPdfPreview(bool preferImmediatePreview)
        {
            Calls.Add($"RefreshSelectedPdfPreview:{preferImmediatePreview}");
        }

        public bool ShouldLoadSelectedPsdPreviewAsync(ImageItemViewModel image)
        {
            Calls.Add($"ShouldLoadSelectedPsdPreviewAsync:{image.FilePath}");
            return ShouldLoadSelectedPsdPreviewAsyncResult;
        }

        public void RefreshSelectedPsdPreviewAsync(bool preferImmediatePreview, bool useThumbnailPlaceholder)
        {
            Calls.Add($"RefreshSelectedPsdPreviewAsync:{preferImmediatePreview}:{useThumbnailPlaceholder}");
        }

        public Bitmap? CreatePreviewBitmap(string filePath, int maxWidth)
        {
            Calls.Add($"CreatePreviewBitmap:{filePath}:{maxWidth}");
            return null;
        }

        public void SetSelectedPreview(Bitmap? preview)
        {
            Calls.Add("SetSelectedPreview");
        }

        public bool ShouldLoadGifPreviewFrames()
        {
            Calls.Add("ShouldLoadGifPreviewFrames");
            return ShouldLoadGifPreviewFramesResult;
        }

        public Task LoadGifPreviewAsync(string filePath)
        {
            Calls.Add($"LoadGifPreviewAsync:{filePath}");
            return Task.CompletedTask;
        }

        public void IncrementGifPreviewRequestId()
        {
            Calls.Add("IncrementGifPreviewRequestId");
        }

        public void PrepareSelectedAnimatedGifPreview(string filePath)
        {
            Calls.Add($"PrepareSelectedAnimatedGifPreview:{filePath}");
        }

        public bool TryApplySelectedGifTrimPreviewRange()
        {
            Calls.Add("TryApplySelectedGifTrimPreviewRange");
            return TryApplySelectedGifTrimPreviewRangeResult;
        }

        public bool TryApplySelectedGifSpecificFramePreview()
        {
            Calls.Add("TryApplySelectedGifSpecificFramePreview");
            return TryApplySelectedGifSpecificFramePreviewResult;
        }

        public bool IsGifSpecificFramePlaybackActive()
        {
            Calls.Add("IsGifSpecificFramePlaybackActive");
            return false;
        }

        public bool CanToggleGifSpecificFramePlayback()
        {
            Calls.Add("CanToggleGifSpecificFramePlayback");
            return CanToggleGifSpecificFramePlaybackResult;
        }

        public bool HasReadyGifSpecificFramePlaybackFrames()
        {
            Calls.Add("HasReadyGifSpecificFramePlaybackFrames");
            return HasReadyGifSpecificFramePlaybackFramesResult;
        }

        public void SetGifSpecificFramePlaybackActive(bool isPlaying)
        {
            Calls.Add($"SetGifSpecificFramePlaybackActive:{isPlaying}");
        }

        public void StartGifSpecificFramePlayback()
        {
            Calls.Add("StartGifSpecificFramePlayback");
        }

        public void PauseGifSpecificFramePlayback()
        {
            Calls.Add("PauseGifSpecificFramePlayback");
        }

        public bool ShouldRefreshSelectedConfigurablePreview(ImageItemViewModel image)
        {
            Calls.Add($"ShouldRefreshSelectedConfigurablePreview:{image.FilePath}");
            return ShouldRefreshSelectedConfigurablePreviewResult;
        }

        public void RefreshSelectedConfigurablePreview()
        {
            Calls.Add("RefreshSelectedConfigurablePreview");
        }
    }
}
