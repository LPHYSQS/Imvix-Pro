
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private const string ThemeCodeDark = "Dark";
        private const string ThemeCodeLight = "Light";
        private const string ThemeCodeSystem = "System";
        private const string LanguageCodeSystem = "System";
        private const string FallbackLanguageCode = "en-US";
        private const string ContactAuthorEmail = "3261296352@qq.com";

        private static readonly (string Code, string DisplayName)[] SupportedLanguageSeeds =
        [
            ("zh-CN", "\u7B80\u4F53\u4E2D\u6587"),
            ("zh-TW", "\u7E41\u9AD4\u4E2D\u6587"),
            ("en-US", "English"),
            ("ja-JP", "\u65E5\u672C\u8A9E"),
            ("ko-KR", "\uD55C\uAD6D\uC5B4"),
            ("fr-FR", "Fran\u00E7ais"),
            ("de-DE", "Deutsch"),
            ("it-IT", "Italiano"),
            ("ru-RU", "\u0420\u0443\u0441\u0441\u043A\u0438\u0439"),
            ("ar-SA", "\u0627\u0644\u0639\u0631\u0628\u064A\u0629")
        ];

        private readonly SettingsService _settingsService;
        private readonly LocalizationService _localizationService;
        private readonly ImageConversionService _imageConversionService;
        private readonly ConversionPlanningService _conversionPlanningService;
        private readonly ConversionStatusSummaryService _conversionStatusSummaryService;
        private readonly ConversionTextPresenter _conversionTextPresenter;
        private readonly ConversionSummaryCoordinator _conversionSummaryCoordinator;
        private readonly PreviewRenderCoordinator _previewRenderCoordinator;
        private readonly WatchProfilePlanningService _watchProfilePlanningService;
        private readonly PdfSecurityService _pdfSecurityService;
        private readonly AppLogger _logger;
        private readonly IPreviewRenderContext _previewRenderContext;
        private ConversionJobDefinition? _watchJobDefinitionSnapshot;
        private RuntimeStatusSummary _manualRuntimeStatus = new("StatusReady", string.Empty, 0, 0);
        private WatchRuntimeStatusSummary _watchRuntimeStatus = new(WatchRuntimeState.Stopped, 0, 0, string.Empty);

        private string _statusKey = "StatusReady";
        private bool _isLoadingSettings;
        private bool _isSyncingSvgColorInputs;
        private bool _isSyncingIconColorInputs;
        private bool _isVersionBadgeHovered;
        private bool _isRefreshingLanguageOptions;
        private readonly DispatcherTimer _gifPreviewTimer = new();
        private ImageConversionService.GifPreviewHandle? _gifPreviewHandle;
        private IReadOnlyList<Bitmap>? _gifPreviewFrames;
        private IReadOnlyList<TimeSpan>? _gifPreviewDurations;
        private int _gifPreviewIndex;
        private long _gifPreviewRequestId;

        public PreviewSelectionState PreviewSelectionState { get; }

        public HistoryState HistoryState { get; }

        public NotificationState NotificationState { get; }

        public TaskAnalysisState TaskAnalysisState { get; }

        public MainWindowViewModel()
            : this(AppServices.CreateMainWindowViewModelServices())
        {
        }

        internal MainWindowViewModel(MainWindowViewModelServices services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _settingsService = services.SettingsService ?? throw new ArgumentNullException(nameof(services.SettingsService));
            _localizationService = services.LocalizationService ?? throw new ArgumentNullException(nameof(services.LocalizationService));
            _imageConversionService = services.ImageConversionService ?? throw new ArgumentNullException(nameof(services.ImageConversionService));
            _imageAnalysisService = services.ImageAnalysisService ?? throw new ArgumentNullException(nameof(services.ImageAnalysisService));
            _conversionPlanningService = services.ConversionPlanningService ?? throw new ArgumentNullException(nameof(services.ConversionPlanningService));
            _conversionStatusSummaryService = services.ConversionStatusSummaryService ?? throw new ArgumentNullException(nameof(services.ConversionStatusSummaryService));
            _conversionTextPresenter = services.ConversionTextPresenter ?? throw new ArgumentNullException(nameof(services.ConversionTextPresenter));
            _conversionSummaryCoordinator = services.ConversionSummaryCoordinator ?? throw new ArgumentNullException(nameof(services.ConversionSummaryCoordinator));
            _previewRenderCoordinator = services.PreviewRenderCoordinator ?? throw new ArgumentNullException(nameof(services.PreviewRenderCoordinator));
            _watchProfilePlanningService = services.WatchProfilePlanningService ?? throw new ArgumentNullException(nameof(services.WatchProfilePlanningService));
            _conversionPipelineService = services.ConversionPipelineService ?? throw new ArgumentNullException(nameof(services.ConversionPipelineService));
            _conversionHistoryService = services.ConversionHistoryService ?? throw new ArgumentNullException(nameof(services.ConversionHistoryService));
            _conversionLogService = services.ConversionLogService ?? throw new ArgumentNullException(nameof(services.ConversionLogService));
            _folderWatchService = services.FolderWatchService ?? throw new ArgumentNullException(nameof(services.FolderWatchService));
            _systemIntegrationService = services.SystemIntegrationService ?? throw new ArgumentNullException(nameof(services.SystemIntegrationService));
            _pdfSecurityService = services.PdfSecurityService ?? throw new ArgumentNullException(nameof(services.PdfSecurityService));
            _pdfImportService = services.PdfImportService ?? throw new ArgumentNullException(nameof(services.PdfImportService));
            _psdImportService = services.PsdImportService ?? throw new ArgumentNullException(nameof(services.PsdImportService));
            _pdfRenderService = services.PdfRenderService ?? throw new ArgumentNullException(nameof(services.PdfRenderService));
            _psdRenderService = services.PsdRenderService ?? throw new ArgumentNullException(nameof(services.PsdRenderService));
            _logger = services.Logger ?? throw new ArgumentNullException(nameof(services.Logger));
            _previewRenderContext = new MainWindowPreviewRenderContext(this);
            PreviewSelectionState = new PreviewSelectionState();
            HistoryState = new HistoryState(_conversionHistoryService, _conversionSummaryCoordinator);
            NotificationState = new NotificationState();
            TaskAnalysisState = new TaskAnalysisState();

            Images.CollectionChanged += OnImagesCollectionChanged;
            FailedConversions.CollectionChanged += OnFailedConversionsCollectionChanged;
            Presets.CollectionChanged += OnPresetsCollectionChanged;
            _gifPreviewTimer.Tick += OnGifPreviewTick;

            _isLoadingSettings = true;

            var settings = _settingsService.Load();
            var applicationPreferences = AppSettingsStateMapper.ResolveApplicationPreferences(settings);
            var previewToolState = AppSettingsStateMapper.ResolvePreviewToolState(settings);
            var watchProfile = AppSettingsStateMapper.ResolveWatchProfile(settings);

            var initialLanguageCode = string.IsNullOrWhiteSpace(applicationPreferences.LanguageCode)
                ? LanguageCodeSystem
                : applicationPreferences.LanguageCode;
            var effectiveLanguageCode = ResolveEffectiveLanguageCode(initialLanguageCode);
            _localizationService.SetLanguage(effectiveLanguageCode);
            UiFlowDirection = ResolveFlowDirection(effectiveLanguageCode);

            RefreshLanguageOptions(initialLanguageCode);
            RefreshThemeOptions(applicationPreferences.ThemeCode);
            if (SelectedTheme is not null)
            {
                ApplyTheme(SelectedTheme.Code);
            }

            SelectedOutputFormat = applicationPreferences.DefaultOutputFormat;
            SelectedCompressionMode = applicationPreferences.DefaultCompressionMode;
            Quality = Math.Clamp(applicationPreferences.DefaultQuality, 1, 100);

            SelectedResizeMode = applicationPreferences.DefaultResizeMode;
            ResizeWidth = Math.Max(1, applicationPreferences.DefaultResizeWidth);
            ResizeHeight = Math.Max(1, applicationPreferences.DefaultResizeHeight);
            ResizePercent = Math.Clamp(applicationPreferences.DefaultResizePercent, 1, 1000);

            SelectedRenameMode = applicationPreferences.DefaultRenameMode;
            RenamePrefix = applicationPreferences.DefaultRenamePrefix;
            RenameSuffix = applicationPreferences.DefaultRenameSuffix;
            RenameStartNumber = Math.Max(0, applicationPreferences.DefaultRenameStartNumber);
            RenameNumberDigits = Math.Clamp(applicationPreferences.DefaultRenameNumberDigits, 1, 8);

            OutputDirectory = applicationPreferences.DefaultOutputDirectory;
            UseSourceFolder = ResolveUseSourceFolder(applicationPreferences);
            IncludeSubfoldersOnFolderImport = applicationPreferences.IncludeSubfoldersOnFolderImport;

            AutoOpenOutputDirectory = applicationPreferences.AutoOpenOutputDirectory;
            AllowOverwrite = applicationPreferences.AllowOverwrite;
            SvgUseBackground = applicationPreferences.SvgUseBackground;
            SvgBackgroundColor = string.IsNullOrWhiteSpace(applicationPreferences.SvgBackgroundColor) ? "#FFFFFFFF" : applicationPreferences.SvgBackgroundColor;
            IconUseTransparency = applicationPreferences.IconUseTransparency;
            IconBackgroundColor = string.IsNullOrWhiteSpace(applicationPreferences.IconBackgroundColor) ? "#FFFFFFFF" : applicationPreferences.IconBackgroundColor;
            SelectedGifHandlingMode = applicationPreferences.DefaultGifHandlingMode;
            SelectedGifSpecificFrameIndex = Math.Max(0, applicationPreferences.DefaultGifSpecificFrameIndex);

            Presets.Clear();
            foreach (var preset in applicationPreferences.Presets.Where(static p => !string.IsNullOrWhiteSpace(p.Name)))
            {
                Presets.Add(ClonePreset(preset));
            }

            InitializeVersion3Features(applicationPreferences, watchProfile);
            InitializeAiFeatures(applicationPreferences, previewToolState);
            RefreshEnumOptions();

            RightPanelTabIndex = 0;
            ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateReadyRuntimeStatus());

            _isLoadingSettings = false;
            RefreshLocalizedProperties();
            EnsureStartupState();
            _ = CompleteVersion3InitializationAsync();
            RefreshPreviewSelectionState();

            OnPropertyChanged(nameof(IsSvgBackgroundColorVisible));
            OnPropertyChanged(nameof(IsIconTransparencyToggleVisible));
            OnPropertyChanged(nameof(IsIconBackgroundColorVisible));
            OnPropertyChanged(nameof(IsQualityEditable));
            OnPropertyChanged(nameof(IsResizeWidthVisible));
            OnPropertyChanged(nameof(IsResizeHeightVisible));
            OnPropertyChanged(nameof(IsResizePercentVisible));
            OnPropertyChanged(nameof(IsRenamePrefixVisible));
            OnPropertyChanged(nameof(IsRenameSuffixVisible));
            OnPropertyChanged(nameof(IsRenameNumberVisible));

            SavePresetCommand.NotifyCanExecuteChanged();
            ApplyPresetCommand.NotifyCanExecuteChanged();
            DeletePresetCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private ImageItemViewModel? selectedImage;

        [ObservableProperty]
        private Bitmap? selectedPreview;

        [ObservableProperty]
        private OutputImageFormat selectedOutputFormat;

        [ObservableProperty]
        private CompressionMode selectedCompressionMode = CompressionMode.Custom;

        [ObservableProperty]
        private ResizeMode selectedResizeMode = ResizeMode.None;

        [ObservableProperty]
        private int resizeWidth = 1280;

        [ObservableProperty]
        private int resizeHeight = 720;

        [ObservableProperty]
        private int resizePercent = 100;

        [ObservableProperty]
        private RenameMode selectedRenameMode = RenameMode.KeepOriginal;

        [ObservableProperty]
        private GifHandlingMode selectedGifHandlingMode = GifHandlingMode.FirstFrame;

        [ObservableProperty]
        private EnumOption<CompressionMode>? selectedCompressionModeOption;

        [ObservableProperty]
        private EnumOption<ResizeMode>? selectedResizeModeOption;

        [ObservableProperty]
        private EnumOption<RenameMode>? selectedRenameModeOption;

        [ObservableProperty]
        private EnumOption<GifHandlingMode>? selectedGifHandlingModeOption;

        [ObservableProperty]
        private string renamePrefix = string.Empty;

        [ObservableProperty]
        private string renameSuffix = string.Empty;

        [ObservableProperty]
        private int renameStartNumber = 1;

        [ObservableProperty]
        private int renameNumberDigits = 4;

        [ObservableProperty]
        private string outputDirectory = string.Empty;

        [ObservableProperty]
        private bool useSourceFolder;

        [ObservableProperty]
        private bool includeSubfoldersOnFolderImport = true;

        [ObservableProperty]
        private bool autoOpenOutputDirectory;

        [ObservableProperty]
        private bool allowOverwrite;

        [ObservableProperty]
        private int quality = 90;

        [ObservableProperty]
        private bool svgUseBackground;

        [ObservableProperty]
        private string svgBackgroundColor = "#FFFFFFFF";

        [ObservableProperty]
        private string svgBackgroundColorRgb = "255,255,255";

        [ObservableProperty]
        private Color svgBackgroundColorValue = Color.FromArgb(255, 255, 255, 255);

        [ObservableProperty]
        private bool iconUseTransparency = true;

        [ObservableProperty]
        private string iconBackgroundColor = "#FFFFFFFF";

        [ObservableProperty]
        private string iconBackgroundColorRgb = "255,255,255";

        [ObservableProperty]
        private Color iconBackgroundColorValue = Color.FromArgb(255, 255, 255, 255);

        [ObservableProperty]
        private LanguageOption? selectedLanguage;

        [ObservableProperty]
        private ThemeOption? selectedTheme;

        [ObservableProperty]
        private FlowDirection uiFlowDirection = FlowDirection.LeftToRight;

        partial void OnUiFlowDirectionChanged(FlowDirection value)
        {
            OnPropertyChanged(nameof(IsRightToLeftLayout));
            OnPropertyChanged(nameof(IsLeftToRightLayout));
        }

        [ObservableProperty]
        private double progressPercent;

        [ObservableProperty]
        private string currentFile = string.Empty;

        [ObservableProperty]
        private int remainingCount;

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private bool isConverting;

        [ObservableProperty]
        private int rightPanelTabIndex;

        [ObservableProperty]
        private string presetNameInput = string.Empty;

        [ObservableProperty]
        private ConversionPreset? selectedPreset;

                [RelayCommand(CanExecute = nameof(CanStartConversion))]
        private async Task StartConversionAsync()
        {
            await StartManualConversionCoreAsync();
        }

        partial void OnSelectedImageChanged(ImageItemViewModel? value)
        {
            _previewRenderCoordinator.HandleSelectedImageChanged(value, _previewRenderContext);

            OnPropertyChanged(nameof(IsGifPreviewVisible));
            OnPropertyChanged(nameof(IsGifTrimRangeVisible));
            OnPropertyChanged(nameof(IsSvgPreviewVisible));
            OnPropertyChanged(nameof(IsSvgBackgroundToggleVisible));
            OnPropertyChanged(nameof(IsSvgBackgroundToggleEnabled));
            OnPropertyChanged(nameof(IsSvgBackgroundRequiredHintVisible));
            OnPropertyChanged(nameof(SvgBackgroundToggleValue));
            OnPropertyChanged(nameof(IsSvgBackgroundColorVisible));
            OnPropertyChanged(nameof(IsIconPreviewVisible));
            OnPropertyChanged(nameof(IsIconTransparencyToggleVisible));
            OnPropertyChanged(nameof(IsIconBackgroundColorVisible));
            OnPropertyChanged(nameof(SvgSettingsText));
            OnPropertyChanged(nameof(SvgUseBackgroundText));
            RefreshPdfUiState();
            RefreshGifSpecificFrameUiState();
            RefreshGifTrimUiState();

            RefreshConversionInsights();
            RefreshPreviewSelectionState();
        }

        private sealed class MainWindowPreviewRenderContext : IPreviewRenderContext
        {
            private readonly MainWindowViewModel _owner;

            public MainWindowPreviewRenderContext(MainWindowViewModel owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void CancelPendingPdfPreviewRender()
            {
                _owner.CancelPendingPdfPreviewRender();
            }

            public void CancelPendingSelectedPsdPreviewRender()
            {
                _owner.CancelPendingSelectedPsdPreviewRender();
            }

            public void ClearSelectedPreview()
            {
                _owner.ClearSelectedPreview();
            }

            public void RefreshGifHandlingModeOptions()
            {
                _owner.RefreshGifHandlingModeOptions();
            }

            public void RestoreGifSpecificFrameSelection(ImageItemViewModel? image)
            {
                _owner.RestoreGifSpecificFrameSelection(image);
            }

            public void RestoreGifTrimSelection(ImageItemViewModel? image)
            {
                _owner.RestoreGifTrimSelection(image);
            }

            public void RestorePdfSelection(ImageItemViewModel? image)
            {
                _owner.RestorePdfSelection(image);
            }

            public void RefreshSelectedPdfPreview(bool preferImmediatePreview)
            {
                _owner.RefreshSelectedPdfPreview(preferImmediatePreview);
            }

            public bool ShouldLoadSelectedPsdPreviewAsync(ImageItemViewModel image)
            {
                return _owner.ShouldLoadSelectedPsdPreviewAsync(image);
            }

            public void RefreshSelectedPsdPreviewAsync(bool preferImmediatePreview, bool useThumbnailPlaceholder)
            {
                _owner.RefreshSelectedPsdPreviewAsync(preferImmediatePreview, useThumbnailPlaceholder);
            }

            public Bitmap? CreatePreviewBitmap(string filePath, int maxWidth)
            {
                return _owner.CreatePreviewBitmap(filePath, maxWidth);
            }

            public void SetSelectedPreview(Bitmap? preview)
            {
                _owner.SelectedPreview = preview;
            }

            public bool ShouldLoadGifPreviewFrames()
            {
                return _owner.ShouldLoadGifPreviewFrames();
            }

            public void LoadGifPreview(string filePath)
            {
                _ = _owner.LoadGifPreviewAsync(filePath);
            }

            public void IncrementGifPreviewRequestId()
            {
                Interlocked.Increment(ref _owner._gifPreviewRequestId);
            }
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (value is null)
            {
                return;
            }

            if (_isRefreshingLanguageOptions)
            {
                OnPropertyChanged(nameof(IsSystemLanguageSelected));
                return;
            }

            var effectiveLanguageCode = ResolveEffectiveLanguageCode(value.Code);
            _localizationService.SetLanguage(effectiveLanguageCode);
            UiFlowDirection = ResolveFlowDirection(effectiveLanguageCode);
            RefreshLanguageOptions(value.Code);
            RefreshThemeOptions(SelectedTheme?.Code);
            RefreshLocalizedProperties();
            RefreshEnumOptions();
            ApplyManualRuntimeStatus(_manualRuntimeStatus);
            OnPropertyChanged(nameof(CurrentLanguageCode));
            OnPropertyChanged(nameof(IsSystemLanguageSelected));
            PersistSettings();
        }

        partial void OnSelectedThemeChanged(ThemeOption? value)
        {
            if (value is null)
            {
                return;
            }

            ApplyTheme(value.Code);
            PersistSettings();
        }

        partial void OnSelectedOutputFormatChanged(OutputImageFormat value)
        {
            PersistSettings();
            RefreshGifHandlingModeOptions();
            OnPropertyChanged(nameof(IsGifPreviewVisible));
            OnPropertyChanged(nameof(IsGifTrimRangeVisible));
            OnPropertyChanged(nameof(IsSvgBackgroundToggleVisible));
            OnPropertyChanged(nameof(IsSvgBackgroundToggleEnabled));
            OnPropertyChanged(nameof(IsSvgBackgroundRequiredHintVisible));
            OnPropertyChanged(nameof(SvgBackgroundToggleValue));
            OnPropertyChanged(nameof(IsSvgBackgroundColorVisible));
            OnPropertyChanged(nameof(IsIconTransparencyToggleVisible));
            OnPropertyChanged(nameof(IsIconBackgroundColorVisible));
            OnPropertyChanged(nameof(SvgUseBackgroundText));
            RestoreGifTrimSelection(SelectedImage);
            RefreshGifTrimUiState();
            RefreshPdfUiState();
            RefreshSelectedAnimatedGifPreview();
            RefreshSelectedPreviewIfConfigurableSource();

            if (SelectedImage?.IsPdfDocument == true)
            {
                RefreshSelectedPdfPreview(preferImmediatePreview: true);
            }

            RefreshPreviewSelectionState();
        }

        partial void OnSelectedCompressionModeChanged(CompressionMode value)
        {
            var option = CompressionModes.FirstOrDefault(x => EqualityComparer<CompressionMode>.Default.Equals(x.Value, value));
            if (SelectedCompressionModeOption != option)
            {
                SelectedCompressionModeOption = option;
            }

            OnPropertyChanged(nameof(IsQualityEditable));
            PersistSettings();
        }

        partial void OnSelectedCompressionModeOptionChanged(EnumOption<CompressionMode>? value)
        {
            if (value is null)
            {
                return;
            }

            if (!EqualityComparer<CompressionMode>.Default.Equals(SelectedCompressionMode, value.Value))
            {
                SelectedCompressionMode = value.Value;
            }
        }

        partial void OnSelectedResizeModeChanged(ResizeMode value)
        {
            var option = ResizeModes.FirstOrDefault(x => EqualityComparer<ResizeMode>.Default.Equals(x.Value, value));
            if (SelectedResizeModeOption != option)
            {
                SelectedResizeModeOption = option;
            }

            OnPropertyChanged(nameof(IsResizeWidthVisible));
            OnPropertyChanged(nameof(IsResizeHeightVisible));
            OnPropertyChanged(nameof(IsResizePercentVisible));
            PersistSettings();
        }

        partial void OnSelectedResizeModeOptionChanged(EnumOption<ResizeMode>? value)
        {
            if (value is null)
            {
                return;
            }

            if (!EqualityComparer<ResizeMode>.Default.Equals(SelectedResizeMode, value.Value))
            {
                SelectedResizeMode = value.Value;
            }
        }

        partial void OnResizeWidthChanged(int value)
        {
            if (value < 1)
            {
                ResizeWidth = 1;
                return;
            }

            PersistSettings();
        }

        partial void OnResizeHeightChanged(int value)
        {
            if (value < 1)
            {
                ResizeHeight = 1;
                return;
            }

            PersistSettings();
        }

        partial void OnResizePercentChanged(int value)
        {
            if (value < 1)
            {
                ResizePercent = 1;
                return;
            }

            if (value > 1000)
            {
                ResizePercent = 1000;
                return;
            }

            PersistSettings();
        }

        partial void OnSelectedRenameModeChanged(RenameMode value)
        {
            var option = RenameModes.FirstOrDefault(x => EqualityComparer<RenameMode>.Default.Equals(x.Value, value));
            if (SelectedRenameModeOption != option)
            {
                SelectedRenameModeOption = option;
            }

            OnPropertyChanged(nameof(IsRenamePrefixVisible));
            OnPropertyChanged(nameof(IsRenameSuffixVisible));
            OnPropertyChanged(nameof(IsRenameNumberVisible));
            PersistSettings();
        }

        partial void OnSelectedRenameModeOptionChanged(EnumOption<RenameMode>? value)
        {
            if (value is null)
            {
                return;
            }

            if (!EqualityComparer<RenameMode>.Default.Equals(SelectedRenameMode, value.Value))
            {
                SelectedRenameMode = value.Value;
            }
        }

        partial void OnSelectedGifHandlingModeChanged(GifHandlingMode value)
        {
            var option = GifHandlingModes.FirstOrDefault(x => EqualityComparer<GifHandlingMode>.Default.Equals(x.Value, value));
            if (SelectedGifHandlingModeOption != option)
            {
                SelectedGifHandlingModeOption = option;
            }

            PersistSettings();

            if (SelectedImage is null || !SelectedImage.IsAnimatedGif)
            {
                RefreshGifPdfUiState();
                RefreshGifSpecificFrameUiState();
                return;
            }

            RefreshGifPdfUiState();
            RefreshGifSpecificFrameUiState();

            if (value is GifHandlingMode.AllFrames or GifHandlingMode.SpecificFrame)
            {
                WarmAllGifPreviewsIfNeeded();
            }

            RefreshSelectedAnimatedGifPreview();
        }

        partial void OnSelectedGifHandlingModeOptionChanged(EnumOption<GifHandlingMode>? value)
        {
            if (value is null)
            {
                return;
            }

            if (!EqualityComparer<GifHandlingMode>.Default.Equals(SelectedGifHandlingMode, value.Value))
            {
                SelectedGifHandlingMode = value.Value;
            }
        }

        partial void OnRenamePrefixChanged(string value)
        {
            PersistSettings();
        }

        partial void OnRenameSuffixChanged(string value)
        {
            PersistSettings();
        }

        partial void OnRenameStartNumberChanged(int value)
        {
            if (value < 0)
            {
                RenameStartNumber = 0;
                return;
            }

            PersistSettings();
        }

        partial void OnRenameNumberDigitsChanged(int value)
        {
            if (value < 1)
            {
                RenameNumberDigits = 1;
                return;
            }

            if (value > 8)
            {
                RenameNumberDigits = 8;
                return;
            }

            PersistSettings();
        }

        partial void OnOutputDirectoryChanged(string value)
        {
            PersistSettings();
        }

        partial void OnUseSourceFolderChanged(bool value)
        {
            PersistSettings();
        }

        partial void OnIncludeSubfoldersOnFolderImportChanged(bool value)
        {
            PersistSettings();
        }

        partial void OnAutoOpenOutputDirectoryChanged(bool value)
        {
            PersistSettings();
        }

        partial void OnAllowOverwriteChanged(bool value)
        {
            PersistSettings();
        }

        partial void OnQualityChanged(int value)
        {
            if (value < 1)
            {
                Quality = 1;
                return;
            }

            if (value > 100)
            {
                Quality = 100;
                return;
            }

            PersistSettings();
        }

        partial void OnSvgUseBackgroundChanged(bool value)
        {
            OnPropertyChanged(nameof(SvgBackgroundToggleValue));
            OnPropertyChanged(nameof(IsSvgBackgroundColorVisible));
            RefreshSelectedPreviewIfConfigurableSource();
            RefreshPreviewSelectionState();
            PersistSettings();
        }

        partial void OnSvgBackgroundColorChanged(string value)
        {
            if (_isSyncingSvgColorInputs)
            {
                return;
            }

            if (!TryParseSvgColor(value, out var parsed))
            {
                UpdateSvgColorInputs(SvgBackgroundColorValue);
                return;
            }

            UpdateSvgColorInputs(parsed);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnSvgBackgroundColorRgbChanged(string value)
        {
            if (_isSyncingSvgColorInputs)
            {
                return;
            }

            if (!TryParseRgbColor(value, out var parsed))
            {
                UpdateSvgColorInputs(SvgBackgroundColorValue);
                return;
            }

            UpdateSvgColorInputs(parsed);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnSvgBackgroundColorValueChanged(Color value)
        {
            if (_isSyncingSvgColorInputs)
            {
                return;
            }

            UpdateSvgColorInputs(value);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnIconUseTransparencyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsIconBackgroundColorVisible));
            RefreshSelectedPreviewIfConfigurableSource();
            RefreshPreviewSelectionState();
            PersistSettings();
        }

        partial void OnIconBackgroundColorChanged(string value)
        {
            if (_isSyncingIconColorInputs)
            {
                return;
            }

            if (!TryParseSvgColor(value, out var parsed))
            {
                UpdateIconColorInputs(IconBackgroundColorValue);
                return;
            }

            UpdateIconColorInputs(parsed);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnIconBackgroundColorRgbChanged(string value)
        {
            if (_isSyncingIconColorInputs)
            {
                return;
            }

            if (!TryParseRgbColor(value, out var parsed))
            {
                UpdateIconColorInputs(IconBackgroundColorValue);
                return;
            }

            UpdateIconColorInputs(parsed);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnIconBackgroundColorValueChanged(Color value)
        {
            if (_isSyncingIconColorInputs)
            {
                return;
            }

            UpdateIconColorInputs(value);
            RefreshSelectedPreviewIfConfigurableSource();
            PersistSettings();
        }

        partial void OnProgressPercentChanged(double value)
        {
            OnPropertyChanged(nameof(ProgressPercentText));
        }

        partial void OnIsConvertingChanged(bool value)
        {
            StartConversionCommand.NotifyCanExecuteChanged();
            ClearImagesCommand.NotifyCanExecuteChanged();
            PauseConversionCommand.NotifyCanExecuteChanged();
            ResumeConversionCommand.NotifyCanExecuteChanged();
            CancelConversionCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ShowAiConversionFeedback));

            if (!value)
            {
                RefreshConversionInsights();
            }
        }

        partial void OnPresetNameInputChanged(string value)
        {
            SavePresetCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPresetChanged(ConversionPreset? value)
        {
            ApplyPresetCommand.NotifyCanExecuteChanged();
            DeletePresetCommand.NotifyCanExecuteChanged();

            if (value is not null)
            {
                PresetNameInput = value.Name;
            }
        }

    }
}

















