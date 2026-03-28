using Bitmap = Avalonia.Media.Imaging.Bitmap;
using ImvixPro.Models;
using ImvixPro.Services.PdfModule;
using ImvixPro.Services.PsdModule;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using DrawingImage = System.Drawing.Image;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace ImvixPro.Services
{
    public sealed class FileDetailService
    {
        private const int DetailPreviewWidth = 420;
        private const int PdfSampleBytes = 4 * 1024 * 1024;
        private const int ExifTagCameraModel = 0x0110;
        private const int ExifTagDateTaken = 0x9003;
        private const int ExifTagIso = 0x8827;
        private const int ExifTagAperture = 0x829D;
        private const int ExifTagShutterSpeed = 0x829A;
        private const int ExifTagIccProfile = 0x8773;
        private static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".bmp",
            ".gif",
            ".tif",
            ".tiff",
            ".ico",
            ".svg",
            ".psd"
        };

        private readonly LocalizationService _localizationService;
        private readonly PdfRenderService _pdfRenderService;
        private readonly PdfSecurityService _pdfSecurityService;
        private readonly PsdRenderService _psdRenderService;
        private readonly PsdDetailService _psdDetailService;
        private readonly ImageAnalysisService _imageAnalysisService;
        private readonly AppLogger _logger;
        private readonly IReadOnlyList<IFileDetailProvider> _providers;

        public FileDetailService(string languageCode)
            : this(
                languageCode,
                AppServices.CreateLocalizationService(languageCode),
                AppServices.PdfRenderService,
                AppServices.PdfSecurityService,
                AppServices.PsdRenderService,
                AppServices.PsdDetailService,
                AppServices.ImageAnalysisService,
                AppServices.Logger)
        {
        }

        internal FileDetailService(
            string languageCode,
            LocalizationService localizationService,
            PdfRenderService pdfRenderService,
            PdfSecurityService pdfSecurityService,
            PsdRenderService psdRenderService,
            PsdDetailService psdDetailService,
            ImageAnalysisService imageAnalysisService,
            AppLogger logger)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _pdfRenderService = pdfRenderService ?? throw new ArgumentNullException(nameof(pdfRenderService));
            _pdfSecurityService = pdfSecurityService ?? throw new ArgumentNullException(nameof(pdfSecurityService));
            _psdRenderService = psdRenderService ?? throw new ArgumentNullException(nameof(psdRenderService));
            _psdDetailService = psdDetailService ?? throw new ArgumentNullException(nameof(psdDetailService));
            _imageAnalysisService = imageAnalysisService ?? throw new ArgumentNullException(nameof(imageAnalysisService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localizationService.SetLanguage(string.IsNullOrWhiteSpace(languageCode) ? "en-US" : languageCode);
            _providers =
            [
                new ShortcutFileDetailProvider(),
                new ExecutableFileDetailProvider(),
                new PdfFileDetailProvider(),
                new PsdFileDetailProvider(),
                new ImageFileDetailProvider()
            ];
        }

        public string Translate(string key)
        {
            return _localizationService.Translate(key);
        }

        public FileDetailDocument Load(ImageItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var fileInfo = new FileInfo(item.FilePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("The selected file no longer exists.", item.FilePath);
            }

            var context = new FileDetailContext(item, fileInfo, this);
            var provider = _providers.FirstOrDefault(candidate => candidate.CanHandle(context))
                           ?? new ImageFileDetailProvider();

            var sections = new List<FileDetailSection>
            {
                BuildGeneralSection(context)
            };
            sections.AddRange(provider.BuildSections(context));

            return new FileDetailDocument(
                item.FileName,
                item.FilePath,
                BuildFileTypeText(context, provider.GetTypeLabelKey(context)),
                provider.GetPreviewDescription(context),
                CreatePreview(context),
                sections);
        }

        public string BuildCopyText(FileDetailDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var builder = new StringBuilder();
            builder.AppendLine(document.FileName);
            builder.AppendLine(document.FileTypeText);
            builder.AppendLine(document.FilePath);

            foreach (var section in document.Sections)
            {
                builder.AppendLine();
                builder.AppendLine($"[{section.Title}]");
                foreach (var entry in section.Entries)
                {
                    builder.AppendLine($"{entry.Label}: {entry.Value}");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private FileDetailSection BuildGeneralSection(FileDetailContext context)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldName", context.Item.FileName),
                context.Entry("FileDetailFieldPath", context.Item.FilePath),
                context.Entry("FileDetailFieldFileType", $".{context.Item.Extension}"),
                context.Entry("FileDetailFieldSize", FormatFileSize(context.FileInfo.Length)),
                context.Entry("FileDetailFieldCreated", FormatTimestamp(context.FileInfo.CreationTime)),
                context.Entry("FileDetailFieldModified", FormatTimestamp(context.FileInfo.LastWriteTime)),
                context.Entry("FileDetailFieldAccessed", FormatTimestamp(context.FileInfo.LastAccessTime))
            };

            return new FileDetailSection(context.T("FileDetailSectionGeneral"), entries);
        }

        private IReadOnlyList<FileDetailSection> BuildPsdSections(FileDetailContext context)
        {
            var detail = _psdDetailService.Read(
                context.Item.FilePath,
                context.Item.PixelWidth,
                context.Item.PixelHeight,
                SafeHasTransparency(context.Item));

            return
            [
                BuildPsdCanvasSection(context, detail),
                BuildPsdLayerSection(context, detail),
                BuildPsdChannelSection(context, detail),
                BuildPsdColorSection(context, detail),
                BuildPsdAdvancedSection(context, detail)
            ];
        }

        private FileDetailSection BuildPsdCanvasSection(FileDetailContext context, PsdDetailInfo detail)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldDimensions", FormatPixelSize(detail.Width, detail.Height)),
                context.Entry("FileDetailFieldDpi", FormatDpi(detail.DpiX is not null ? (float)detail.DpiX.Value : null, detail.DpiY is not null ? (float)detail.DpiY.Value : null)),
                context.Entry("FileDetailFieldColorMode", detail.ColorMode),
                context.Entry("FileDetailFieldBitDepth", FormatBitDepth(detail.BitDepth))
            };

            return new FileDetailSection(context.T("FileDetailSectionPsdCanvas"), entries);
        }

        private FileDetailSection BuildPsdLayerSection(FileDetailContext context, PsdDetailInfo detail)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldLayerCount", FormatCount(detail.LayerCount)),
                context.Entry("FileDetailFieldVisibleLayers", FormatCount(detail.VisibleLayerCount)),
                context.Entry("FileDetailFieldHiddenLayers", FormatCount(detail.HiddenLayerCount)),
                context.Entry("FileDetailFieldLayerGroups", FormatPresenceWithCount(context, detail.LayerGroupCount)),
                context.Entry("FileDetailFieldMaxLayerSize", FormatPixelSize(detail.MaxLayerWidth, detail.MaxLayerHeight)),
                context.Entry("FileDetailFieldTransparencyChannel", FormatNullableBoolean(context, detail.HasTransparencyChannel)),
                context.Entry("FileDetailFieldLayerStructure", BuildLayerStructureSummary(context, detail)),
                context.Entry("FileDetailFieldLayerNames", BuildLayerNameSummary(detail.LayerNameSamples))
            };

            return new FileDetailSection(context.T("FileDetailSectionPsdLayers"), entries);
        }

        private FileDetailSection BuildPsdChannelSection(FileDetailContext context, PsdDetailInfo detail)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldChannelCount", FormatCount(detail.ChannelCount)),
                context.Entry("FileDetailFieldAlphaChannel", FormatNullableBoolean(context, detail.HasAlphaChannel)),
                context.Entry("FileDetailFieldTransparencySupport", FormatNullableBoolean(context, detail.SupportsTransparency))
            };

            return new FileDetailSection(context.T("FileDetailSectionPsdChannels"), entries);
        }

        private FileDetailSection BuildPsdColorSection(FileDetailContext context, PsdDetailInfo detail)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldIccProfile", detail.IccProfileName),
                context.Entry("FileDetailFieldColorSpace", detail.ColorSpace),
                context.Entry("FileDetailFieldEmbeddedColorProfile", FormatNullableBoolean(context, detail.HasEmbeddedColorProfile))
            };

            return new FileDetailSection(context.T("FileDetailSectionPsdColor"), entries);
        }

        private FileDetailSection BuildPsdAdvancedSection(FileDetailContext context, PsdDetailInfo detail)
        {
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldMergedImage", FormatNullableBoolean(context, detail.HasMergedImage)),
                context.Entry("FileDetailFieldCompatibilityVersion", detail.CompatibilityVersion),
                context.Entry("FileDetailFieldCompressed", FormatNullableBoolean(context, detail.IsCompressed)),
                context.Entry("FileDetailFieldCompression", detail.Compression),
                context.Entry("FileDetailFieldTextLayers", FormatNullableBoolean(context, detail.HasTextLayers)),
                context.Entry("FileDetailFieldSmartObjects", FormatNullableBoolean(context, detail.HasSmartObjects)),
                context.Entry("FileDetailFieldVectorPaths", FormatNullableBoolean(context, detail.HasVectorPaths))
            };

            return new FileDetailSection(context.T("FileDetailSectionPsdAdvanced"), entries);
        }

        private FileDetailSection BuildImageSection(FileDetailContext context)
        {
            var metadata = ReadImageMetadata(context);
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldResolution", metadata.ResolutionText),
                context.Entry("FileDetailFieldPixelCount", metadata.PixelCountText),
                context.Entry("FileDetailFieldColorMode", metadata.ColorMode),
                context.Entry("FileDetailFieldBitDepth", metadata.BitDepthText),
                context.Entry("FileDetailFieldTransparency", FormatBoolean(context, metadata.HasTransparency)),
                context.Entry("FileDetailFieldDpi", metadata.DpiText),
                context.Entry("FileDetailFieldCompression", metadata.Compression),
                context.Entry("FileDetailFieldExifCamera", metadata.CameraModel),
                context.Entry("FileDetailFieldExifDateTaken", metadata.DateTaken),
                context.Entry("FileDetailFieldExifIso", metadata.Iso),
                context.Entry("FileDetailFieldExifAperture", metadata.Aperture),
                context.Entry("FileDetailFieldExifShutter", metadata.ShutterSpeed),
                context.Entry("FileDetailFieldIccProfile", FormatNullableBoolean(context, metadata.HasIccProfile)),
                context.Entry("FileDetailFieldAnimationFrames", metadata.AnimationFrameCountText),
                context.Entry("FileDetailFieldFrameRate", metadata.FrameRateText)
            };

            return new FileDetailSection(context.T("FileDetailSectionImage"), entries);
        }

        private FileDetailSection BuildPdfSection(FileDetailContext context)
        {
            var metadata = ReadPdfMetadata(context);
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldPageCount", metadata.PageCountText),
                context.Entry("FileDetailFieldPdfVersion", metadata.Version),
                context.Entry("FileDetailFieldPageSize", metadata.PageSize),
                context.Entry("FileDetailFieldEncrypted", FormatNullableBoolean(context, metadata.IsEncrypted)),
                context.Entry("FileDetailFieldCopyAllowed", FormatNullableBoolean(context, metadata.AllowCopy)),
                context.Entry("FileDetailFieldPrintAllowed", FormatNullableBoolean(context, metadata.AllowPrint)),
                context.Entry("FileDetailFieldAuthor", metadata.Author),
                context.Entry("FileDetailFieldProducer", metadata.Producer),
                context.Entry("FileDetailFieldTitle", metadata.Title)
            };

            return new FileDetailSection(context.T("FileDetailSectionPdf"), entries);
        }

        private FileDetailSection BuildExecutableSection(FileDetailContext context)
        {
            var metadata = ReadExecutableMetadata(context.Item.FilePath, context);
            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldFileVersion", metadata.FileVersion),
                context.Entry("FileDetailFieldProductName", metadata.ProductName),
                context.Entry("FileDetailFieldCompanyName", metadata.CompanyName),
                context.Entry("FileDetailFieldFileDescription", metadata.FileDescription),
                context.Entry("FileDetailFieldOriginalFileName", metadata.OriginalFileName),
                context.Entry("FileDetailFieldArchitecture", metadata.Architecture),
                context.Entry("FileDetailFieldDotNetAssembly", FormatNullableBoolean(context, metadata.IsDotNetAssembly)),
                context.Entry("FileDetailFieldSigned", FormatNullableBoolean(context, metadata.IsSigned)),
                context.Entry("FileDetailFieldSigner", metadata.Signer)
            };

            return new FileDetailSection(context.T("FileDetailSectionExecutable"), entries);
        }

        private FileDetailSection BuildShortcutSection(FileDetailContext context)
        {
            var metadata = ShortcutIconService.TryGetShortcutMetadata(context.Item.FilePath, out var shortcutMetadata)
                ? shortcutMetadata
                : default;

            var iconSource = string.IsNullOrWhiteSpace(metadata.IconPath)
                ? null
                : metadata.IconIndex == 0
                    ? metadata.IconPath
                    : string.Create(CultureInfo.InvariantCulture, $"{metadata.IconPath}, {metadata.IconIndex}");

            var entries = new List<FileDetailEntry>
            {
                context.Entry("FileDetailFieldTargetPath", metadata.TargetPath),
                context.Entry("FileDetailFieldArguments", metadata.Arguments),
                context.Entry("FileDetailFieldWorkingDirectory", metadata.WorkingDirectory),
                context.Entry("FileDetailFieldIconSource", iconSource),
                context.Entry("FileDetailFieldTargetExists", string.IsNullOrWhiteSpace(metadata.TargetPath) ? null : FormatBoolean(context, metadata.TargetExists))
            };

            return new FileDetailSection(context.T("FileDetailSectionShortcut"), entries);
        }

        private ImageMetadataSnapshot ReadImageMetadata(FileDetailContext context)
        {
            var extension = Path.GetExtension(context.Item.FilePath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return new ImageMetadataSnapshot(
                    context.Item.PixelWidth > 0 && context.Item.PixelHeight > 0
                        ? string.Create(CultureInfo.InvariantCulture, $"{context.Item.PixelWidth} x {context.Item.PixelHeight}")
                        : null,
                    context.Item.PixelCount > 0 ? context.Item.PixelCount.ToString("N0", CultureInfo.CurrentCulture) : null,
                    "Vector",
                    null,
                    true,
                    null,
                    null,
                    "SVG (vector)",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            if (PsdImportService.IsPsdFile(context.Item.FilePath))
            {
                return ReadPsdMetadata(context);
            }

            var hasTransparency = SafeHasTransparency(context.Item);
            var codecSnapshot = ReadCodecSnapshot(context.Item.FilePath, context.Item.PixelWidth, context.Item.PixelHeight, context.Item.GifFrameCount, hasTransparency);
            var drawingSnapshot = ReadDrawingSnapshot(context.Item.FilePath, hasTransparency);

            var colorMode = !string.IsNullOrWhiteSpace(drawingSnapshot.ColorMode)
                ? drawingSnapshot.ColorMode
                : codecSnapshot.ColorMode;
            var bitDepth = drawingSnapshot.BitDepth ?? codecSnapshot.BitDepth;
            var animationFrameCountText = codecSnapshot.FrameCount > 1 || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                ? codecSnapshot.FrameCount.ToString("N0", CultureInfo.CurrentCulture)
                : null;
            var frameRateText = codecSnapshot.FrameRate is > 0d
                ? string.Create(CultureInfo.CurrentCulture, $"{codecSnapshot.FrameRate:0.##} fps")
                : null;

            return new ImageMetadataSnapshot(
                codecSnapshot.Width > 0 && codecSnapshot.Height > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{codecSnapshot.Width} x {codecSnapshot.Height}")
                    : null,
                codecSnapshot.Width > 0 && codecSnapshot.Height > 0
                    ? ((long)codecSnapshot.Width * codecSnapshot.Height).ToString("N0", CultureInfo.CurrentCulture)
                    : null,
                colorMode,
                bitDepth is > 0 ? string.Create(CultureInfo.InvariantCulture, $"{bitDepth} bit") : null,
                hasTransparency,
                FormatDpi(drawingSnapshot.DpiX, drawingSnapshot.DpiY),
                drawingSnapshot.HasIccProfile,
                ResolveCompressionLabel(extension, codecSnapshot.FrameCount),
                drawingSnapshot.CameraModel,
                drawingSnapshot.DateTaken,
                drawingSnapshot.Iso,
                drawingSnapshot.Aperture,
                drawingSnapshot.ShutterSpeed,
                animationFrameCountText,
                frameRateText);
        }

        private ImageMetadataSnapshot ReadPsdMetadata(FileDetailContext context)
        {
            var hasFallbackTransparency = SafeHasTransparency(context.Item);
            if (!_psdRenderService.TryReadDocumentInfo(context.Item.FilePath, out var info, out _) || info is null)
            {
                return new ImageMetadataSnapshot(
                    context.Item.PixelWidth > 0 && context.Item.PixelHeight > 0
                        ? string.Create(CultureInfo.InvariantCulture, $"{context.Item.PixelWidth} x {context.Item.PixelHeight}")
                        : null,
                    context.Item.PixelCount > 0 ? context.Item.PixelCount.ToString("N0", CultureInfo.CurrentCulture) : null,
                    null,
                    null,
                    hasFallbackTransparency,
                    null,
                    null,
                    "PSD",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            return new ImageMetadataSnapshot(
                info.Width > 0 && info.Height > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{info.Width} x {info.Height}")
                    : null,
                info.Width > 0 && info.Height > 0
                    ? ((long)info.Width * info.Height).ToString("N0", CultureInfo.CurrentCulture)
                    : null,
                info.ColorMode,
                info.BitDepth is > 0 ? string.Create(CultureInfo.InvariantCulture, $"{info.BitDepth} bit") : null,
                info.HasTransparency,
                null,
                null,
                "PSD",
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        private PdfMetadataSnapshot ReadPdfMetadata(FileDetailContext context)
        {
            var pageCount = context.Item.PdfPageCount;
            var width = context.Item.PixelWidth;
            var height = context.Item.PixelHeight;

            if ((pageCount <= 0 || width <= 0 || height <= 0) &&
                _pdfRenderService.TryReadDocumentInfo(context.Item.FilePath, out var info, out _))
            {
                pageCount = info.PageCount;
                width = info.FirstPageWidth;
                height = info.FirstPageHeight;
            }

            var originalSample = ReadPdfSampleText(context.Item.FilePath);
            var metadataSample = ReadAccessiblePdfSampleText(context.Item);
            var isEncrypted = context.Item.IsEncrypted;
            var permissions = isEncrypted ? TryReadPdfPermissionFlags(originalSample) : null;
            var author = ReadPdfMetadataValue(metadataSample, originalSample, "Author", context, preferXmpValue: false);
            var producer = ReadPdfMetadataValue(metadataSample, originalSample, "Producer", context, preferXmpValue: true);
            var title = ReadPdfMetadataValue(metadataSample, originalSample, "Title", context, preferXmpValue: true);

            return new PdfMetadataSnapshot(
                pageCount > 0 ? pageCount.ToString("N0", CultureInfo.CurrentCulture) : null,
                TryReadPdfVersion(originalSample ?? metadataSample),
                width > 0 && height > 0 ? FormatPdfPageSize(width, height) : null,
                isEncrypted,
                isEncrypted == false ? true : permissions is long permissionFlags ? (permissionFlags & 16L) != 0 : null,
                isEncrypted == false ? true : permissions is long permissionBits ? (permissionBits & 4L) != 0 : null,
                author,
                producer,
                title);
        }

        private static ExecutableMetadataSnapshot ReadExecutableMetadata(string filePath, FileDetailContext context)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
            var architecture = context.Unknown;
            bool? isDotNetAssembly = null;

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var peReader = new PEReader(stream);
                var headers = peReader.PEHeaders;
                var corHeader = headers.CorHeader;
                isDotNetAssembly = corHeader is not null;
                architecture = ResolveExecutableArchitecture(headers.CoffHeader.Machine, corHeader?.Flags, context);
            }
            catch
            {
                architecture = context.Unknown;
            }

            var isSigned = false;
            string? signer = null;
            try
            {
#pragma warning disable SYSLIB0057
                var certificate = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
                if (certificate is not null)
                {
                    using var certificate2 = new X509Certificate2(certificate);
                    isSigned = true;
                    signer = certificate2.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                    if (string.IsNullOrWhiteSpace(signer))
                    {
                        signer = certificate2.Subject;
                    }
                }
            }
            catch
            {
                isSigned = false;
                signer = null;
            }

            return new ExecutableMetadataSnapshot(
                NormalizeValue(versionInfo.FileVersion),
                NormalizeValue(versionInfo.ProductName),
                NormalizeValue(versionInfo.CompanyName),
                NormalizeValue(versionInfo.FileDescription),
                NormalizeValue(versionInfo.OriginalFilename),
                NormalizeValue(architecture),
                isDotNetAssembly,
                isSigned,
                NormalizeValue(signer));
        }

        private static string ResolveExecutableArchitecture(Machine machine, CorFlags? corFlags, FileDetailContext context)
        {
            return machine switch
            {
                Machine.I386 when corFlags is not null &&
                                  !corFlags.Value.HasFlag(CorFlags.Requires32Bit) &&
                                  !corFlags.Value.HasFlag(CorFlags.Prefers32Bit) =>
                    context.T("ArchitectureAnyCpu"),
                Machine.I386 => "x86",
                Machine.Amd64 => "x64",
                Machine.Arm => "ARM",
                Machine.Arm64 => "ARM64",
                Machine.IA64 => "IA64",
                _ => context.Unknown
            };
        }

        private static CodecSnapshot ReadCodecSnapshot(string filePath, int fallbackWidth, int fallbackHeight, int fallbackFrameCount, bool hasTransparency)
        {
            var width = Math.Max(0, fallbackWidth);
            var height = Math.Max(0, fallbackHeight);
            var frameCount = Math.Max(1, fallbackFrameCount);
            string? colorMode = null;
            int? bitDepth = null;
            double? frameRate = null;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                if (codec is null)
                {
                    return new CodecSnapshot(width, height, frameCount, colorMode, bitDepth, frameRate);
                }

                width = Math.Max(width, codec.Info.Width);
                height = Math.Max(height, codec.Info.Height);
                frameCount = Math.Max(frameCount, codec.FrameCount);
                colorMode = ResolveColorMode(codec.Info.ColorType, hasTransparency);
                bitDepth = ResolveBitDepth(codec.Info.ColorType);

                if (frameCount > 1)
                {
                    var durations = codec.FrameInfo
                        .Select(frame => Math.Max(0, frame.Duration))
                        .Where(duration => duration > 0)
                        .ToArray();

                    if (durations.Length > 0)
                    {
                        frameRate = 1000d / durations.Average();
                    }
                }
            }
            catch
            {
                // Keep fallback values from imported metadata when deeper codec inspection fails.
            }

            return new CodecSnapshot(width, height, frameCount, colorMode, bitDepth, frameRate);
        }

        private static DrawingSnapshot ReadDrawingSnapshot(string filePath, bool hasTransparency)
        {
            if (!OperatingSystem.IsWindows())
            {
                return default;
            }

            try
            {
                return ReadDrawingSnapshotCore(filePath, hasTransparency);
            }
            catch
            {
                return default;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static DrawingSnapshot ReadDrawingSnapshotCore(string filePath, bool hasTransparency)
        {
            using var stream = File.OpenRead(filePath);
            using var image = DrawingImage.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            float? dpiX = image.HorizontalResolution > 0 ? image.HorizontalResolution : null;
            float? dpiY = image.VerticalResolution > 0 ? image.VerticalResolution : null;
            var pixelFormat = image.PixelFormat;

            return new DrawingSnapshot(
                dpiX,
                dpiY,
                ResolveColorMode(pixelFormat, hasTransparency),
                ResolveBitDepth(pixelFormat),
                image.PropertyIdList.Contains(ExifTagIccProfile),
                TryReadExifString(image, ExifTagCameraModel),
                TryReadExifDate(image, ExifTagDateTaken),
                TryReadExifIso(image, ExifTagIso),
                TryReadExifAperture(image, ExifTagAperture),
                TryReadExifShutter(image, ExifTagShutterSpeed));
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? TryReadExifString(DrawingImage image, int propertyId)
        {
            try
            {
                if (!image.PropertyIdList.Contains(propertyId))
                {
                    return null;
                }

                var property = image.GetPropertyItem(propertyId);
                if (property is null)
                {
                    return null;
                }

                var bytes = property.Value;
                if (bytes is null || bytes.Length == 0)
                {
                    return null;
                }

                return NormalizeValue(Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' '));
            }
            catch
            {
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? TryReadExifDate(DrawingImage image, int propertyId)
        {
            var raw = TryReadExifString(image, propertyId);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                return timestamp.ToString("G", CultureInfo.CurrentCulture);
            }

            return raw;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? TryReadExifIso(DrawingImage image, int propertyId)
        {
            var numeric = TryReadExifUnsignedInteger(image, propertyId);
            return numeric is > 0 ? numeric.Value.ToString("N0", CultureInfo.CurrentCulture) : null;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? TryReadExifAperture(DrawingImage image, int propertyId)
        {
            var value = TryReadExifRational(image, propertyId);
            return value is > 0d ? string.Create(CultureInfo.CurrentCulture, $"f/{value:0.0#}") : null;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? TryReadExifShutter(DrawingImage image, int propertyId)
        {
            var value = TryReadExifRational(image, propertyId);
            if (value is null || value <= 0d)
            {
                return null;
            }

            if (value < 1d)
            {
                var reciprocal = Math.Round(1d / value.Value);
                if (reciprocal >= 1d)
                {
                    return string.Create(CultureInfo.InvariantCulture, $"1/{reciprocal:0} s");
                }
            }

            return string.Create(CultureInfo.CurrentCulture, $"{value:0.###} s");
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static uint? TryReadExifUnsignedInteger(DrawingImage image, int propertyId)
        {
            try
            {
                if (!image.PropertyIdList.Contains(propertyId))
                {
                    return null;
                }

                var property = image.GetPropertyItem(propertyId);
                if (property is null)
                {
                    return null;
                }

                var bytes = property.Value;
                if (bytes is null || bytes.Length < sizeof(ushort))
                {
                    return null;
                }

                return property.Len >= sizeof(uint)
                    ? BitConverter.ToUInt32(bytes, 0)
                    : BitConverter.ToUInt16(bytes, 0);
            }
            catch
            {
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static double? TryReadExifRational(DrawingImage image, int propertyId)
        {
            try
            {
                if (!image.PropertyIdList.Contains(propertyId))
                {
                    return null;
                }

                var property = image.GetPropertyItem(propertyId);
                if (property is null)
                {
                    return null;
                }

                var bytes = property.Value;
                if (bytes is null || bytes.Length < sizeof(uint) * 2)
                {
                    return null;
                }

                var numerator = BitConverter.ToUInt32(bytes, 0);
                var denominator = BitConverter.ToUInt32(bytes, sizeof(uint));
                if (denominator == 0)
                {
                    return null;
                }

                return numerator / (double)denominator;
            }
            catch
            {
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? ResolveColorMode(DrawingPixelFormat pixelFormat, bool hasTransparency)
        {
            if (pixelFormat == DrawingPixelFormat.Undefined || pixelFormat == DrawingPixelFormat.DontCare)
            {
                return null;
            }

            if (pixelFormat == DrawingPixelFormat.Format16bppGrayScale)
            {
                return "Grayscale";
            }

            if ((pixelFormat & DrawingPixelFormat.Indexed) == DrawingPixelFormat.Indexed)
            {
                return "Indexed";
            }

            if (hasTransparency ||
                pixelFormat is DrawingPixelFormat.Format32bppArgb or DrawingPixelFormat.Format32bppPArgb or DrawingPixelFormat.Format64bppArgb or DrawingPixelFormat.Format64bppPArgb)
            {
                return "RGBA";
            }

            return "RGB";
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static int? ResolveBitDepth(DrawingPixelFormat pixelFormat)
        {
            return pixelFormat == DrawingPixelFormat.Undefined || pixelFormat == DrawingPixelFormat.DontCare
                ? null
                : DrawingImage.GetPixelFormatSize(pixelFormat);
        }

        private static string? ResolveColorMode(SKColorType colorType, bool hasTransparency)
        {
            return colorType switch
            {
                SKColorType.Gray8 or SKColorType.Alpha8 => "Grayscale",
                SKColorType.Rgb565 => "RGB",
                SKColorType.Bgra8888 or SKColorType.Rgba8888 or SKColorType.Bgra1010102 or SKColorType.Rgba1010102 or SKColorType.RgbaF16 => hasTransparency ? "RGBA" : "RGB",
                SKColorType.Rgb888x => "RGB",
                _ => hasTransparency ? "RGBA" : "RGB"
            };
        }

        private static int? ResolveBitDepth(SKColorType colorType)
        {
            return colorType switch
            {
                SKColorType.Gray8 or SKColorType.Alpha8 => 8,
                SKColorType.Rgb565 => 16,
                SKColorType.Bgra8888 or SKColorType.Rgba8888 or SKColorType.Rgb888x => 32,
                SKColorType.Bgra1010102 or SKColorType.Rgba1010102 => 32,
                SKColorType.RgbaF16 => 64,
                _ => null
            };
        }

        private static string ResolveCompressionLabel(string extension, int frameCount)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "JPEG (lossy)",
                ".png" => "PNG (lossless)",
                ".webp" when frameCount > 1 => "WEBP (animated)",
                ".webp" => "WEBP",
                ".bmp" => "BMP",
                ".gif" when frameCount > 1 => "GIF (animated)",
                ".gif" => "GIF",
                ".tif" or ".tiff" => "TIFF",
                ".ico" => "ICO",
                ".svg" => "SVG (vector)",
                ".psd" => "PSD",
                _ => extension.TrimStart('.').ToUpperInvariant()
            };
        }

        private static string? ReadPdfSampleText(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                using var stream = File.OpenRead(filePath);
                if (fileInfo.Length <= PdfSampleBytes * 2L)
                {
                    var allBytes = new byte[(int)fileInfo.Length];
                    stream.ReadExactly(allBytes);
                    return Encoding.Latin1.GetString(allBytes);
                }

                var head = new byte[PdfSampleBytes];
                stream.ReadExactly(head);

                stream.Seek(-PdfSampleBytes, SeekOrigin.End);
                var tail = new byte[PdfSampleBytes];
                stream.ReadExactly(tail);

                return Encoding.Latin1.GetString(head) + "\n" + Encoding.Latin1.GetString(tail);
            }
            catch
            {
                return null;
            }
        }

        private static string? TryReadPdfVersion(string? sample)
        {
            if (string.IsNullOrWhiteSpace(sample))
            {
                return null;
            }

            var markerIndex = sample.IndexOf("%PDF-", StringComparison.Ordinal);
            if (markerIndex < 0 || markerIndex + 8 > sample.Length)
            {
                return null;
            }

            return NormalizeValue(sample[(markerIndex + 5)..Math.Min(sample.Length, markerIndex + 8)].Trim());
        }

        private static long? TryReadPdfPermissionFlags(string? sample)
        {
            if (string.IsNullOrWhiteSpace(sample))
            {
                return null;
            }

            var encryptIndex = sample.IndexOf("/Encrypt", StringComparison.Ordinal);
            if (encryptIndex < 0)
            {
                return null;
            }

            var window = sample[encryptIndex..Math.Min(sample.Length, encryptIndex + 512)];
            var match = Regex.Match(window, @"/P\s+(-?\d+)", RegexOptions.CultureInvariant);
            return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var permissions)
                ? permissions
                : null;
        }

        private static PdfExtractedValue TryExtractPdfValue(string? sample, string key)
        {
            if (string.IsNullOrWhiteSpace(sample))
            {
                return default;
            }

            var token = "/" + key;
            var searchStart = 0;
            while (searchStart < sample.Length)
            {
                var index = sample.IndexOf(token, searchStart, StringComparison.Ordinal);
                if (index < 0)
                {
                    return default;
                }

                var cursor = index + token.Length;
                if (cursor < sample.Length && (char.IsLetterOrDigit(sample[cursor]) || sample[cursor] == '#'))
                {
                    searchStart = cursor;
                    continue;
                }

                while (cursor < sample.Length && char.IsWhiteSpace(sample[cursor]))
                {
                    cursor++;
                }

                if (cursor >= sample.Length)
                {
                    return default;
                }

                if (sample[cursor] == '(')
                {
                    return new PdfExtractedValue(true, ReadPdfLiteralString(sample, cursor));
                }

                if (sample[cursor] == '<' && (cursor + 1 >= sample.Length || sample[cursor + 1] != '<'))
                {
                    return new PdfExtractedValue(true, ReadPdfHexString(sample, cursor));
                }

                searchStart = cursor;
            }

            return default;
        }

        private static string? ReadPdfLiteralString(string sample, int startIndex)
        {
            var bytes = new List<byte>();
            var escaped = false;
            var depth = 1;

            for (var index = startIndex + 1; index < sample.Length; index++)
            {
                var character = sample[index];
                if (escaped)
                {
                    if (character is >= '0' and <= '7')
                    {
                        var octalLength = 1;
                        while (octalLength < 3 &&
                               index + octalLength < sample.Length &&
                               sample[index + octalLength] is >= '0' and <= '7')
                        {
                            octalLength++;
                        }

                        var octal = sample.Substring(index, octalLength);
                        bytes.Add(Convert.ToByte(octal, 8));
                        index += octalLength - 1;
                        escaped = false;
                        continue;
                    }

                    switch (character)
                    {
                        case 'n':
                            bytes.Add((byte)'\n');
                            break;
                        case 'r':
                            bytes.Add((byte)'\r');
                            break;
                        case 't':
                            bytes.Add((byte)'\t');
                            break;
                        case 'b':
                            bytes.Add((byte)'\b');
                            break;
                        case 'f':
                            bytes.Add((byte)'\f');
                            break;
                        case '\r':
                            if (index + 1 < sample.Length && sample[index + 1] == '\n')
                            {
                                index++;
                            }

                            break;
                        case '\n':
                            break;
                        default:
                            bytes.Add((byte)character);
                            break;
                    }

                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '(')
                {
                    depth++;
                    bytes.Add((byte)character);
                    continue;
                }

                if (character == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }

                    bytes.Add((byte)character);
                    continue;
                }

                bytes.Add((byte)character);
            }

            return DecodePdfTextBytes([.. bytes]);
        }

        private static string? ReadPdfHexString(string sample, int startIndex)
        {
            var endIndex = sample.IndexOf('>', startIndex + 1);
            if (endIndex <= startIndex)
            {
                return null;
            }

            var hex = new string(sample[(startIndex + 1)..endIndex].Where(Uri.IsHexDigit).ToArray());
            if (hex.Length == 0)
            {
                return null;
            }

            if (hex.Length % 2 != 0)
            {
                hex += "0";
            }

            var bytes = new byte[hex.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return DecodePdfTextBytes(bytes);
        }

        private static string FormatPdfPageSize(int width, int height)
        {
            var widthMm = width * 25.4d / 72d;
            var heightMm = height * 25.4d / 72d;
            var standardSize = ResolveStandardPageSize(widthMm, heightMm);

            return standardSize is null
                ? string.Create(CultureInfo.CurrentCulture, $"{widthMm:0.#} x {heightMm:0.#} mm")
                : string.Create(CultureInfo.CurrentCulture, $"{standardSize} ({widthMm:0.#} x {heightMm:0.#} mm)");
        }

        private static string? ResolveStandardPageSize(double widthMm, double heightMm)
        {
            var candidates = new (string Name, double WidthMm, double HeightMm)[]
            {
                ("A5", 148d, 210d),
                ("A4", 210d, 297d),
                ("A3", 297d, 420d),
                ("Letter", 215.9d, 279.4d),
                ("Legal", 215.9d, 355.6d)
            };

            foreach (var candidate in candidates)
            {
                if (MatchesPageSize(widthMm, heightMm, candidate.WidthMm, candidate.HeightMm) ||
                    MatchesPageSize(widthMm, heightMm, candidate.HeightMm, candidate.WidthMm))
                {
                    return candidate.Name;
                }
            }

            return null;
        }

        private static bool MatchesPageSize(double widthMm, double heightMm, double expectedWidthMm, double expectedHeightMm)
        {
            const double toleranceMm = 6d;
            return Math.Abs(widthMm - expectedWidthMm) <= toleranceMm &&
                   Math.Abs(heightMm - expectedHeightMm) <= toleranceMm;
        }

        private static string? FormatCount(int? value)
        {
            return value is int count && count >= 0
                ? count.ToString("N0", CultureInfo.CurrentCulture)
                : null;
        }

        private static string? FormatBitDepth(int? bitDepth)
        {
            return bitDepth is > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{bitDepth}-bit")
                : null;
        }

        private static string? FormatPixelSize(int? width, int? height)
        {
            return width is > 0 && height is > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{width} x {height} px")
                : null;
        }

        private static string? FormatPresenceWithCount(FileDetailContext context, int? count)
        {
            return count switch
            {
                null => null,
                <= 0 => context.No,
                var value => string.Create(CultureInfo.CurrentCulture, $"{context.Yes} ({value:N0})")
            };
        }

        private static string? BuildLayerNameSummary(IReadOnlyList<string> layerNames)
        {
            return layerNames.Count == 0
                ? null
                : string.Join(Environment.NewLine, layerNames.Select(static name => $"- {name}"));
        }

        private static string? BuildLayerStructureSummary(FileDetailContext context, PsdDetailInfo detail)
        {
            var lines = new List<string>();

            AppendLayerSummaryLine(lines, context, "PsdLayerSummaryLockedLayers", detail.LockedLayerCount);
            AppendLayerSummaryLine(lines, context, "PsdLayerSummaryTextLayers", detail.TextLayerCount);
            AppendLayerSummaryLine(lines, context, "PsdLayerSummaryImageLayers", detail.ImageLayerCount);
            AppendLayerSummaryLine(lines, context, "PsdLayerSummaryShapeLayers", detail.ShapeLayerCount);
            AppendLayerSummaryLine(lines, context, "PsdLayerSummaryGroupLayers", detail.LayerGroupCount ?? 0);

            return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
        }

        private static void AppendLayerSummaryLine(List<string> lines, FileDetailContext context, string labelKey, int count)
        {
            if (count <= 0)
            {
                return;
            }

            lines.Add("- " + string.Format(
                CultureInfo.CurrentCulture,
                context.T("PsdLayerSummaryCountTemplate"),
                context.T(labelKey),
                count.ToString("N0", CultureInfo.CurrentCulture)));
        }

        private Bitmap? CreatePreview(FileDetailContext context)
        {
            if (context.Item.IsPdfDocument && context.Item.NeedsPdfUnlock)
            {
                return _pdfRenderService.TryCreateLockedPreview(DetailPreviewWidth);
            }

            var filePath = context.Item.FilePath;
            var preview = ImageConversionService.TryCreatePreview(filePath, DetailPreviewWidth, svgUseBackground: false, svgBackgroundColor: null);
            if (preview is not null || !Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return preview;
            }

            return ImageConversionService.TryCreatePreview(filePath, DetailPreviewWidth, svgUseBackground: true, svgBackgroundColor: "#FFFFFFFF");
        }

        private static string FormatFileSize(long bytes)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;
            var size = bytes < kb
                ? string.Create(CultureInfo.InvariantCulture, $"{bytes} B")
                : bytes < mb
                    ? string.Create(CultureInfo.InvariantCulture, $"{bytes / kb:0.0} KB")
                    : string.Create(CultureInfo.InvariantCulture, $"{bytes / mb:0.0} MB");

            return string.Create(CultureInfo.CurrentCulture, $"{size} ({bytes:N0} B)");
        }

        private static string FormatTimestamp(DateTime timestamp)
        {
            return timestamp.ToString("G", CultureInfo.CurrentCulture);
        }

        private static string? FormatDpi(float? dpiX, float? dpiY)
        {
            if (dpiX is null || dpiY is null)
            {
                return null;
            }

            return string.Create(CultureInfo.CurrentCulture, $"{dpiX:0.##} x {dpiY:0.##}");
        }

        private static string FormatBoolean(FileDetailContext context, bool value)
        {
            return value ? context.Yes : context.No;
        }

        private static string? FormatNullableBoolean(FileDetailContext context, bool? value)
        {
            return value switch
            {
                true => context.Yes,
                false => context.No,
                _ => null
            };
        }

        private bool SafeHasTransparency(ImageItemViewModel item)
        {
            try
            {
                return _imageAnalysisService.HasTransparency(item);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(FileDetailService), $"Failed to inspect transparency for '{item.FilePath}'.", ex);
                return false;
            }
        }

        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? ResolvePdfMetadataValue(PdfExtractedValue value, FileDetailContext context)
        {
            if (!value.Found)
            {
                return null;
            }

            return NormalizeValue(value.Value) ?? context.Unknown;
        }

        private string? ReadAccessiblePdfSampleText(ImageItemViewModel item)
        {
            if (!item.IsPdfDocument)
            {
                return null;
            }

            var resolvedPath = _pdfSecurityService.ResolveAccessiblePath(item.FilePath);
            return ReadPdfSampleText(resolvedPath);
        }

        private static string? ReadPdfMetadataValue(
            string? primarySample,
            string? fallbackSample,
            string key,
            FileDetailContext context,
            bool preferXmpValue)
        {
            var foundEmptyValue = false;

            foreach (var sample in EnumerateDistinctPdfSamples(primarySample, fallbackSample))
            {
                var candidates = preferXmpValue
                    ? new[] { TryExtractPdfXmpValue(sample, key), TryExtractPdfValue(sample, key) }
                    : new[] { TryExtractPdfValue(sample, key), TryExtractPdfXmpValue(sample, key) };

                foreach (var candidate in candidates)
                {
                    if (!candidate.Found)
                    {
                        continue;
                    }

                    var resolved = NormalizeValue(candidate.Value);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        return resolved;
                    }

                    foundEmptyValue = true;
                }
            }

            return foundEmptyValue ? context.Unknown : null;
        }

        private static IEnumerable<string> EnumerateDistinctPdfSamples(string? primarySample, string? fallbackSample)
        {
            if (!string.IsNullOrWhiteSpace(primarySample))
            {
                yield return primarySample;
            }

            if (!string.IsNullOrWhiteSpace(fallbackSample) &&
                !string.Equals(primarySample, fallbackSample, StringComparison.Ordinal))
            {
                yield return fallbackSample;
            }
        }

        private static PdfExtractedValue TryExtractPdfXmpValue(string? sample, string key)
        {
            if (string.IsNullOrWhiteSpace(sample))
            {
                return default;
            }

            return key switch
            {
                "Producer" => TryMatchPdfXmpValue(sample, @"pdf:Producer\s*=\s*(['""])(?<value>.*?)\1"),
                "Title" => TryMatchPdfXmpValue(sample, @"<dc:title>\s*<rdf:Alt>\s*<rdf:li\b[^>]*>(?<value>.*?)</rdf:li>"),
                "Author" => TryMatchPdfXmpValue(sample, @"<dc:creator>\s*<rdf:Seq>\s*<rdf:li\b[^>]*>(?<value>.*?)</rdf:li>"),
                _ => default
            };
        }

        private static PdfExtractedValue TryMatchPdfXmpValue(string sample, string pattern)
        {
            var match = Regex.Match(
                sample,
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
            {
                return default;
            }

            var value = WebUtility.HtmlDecode(match.Groups["value"].Value);
            return new PdfExtractedValue(true, NormalizePdfDecodedText(value));
        }

        private static string? DecodePdfTextBytes(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return null;
            }

            try
            {
                if (bytes.Length >= 3 &&
                    bytes[0] == 0xEF &&
                    bytes[1] == 0xBB &&
                    bytes[2] == 0xBF)
                {
                    return NormalizePdfDecodedText(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
                }

                if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    return NormalizePdfDecodedText(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
                }

                if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    return NormalizePdfDecodedText(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
                }

                try
                {
                    return NormalizePdfDecodedText(StrictUtf8Encoding.GetString(bytes));
                }
                catch (DecoderFallbackException)
                {
                    return NormalizePdfDecodedText(Encoding.Latin1.GetString(bytes));
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? NormalizePdfDecodedText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return NormalizeValue(value
                .Replace("\0", string.Empty, StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal));
        }

        private static string BuildFileTypeText(FileDetailContext context, string typeLabelKey)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                context.T("FileDetailTypeTemplate"),
                context.T(typeLabelKey),
                $".{context.Item.Extension}");
        }

        private interface IFileDetailProvider
        {
            bool CanHandle(FileDetailContext context);

            string GetTypeLabelKey(FileDetailContext context);

            string GetPreviewDescription(FileDetailContext context);

            IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context);
        }

        private sealed class ImageFileDetailProvider : IFileDetailProvider
        {
            public bool CanHandle(FileDetailContext context)
            {
                return ImageExtensions.Contains(Path.GetExtension(context.Item.FilePath));
            }

            public string GetTypeLabelKey(FileDetailContext context)
            {
                return "FileDetailTypeImage";
            }

            public string GetPreviewDescription(FileDetailContext context)
            {
                return context.T("FileDetailPreviewImage");
            }

            public IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context)
            {
                return [context.Service.BuildImageSection(context)];
            }
        }

        private sealed class PdfFileDetailProvider : IFileDetailProvider
        {
            public bool CanHandle(FileDetailContext context)
            {
                return Path.GetExtension(context.Item.FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            }

            public string GetTypeLabelKey(FileDetailContext context)
            {
                return "FileDetailTypePdf";
            }

            public string GetPreviewDescription(FileDetailContext context)
            {
                return context.Item.NeedsPdfUnlock
                    ? context.T("PdfPreviewLockedDescription")
                    : context.T("FileDetailPreviewPdf");
            }

            public IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context)
            {
                return context.Item.NeedsPdfUnlock
                    ? []
                    : [context.Service.BuildPdfSection(context)];
            }
        }

        private sealed class PsdFileDetailProvider : IFileDetailProvider
        {
            public bool CanHandle(FileDetailContext context)
            {
                return PsdImportService.IsPsdFile(context.Item.FilePath);
            }

            public string GetTypeLabelKey(FileDetailContext context)
            {
                return "FileDetailTypePsd";
            }

            public string GetPreviewDescription(FileDetailContext context)
            {
                return context.T("FileDetailPreviewPsd");
            }

            public IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context)
            {
                return context.Service.BuildPsdSections(context);
            }
        }

        private sealed class ExecutableFileDetailProvider : IFileDetailProvider
        {
            public bool CanHandle(FileDetailContext context)
            {
                return Path.GetExtension(context.Item.FilePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
            }

            public string GetTypeLabelKey(FileDetailContext context)
            {
                return "FileDetailTypeExecutable";
            }

            public string GetPreviewDescription(FileDetailContext context)
            {
                return context.T("FileDetailPreviewExecutable");
            }

            public IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context)
            {
                return [context.Service.BuildExecutableSection(context)];
            }
        }

        private sealed class ShortcutFileDetailProvider : IFileDetailProvider
        {
            public bool CanHandle(FileDetailContext context)
            {
                return Path.GetExtension(context.Item.FilePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            }

            public string GetTypeLabelKey(FileDetailContext context)
            {
                return "FileDetailTypeShortcut";
            }

            public string GetPreviewDescription(FileDetailContext context)
            {
                return context.T("FileDetailPreviewShortcut");
            }

            public IReadOnlyList<FileDetailSection> BuildSections(FileDetailContext context)
            {
                return [context.Service.BuildShortcutSection(context)];
            }
        }

        private sealed record FileDetailContext(ImageItemViewModel Item, FileInfo FileInfo, FileDetailService Service)
        {
            public string Yes => Service.Translate("YesText");

            public string No => Service.Translate("NoText");

            public string Unknown => Service.Translate("UnknownText");

            public string NotAvailable => Service.Translate("NotAvailableText");

            public string T(string key)
            {
                return Service.Translate(key);
            }

            public FileDetailEntry Entry(string labelKey, string? value)
            {
                return new FileDetailEntry(T(labelKey), string.IsNullOrWhiteSpace(value) ? NotAvailable : value);
            }
        }

        private readonly record struct CodecSnapshot(
            int Width,
            int Height,
            int FrameCount,
            string? ColorMode,
            int? BitDepth,
            double? FrameRate);

        private readonly record struct DrawingSnapshot(
            float? DpiX,
            float? DpiY,
            string? ColorMode,
            int? BitDepth,
            bool? HasIccProfile,
            string? CameraModel,
            string? DateTaken,
            string? Iso,
            string? Aperture,
            string? ShutterSpeed);

        private readonly record struct ImageMetadataSnapshot(
            string? ResolutionText,
            string? PixelCountText,
            string? ColorMode,
            string? BitDepthText,
            bool HasTransparency,
            string? DpiText,
            bool? HasIccProfile,
            string? Compression,
            string? CameraModel,
            string? DateTaken,
            string? Iso,
            string? Aperture,
            string? ShutterSpeed,
            string? AnimationFrameCountText,
            string? FrameRateText);

        private readonly record struct PdfMetadataSnapshot(
            string? PageCountText,
            string? Version,
            string? PageSize,
            bool? IsEncrypted,
            bool? AllowCopy,
            bool? AllowPrint,
            string? Author,
            string? Producer,
            string? Title);

        private readonly record struct PdfExtractedValue(bool Found, string? Value);

        private readonly record struct ExecutableMetadataSnapshot(
            string? FileVersion,
            string? ProductName,
            string? CompanyName,
            string? FileDescription,
            string? OriginalFileName,
            string? Architecture,
            bool? IsDotNetAssembly,
            bool? IsSigned,
            string? Signer);
    }
}
