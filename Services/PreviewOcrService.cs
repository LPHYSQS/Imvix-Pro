using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace ImvixPro.Services
{
    public enum PreviewOcrLanguageOption
    {
        Auto = 0,
        SimplifiedChinese,
        TraditionalChinese,
        English,
        Japanese,
        Korean,
        German,
        French,
        Italian,
        Russian,
        Arabic
    }

    public sealed record PreviewOcrRecognition(
        string Text,
        bool HasText,
        float MeanConfidence,
        string? ErrorMessage = null)
    {
        public static PreviewOcrRecognition Empty(float meanConfidence = 0f)
        {
            return new PreviewOcrRecognition(string.Empty, false, meanConfidence);
        }

        public static PreviewOcrRecognition Error(string message)
        {
            return new PreviewOcrRecognition(string.Empty, false, 0f, message);
        }
    }

    public sealed class PreviewOcrService
    {
        private const float ChineseAutoRetryConfidenceThreshold = 0.9f;
        private const int ChineseUpscaledLongestEdge = 2800;
        private const float KoreanAutoRetryConfidenceThreshold = 0.9f;
        private const float KoreanAutoRetryLatinFallbackThreshold = 0.9f;
        private const int KoreanUpscaledLongestEdge = 2600;
        private static readonly IReadOnlyDictionary<char, char> TraditionalToSimplifiedMap =
            new Dictionary<char, char>
            {
                ['\u8B58'] = '\u8BC6',
                ['\u5225'] = '\u522B',
                ['\u6E2C'] = '\u6D4B',
                ['\u8A66'] = '\u8BD5',
                ['\u7C21'] = '\u7B80',
                ['\u9AD4'] = '\u4F53',
                ['\u958B'] = '\u5F00',
                ['\u95DC'] = '\u5173',
                ['\u767C'] = '\u53D1',
                ['\u73FE'] = '\u73B0',
                ['\u5716'] = '\u56FE',
                ['\u5C64'] = '\u5C42',
                ['\u9801'] = '\u9875',
                ['\u7DB2'] = '\u7F51',
                ['\u9EDE'] = '\u70B9',
                ['\u9304'] = '\u5F55',
                ['\u9580'] = '\u95E8',
                ['\u66F8'] = '\u4E66',
                ['\u9F8D'] = '\u9F99',
                ['\u756B'] = '\u753B',
                ['\u96F2'] = '\u4E91',
                ['\u81FA'] = '\u53F0',
                ['\u6A94'] = '\u6863',
                ['\u8B49'] = '\u8BC1',
                ['\u78BC'] = '\u7801',
                ['\u932F'] = '\u9519',
                ['\u8AA4'] = '\u8BEF',
                ['\u9810'] = '\u9884',
                ['\u89BD'] = '\u89C8',
                ['\u9078'] = '\u9009',
                ['\u64C7'] = '\u62E9',
                ['\u8CC7'] = '\u8D44',
                ['\u8A0A'] = '\u8BAF',
                ['\u986F'] = '\u663E',
                ['\u8655'] = '\u5904',
                ['\u7DDA'] = '\u7EBF',
                ['\u7DE8'] = '\u7F16',
                ['\u8F2F'] = '\u8F91',
                ['\u5FA9'] = '\u590D',
                ['\u6AA2'] = '\u68C0',
                ['\u6B0A'] = '\u6743',
                ['\u8F49'] = '\u8F6C',
                ['\u63DB'] = '\u6362',
                ['\u58D3'] = '\u538B',
                ['\u7E2E'] = '\u7F29',
                ['\u5831'] = '\u62A5',
                ['\u5C0E'] = '\u5BFC',
                ['\u8F38'] = '\u8F93',
                ['\u96D9'] = '\u53CC',
                ['\u64CA'] = '\u51FB',
                ['\u8996'] = '\u89C6',
                ['\u5C08'] = '\u4E13',
                ['\u696D'] = '\u4E1A',
                ['\u6A19'] = '\u6807',
                ['\u984C'] = '\u9898',
                ['\u5167'] = '\u5185',
                ['\u97FF'] = '\u54CD',
                ['\u61C9'] = '\u5E94',
                ['\u9375'] = '\u952E',
                ['\u76E4'] = '\u76D8',
                ['\u98A8'] = '\u98CE',
                ['\u7D1A'] = '\u7EA7',
                ['\u512A'] = '\u4F18',
                ['\u6B78'] = '\u5F52',
                ['\u7D93'] = '\u7ECF',
                ['\u9A57'] = '\u9A8C',
                ['\u6578'] = '\u6570',
                ['\u64DA'] = '\u636E',
                ['\u7E3D'] = '\u603B',
                ['\u7D50'] = '\u7ED3',
                ['\u5BE6'] = '\u5B9E',
                ['\u969B'] = '\u9645',
                ['\u8A0E'] = '\u8BA8',
                ['\u8AD6'] = '\u8BBA',
                ['\u8207'] = '\u4E0E',
                ['\u70BA'] = '\u4E3A',
                ['\u6642'] = '\u65F6',
                ['\u9593'] = '\u95F4',
                ['\u7D14'] = '\u7EAF',
                ['\u55CE'] = '\u5417',
                ['\u9019'] = '\u8FD9',
                ['\u500B'] = '\u4E2A',
                ['\u6703'] = '\u4F1A',
                ['\u8B93'] = '\u8BA9',
                ['\u5F8C'] = '\u540E'
            };
        private static readonly HashSet<char> TraditionalDistinctChars = TraditionalToSimplifiedMap.Keys.ToHashSet();
        private static readonly HashSet<char> SimplifiedDistinctChars = TraditionalToSimplifiedMap.Values.ToHashSet();
        private static readonly PreviewOcrLanguageProfile[] SupportedLanguageProfiles =
        [
            new(PreviewOcrLanguageOption.SimplifiedChinese, ["chi_sim", "eng"]),
            new(PreviewOcrLanguageOption.TraditionalChinese, ["chi_tra", "eng"]),
            new(PreviewOcrLanguageOption.English, ["eng"]),
            new(PreviewOcrLanguageOption.Japanese, ["jpn", "eng"]),
            new(PreviewOcrLanguageOption.Korean, ["kor", "eng"]),
            new(PreviewOcrLanguageOption.German, ["deu", "eng"]),
            new(PreviewOcrLanguageOption.French, ["fra", "eng"]),
            new(PreviewOcrLanguageOption.Italian, ["ita", "eng"]),
            new(PreviewOcrLanguageOption.Russian, ["rus", "eng"]),
            new(PreviewOcrLanguageOption.Arabic, ["ara", "eng"])
        ];

        private static readonly string[] AutoLanguageDataCodes =
        [
            "chi_sim",
            "chi_tra",
            "eng",
            "jpn",
            "kor",
            "deu",
            "fra",
            "ita",
            "rus",
            "ara"
        ];

        private static readonly string AllSupportedLanguages = string.Join("+", AutoLanguageDataCodes);
        private static readonly string[] RequiredLanguageFiles =
            AutoLanguageDataCodes.Select(code => $"{code}.traineddata").ToArray();
        private static readonly IReadOnlyDictionary<PreviewOcrLanguageOption, string> LanguageSetMap =
            SupportedLanguageProfiles.ToDictionary(
                profile => profile.Option,
                profile => string.Join("+", profile.DataCodes));

        private static readonly string[] RequiredNativeFiles =
        [
            "leptonica-1.82.0.dll",
            "tesseract50.dll"
        ];

        private static readonly SemaphoreSlim EngineGate = new(1, 1);

        private static readonly Dictionary<string, TesseractEngine> EngineCache = new(StringComparer.Ordinal);
        private static readonly Lazy<PreviewOcrRuntimeStatus> RuntimeStatus =
            new(InitializeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);

        public const string UnsupportedPlatformErrorCode = "preview-ocr-unsupported-platform";
        public const string PathErrorCode = "preview-ocr-path-error";
        public const string EngineFilesMissingErrorCode = "preview-ocr-engine-files-missing";
        public const string InitializationFailedErrorCode = "preview-ocr-initialization-failed";

        public static IReadOnlyList<PreviewOcrLanguageOption> SupportedLanguageOptions { get; } =
            SupportedLanguageProfiles.Select(profile => profile.Option).ToArray();

        static PreviewOcrService()
        {
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeSharedEngine();
        }

        public static void WarmUpRuntime()
        {
            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady && !string.IsNullOrWhiteSpace(runtimeStatus.DiagnosticMessage))
            {
                Trace.TraceError($"OCR runtime validation failed: {runtimeStatus.DiagnosticMessage}");
            }
        }

        public async Task<PreviewOcrRecognition> RecognizeAsync(
            byte[] imageBytes,
            PreviewOcrLanguageOption languageOption = PreviewOcrLanguageOption.Auto,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return PreviewOcrRecognition.Error("OCR image data is empty.");
            }

            if (!OperatingSystem.IsWindows())
            {
                return PreviewOcrRecognition.Error(UnsupportedPlatformErrorCode);
            }

            var runtimeStatus = RuntimeStatus.Value;
            if (!runtimeStatus.IsReady)
            {
                return PreviewOcrRecognition.Error(runtimeStatus.ErrorCode ?? InitializationFailedErrorCode);
            }

            return await Task.Run(async () =>
            {
                await EngineGate.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return RecognizeCore(imageBytes, languageOption, runtimeStatus.TessDataPath);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"OCR recognition failed: {ex}");
                    return PreviewOcrRecognition.Error(InitializationFailedErrorCode);
                }
                finally
                {
                    EngineGate.Release();
                }
            }, cancellationToken);
        }

        private static PreviewOcrRecognition RecognizeCore(
            byte[] imageBytes,
            PreviewOcrLanguageOption languageOption,
            string tessDataPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return PreviewOcrRecognition.Error(UnsupportedPlatformErrorCode);
            }

            return languageOption switch
            {
                PreviewOcrLanguageOption.SimplifiedChinese => RecognizeBestChineseResult(
                    imageBytes,
                    TryCreateChineseUpscaledImageBytes(imageBytes),
                    PreviewOcrLanguageOption.SimplifiedChinese,
                    tessDataPath),
                PreviewOcrLanguageOption.TraditionalChinese => RecognizeBestChineseResult(
                    imageBytes,
                    TryCreateChineseUpscaledImageBytes(imageBytes),
                    PreviewOcrLanguageOption.TraditionalChinese,
                    tessDataPath),
                PreviewOcrLanguageOption.Korean => RecognizeBestKoreanResult(imageBytes, tessDataPath),
                PreviewOcrLanguageOption.Auto => RecognizeAutoWithLanguageRetry(imageBytes, tessDataPath),
                _ => RecognizeOnce(imageBytes, languageOption, tessDataPath, PageSegMode.Auto)
            };
        }

        [SupportedOSPlatform("windows")]
        private static PreviewOcrRecognition RecognizeAutoWithLanguageRetry(byte[] imageBytes, string tessDataPath)
        {
            var primary = RecognizeOnce(imageBytes, PreviewOcrLanguageOption.Auto, tessDataPath, PageSegMode.Auto);
            var best = primary;

            if (ShouldTryKoreanRetry(primary))
            {
                var koreanResult = RecognizeBestKoreanResult(imageBytes, tessDataPath);
                best = ChooseAutoOrKorean(best, koreanResult);
            }

            if (ShouldTryChineseRetry(best))
            {
                var chineseVariants = RecognizeBestChineseVariants(imageBytes, tessDataPath);
                best = ChooseAutoOrChinese(best, chineseVariants.Simplified, chineseVariants.Traditional);
            }

            return best;
        }

        [SupportedOSPlatform("windows")]
        private static ChineseVariantRecognition RecognizeBestChineseVariants(byte[] imageBytes, string tessDataPath)
        {
            var upscaledBytes = TryCreateChineseUpscaledImageBytes(imageBytes);
            return new ChineseVariantRecognition(
                RecognizeBestChineseResult(imageBytes, upscaledBytes, PreviewOcrLanguageOption.SimplifiedChinese, tessDataPath),
                RecognizeBestChineseResult(imageBytes, upscaledBytes, PreviewOcrLanguageOption.TraditionalChinese, tessDataPath));
        }

        private static PreviewOcrRecognition RecognizeBestChineseResult(
            byte[] imageBytes,
            byte[]? upscaledBytes,
            PreviewOcrLanguageOption languageOption,
            string tessDataPath)
        {
            var attempts = new List<RecognitionAttempt>
            {
                new("chinese-original-auto", NormalizeRecognition(
                    RecognizeOnce(imageBytes, languageOption, tessDataPath, PageSegMode.Auto),
                    languageOption)),
                new("chinese-original-sparse", NormalizeRecognition(
                    RecognizeOnce(imageBytes, languageOption, tessDataPath, PageSegMode.SparseText),
                    languageOption))
            };

            if (upscaledBytes is not null)
            {
                attempts.Add(new("chinese-upscaled-auto", NormalizeRecognition(
                    RecognizeOnce(upscaledBytes, languageOption, tessDataPath, PageSegMode.Auto),
                    languageOption)));
                attempts.Add(new("chinese-upscaled-sparse", NormalizeRecognition(
                    RecognizeOnce(upscaledBytes, languageOption, tessDataPath, PageSegMode.SparseText),
                    languageOption)));
            }

            return attempts
                .OrderByDescending(attempt => ScoreRecognition(attempt.Result, languageOption))
                .ThenByDescending(attempt => attempt.Result.MeanConfidence)
                .First()
                .Result;
        }

        private static PreviewOcrRecognition NormalizeRecognition(
            PreviewOcrRecognition recognition,
            PreviewOcrLanguageOption languageOption)
        {
            if (!recognition.HasText || languageOption != PreviewOcrLanguageOption.SimplifiedChinese)
            {
                return recognition;
            }

            var normalizedText = ConvertTraditionalToSimplified(recognition.Text);
            return normalizedText == recognition.Text
                ? recognition
                : recognition with { Text = normalizedText };
        }

        private static PreviewOcrRecognition ChooseAutoOrChinese(
            PreviewOcrRecognition primary,
            PreviewOcrRecognition simplifiedResult,
            PreviewOcrRecognition traditionalResult)
        {
            var simplifiedScore = ScoreRecognition(simplifiedResult, PreviewOcrLanguageOption.SimplifiedChinese);
            var traditionalScore = ScoreRecognition(traditionalResult, PreviewOcrLanguageOption.TraditionalChinese);
            var preferredChinese = simplifiedScore >= traditionalScore + 2d
                ? simplifiedResult
                : traditionalScore >= simplifiedScore + 2d
                    ? traditionalResult
                    : simplifiedResult.MeanConfidence >= traditionalResult.MeanConfidence
                        ? simplifiedResult
                        : traditionalResult;

            var preferredLanguage = ReferenceEquals(preferredChinese, traditionalResult)
                ? PreviewOcrLanguageOption.TraditionalChinese
                : PreviewOcrLanguageOption.SimplifiedChinese;
            var primaryScore = ScoreRecognition(primary, PreviewOcrLanguageOption.Auto);
            var preferredChineseScore = ScoreRecognition(preferredChinese, preferredLanguage);
            var primaryStats = AnalyzeText(primary.Text);
            var preferredStats = AnalyzeText(preferredChinese.Text);

            var hasCleanerChineseText =
                preferredStats.HanCount >= primaryStats.HanCount &&
                preferredStats.ChineseVariantMixedCount < primaryStats.ChineseVariantMixedCount;
            var hasVariantCorrection =
                preferredLanguage == PreviewOcrLanguageOption.SimplifiedChinese &&
                preferredStats.TraditionalDistinctCount == 0 &&
                primaryStats.TraditionalDistinctCount > 0;

            if (preferredChineseScore >= primaryScore + 3d || hasCleanerChineseText || hasVariantCorrection)
            {
                return preferredChinese;
            }

            return primary;
        }

        [SupportedOSPlatform("windows")]
        private static PreviewOcrRecognition RecognizeBestKoreanResult(byte[] imageBytes, string tessDataPath)
        {
            var attempts = new List<RecognitionAttempt>
            {
                new("korean-original-auto", RecognizeOnce(imageBytes, PreviewOcrLanguageOption.Korean, tessDataPath, PageSegMode.Auto)),
                new("korean-original-sparse", RecognizeOnce(imageBytes, PreviewOcrLanguageOption.Korean, tessDataPath, PageSegMode.SparseText))
            };

            var upscaledBytes = TryCreateKoreanUpscaledImageBytes(imageBytes);
            if (upscaledBytes is not null)
            {
                attempts.Add(new("korean-upscaled-auto", RecognizeOnce(upscaledBytes, PreviewOcrLanguageOption.Korean, tessDataPath, PageSegMode.Auto)));
                attempts.Add(new("korean-upscaled-sparse", RecognizeOnce(upscaledBytes, PreviewOcrLanguageOption.Korean, tessDataPath, PageSegMode.SparseText)));
            }

            return attempts
                .OrderByDescending(attempt => ScoreRecognition(attempt.Result, PreviewOcrLanguageOption.Korean))
                .ThenByDescending(attempt => attempt.Result.MeanConfidence)
                .First()
                .Result;
        }

        private static PreviewOcrRecognition ChooseAutoOrKorean(
            PreviewOcrRecognition primary,
            PreviewOcrRecognition koreanResult)
        {
            var primaryStats = AnalyzeText(primary.Text);
            var koreanStats = AnalyzeText(koreanResult.Text);
            var primaryScore = ScoreRecognition(primary, PreviewOcrLanguageOption.Auto);
            var koreanScore = ScoreRecognition(koreanResult, PreviewOcrLanguageOption.Korean);

            var hasMeaningfulHangulImprovement =
                koreanStats.HangulCount > primaryStats.HangulCount &&
                koreanResult.MeanConfidence >= primary.MeanConfidence - 0.08f;

            if (koreanScore >= primaryScore + 4d || hasMeaningfulHangulImprovement)
            {
                return koreanResult;
            }

            return primary;
        }

        private static bool ShouldTryKoreanRetry(PreviewOcrRecognition primary)
        {
            if (!primary.HasText)
            {
                return true;
            }

            var stats = AnalyzeText(primary.Text);
            if (stats.HangulCount > 0)
            {
                return true;
            }

            if (stats.KanaCount > 0 || stats.ArabicCount > 0 || stats.CyrillicCount > 0)
            {
                return false;
            }

            if (primary.MeanConfidence < KoreanAutoRetryConfidenceThreshold)
            {
                return true;
            }

            if (stats.HanCount > 0)
            {
                return false;
            }

            return primary.MeanConfidence < KoreanAutoRetryLatinFallbackThreshold &&
                   stats.HangulCount == 0 &&
                   stats.HanCount == 0 &&
                   stats.KanaCount == 0 &&
                   stats.ArabicCount == 0 &&
                   stats.CyrillicCount == 0 &&
                   stats.LatinCount > 0;
        }

        private static bool ShouldTryChineseRetry(PreviewOcrRecognition primary)
        {
            if (!primary.HasText)
            {
                return true;
            }

            var stats = AnalyzeText(primary.Text);
            if (stats.HangulCount > 0 || stats.KanaCount > 0 || stats.ArabicCount > 0 || stats.CyrillicCount > 0)
            {
                return false;
            }

            if (stats.HanCount > 0)
            {
                return stats.TraditionalDistinctCount > 0 ||
                       stats.SimplifiedDistinctCount > 0 ||
                       stats.ChineseVariantMixedCount > 0 ||
                       primary.MeanConfidence < ChineseAutoRetryConfidenceThreshold ||
                       stats.LatinCount > 0;
            }

            return primary.MeanConfidence < ChineseAutoRetryConfidenceThreshold && stats.LatinCount > 0;
        }

        private static PreviewOcrRecognition RecognizeOnce(
            byte[] imageBytes,
            PreviewOcrLanguageOption languageOption,
            string tessDataPath,
            PageSegMode pageSegMode)
        {
            var engine = EnsureEngine(languageOption, tessDataPath);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix, pageSegMode);

            var text = NormalizeText(page.GetText());
            var confidence = page.GetMeanConfidence();

            return string.IsNullOrWhiteSpace(text)
                ? PreviewOcrRecognition.Empty(confidence)
                : new PreviewOcrRecognition(text, true, confidence);
        }

        private static TesseractEngine EnsureEngine(PreviewOcrLanguageOption languageOption, string tessDataPath)
        {
            var languageSet = ResolveLanguageSet(languageOption);
            if (EngineCache.TryGetValue(languageSet, out var cachedEngine))
            {
                return cachedEngine;
            }

            var engine = new TesseractEngine(tessDataPath, languageSet, EngineMode.Default);
            engine.DefaultPageSegMode = PageSegMode.Auto;
            engine.SetVariable("preserve_interword_spaces", "1");
            engine.SetVariable("user_defined_dpi", 300);

            EngineCache[languageSet] = engine;
            return engine;
        }

        private static void DisposeSharedEngine()
        {
            foreach (var engine in EngineCache.Values)
            {
                engine.Dispose();
            }

            EngineCache.Clear();
        }

        private static string ResolveLanguageSet(PreviewOcrLanguageOption languageOption)
        {
            if (languageOption == PreviewOcrLanguageOption.Auto)
            {
                return AllSupportedLanguages;
            }

            return LanguageSetMap.TryGetValue(languageOption, out var languageSet)
                ? languageSet
                : AllSupportedLanguages;
        }

        private static PreviewOcrRuntimeStatus InitializeRuntime()
        {
            if (!OperatingSystem.IsWindows())
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    AppContext.BaseDirectory,
                    string.Empty,
                    string.Empty,
                    UnsupportedPlatformErrorCode,
                    "OCR is supported only on Windows.");
            }

            var baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    PathErrorCode,
                    "AppContext.BaseDirectory is empty.");
            }

            var tessDataPath = RuntimeAssetLocator.OcrTessDataDirectory;
            var nativeRuntimePath = Path.Combine(baseDirectory, Environment.Is64BitProcess ? "x64" : "x86");

            if (!Directory.Exists(tessDataPath))
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    tessDataPath,
                    nativeRuntimePath,
                    PathErrorCode,
                    $"OCR language data folder was not found: {tessDataPath}");
            }

            var missingLanguageFiles = RequiredLanguageFiles
                .Where(file => !File.Exists(Path.Combine(tessDataPath, file)))
                .ToArray();

            if (missingLanguageFiles.Length > 0)
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    tessDataPath,
                    nativeRuntimePath,
                    EngineFilesMissingErrorCode,
                    $"OCR language data is incomplete. Missing: {string.Join(", ", missingLanguageFiles)}");
            }

            if (!Directory.Exists(nativeRuntimePath))
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    tessDataPath,
                    nativeRuntimePath,
                    EngineFilesMissingErrorCode,
                    $"OCR native runtime folder was not found: {nativeRuntimePath}");
            }

            var missingNativeFiles = RequiredNativeFiles
                .Where(file => !File.Exists(Path.Combine(nativeRuntimePath, file)))
                .ToArray();

            if (missingNativeFiles.Length > 0)
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    tessDataPath,
                    nativeRuntimePath,
                    EngineFilesMissingErrorCode,
                    $"OCR native runtime is incomplete. Missing: {string.Join(", ", missingNativeFiles)}");
            }

            try
            {
                ConfigureNativeRuntime(baseDirectory, nativeRuntimePath);
            }
            catch (Exception ex)
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    tessDataPath,
                    nativeRuntimePath,
                    InitializationFailedErrorCode,
                    $"Failed to configure OCR native runtime: {ex.Message}");
            }

            return new PreviewOcrRuntimeStatus(true, baseDirectory, tessDataPath, nativeRuntimePath);
        }

        private static void ConfigureNativeRuntime(string baseDirectory, string nativeRuntimePath)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathEntries = currentPath
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!pathEntries.Contains(nativeRuntimePath, StringComparer.OrdinalIgnoreCase))
            {
                var updatedPath = string.IsNullOrWhiteSpace(currentPath)
                    ? nativeRuntimePath
                    : string.Join(Path.PathSeparator, new[] { nativeRuntimePath, currentPath });

                Environment.SetEnvironmentVariable("PATH", updatedPath);
            }

            InteropDotNet.LibraryLoader.Instance.CustomSearchPath = baseDirectory;
        }

        [SupportedOSPlatform("windows")]
        private static byte[]? TryCreateChineseUpscaledImageBytes(byte[] imageBytes)
        {
            return TryCreateUpscaledImageBytes(imageBytes, ChineseUpscaledLongestEdge, "Chinese");
        }

        [SupportedOSPlatform("windows")]
        private static byte[]? TryCreateKoreanUpscaledImageBytes(byte[] imageBytes)
        {
            return TryCreateUpscaledImageBytes(imageBytes, KoreanUpscaledLongestEdge, "Korean");
        }

        [SupportedOSPlatform("windows")]
        private static byte[]? TryCreateUpscaledImageBytes(
            byte[] imageBytes,
            int targetLongestEdge,
            string scenario)
        {
            try
            {
                using var input = new MemoryStream(imageBytes, writable: false);
                using var sourceImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);

                var longestEdge = Math.Max(sourceImage.Width, sourceImage.Height);
                if (longestEdge <= 0)
                {
                    return null;
                }

                var scale = Math.Max(1d, targetLongestEdge / (double)longestEdge);
                if (scale <= 1.05d)
                {
                    return null;
                }

                var targetWidth = Math.Max(1, (int)Math.Round(sourceImage.Width * scale));
                var targetHeight = Math.Max(1, (int)Math.Round(sourceImage.Height * scale));

                using var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
                bitmap.SetResolution(300, 300);

                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.White);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                graphics.DrawImage(sourceImage, new Rectangle(0, 0, targetWidth, targetHeight));

                using var output = new MemoryStream();
                bitmap.Save(output, DrawingImageFormat.Png);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to prepare {scenario} OCR upscale variant: {ex.Message}");
                return null;
            }
        }

        private static string ConvertTraditionalToSimplified(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                builder.Append(TraditionalToSimplifiedMap.TryGetValue(ch, out var simplified)
                    ? simplified
                    : ch);
            }

            return builder.ToString();
        }

        private static double ScoreRecognition(
            PreviewOcrRecognition result,
            PreviewOcrLanguageOption expectedLanguage)
        {
            if (!result.HasText)
            {
                return double.NegativeInfinity;
            }

            var stats = AnalyzeText(result.Text);
            var score = result.MeanConfidence * 100d;

            if (expectedLanguage == PreviewOcrLanguageOption.SimplifiedChinese)
            {
                score += stats.HanCount > 0
                    ? 18d + (stats.HanCount * 0.45d)
                    : -28d;
                score += stats.SimplifiedDistinctCount * 3d;
                score -= stats.TraditionalDistinctCount * 4d;
            }
            else if (expectedLanguage == PreviewOcrLanguageOption.TraditionalChinese)
            {
                score += stats.HanCount > 0
                    ? 18d + (stats.HanCount * 0.45d)
                    : -28d;
                score += stats.TraditionalDistinctCount * 3d;
                score -= stats.SimplifiedDistinctCount * 4d;
            }
            else if (expectedLanguage == PreviewOcrLanguageOption.Korean)
            {
                score += stats.HangulCount > 0
                    ? 22d + (stats.HangulCount * 0.8d)
                    : -28d;

                if (stats.LatinCount > 0 && stats.HangulCount == 0)
                {
                    score -= Math.Min(12d, stats.LatinCount * 0.45d);
                }
            }
            else if (expectedLanguage == PreviewOcrLanguageOption.Auto)
            {
                if (stats.HangulCount > 0)
                {
                    score += 10d + (stats.HangulCount * 0.5d);
                }

                if (stats.HanCount > 0)
                {
                    score += 8d + (stats.HanCount * 0.25d);
                    score -= stats.ChineseVariantMixedCount * 2.5d;
                }
            }

            if (stats.LatinCount > 0 &&
                expectedLanguage is PreviewOcrLanguageOption.SimplifiedChinese or PreviewOcrLanguageOption.TraditionalChinese &&
                stats.HanCount == 0)
            {
                score -= Math.Min(12d, stats.LatinCount * 0.45d);
            }

            return score;
        }

        private static TextScriptStats AnalyzeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TextScriptStats();
            }

            var stats = new TextScriptStats();
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case >= '\uAC00' and <= '\uD7AF':
                        stats.HangulCount++;
                        break;
                    case >= '\u4E00' and <= '\u9FFF':
                        stats.HanCount++;
                        if (TraditionalDistinctChars.Contains(ch))
                        {
                            stats.TraditionalDistinctCount++;
                        }

                        if (SimplifiedDistinctChars.Contains(ch))
                        {
                            stats.SimplifiedDistinctCount++;
                        }

                        break;
                    case >= '\u3040' and <= '\u30FF':
                        stats.KanaCount++;
                        break;
                    case >= '\u0400' and <= '\u04FF':
                        stats.CyrillicCount++;
                        break;
                    case >= '\u0600' and <= '\u06FF':
                        stats.ArabicCount++;
                        break;
                    default:
                        if ((ch is >= 'A' and <= 'Z') || (ch is >= 'a' and <= 'z'))
                        {
                            stats.LatinCount++;
                        }

                        break;
                }
            }

            stats.ChineseVariantMixedCount = Math.Min(
                stats.TraditionalDistinctCount,
                stats.SimplifiedDistinctCount);
            return stats;
        }

        private static string NormalizeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            var builder = new StringBuilder(normalized.Length);
            var previousLineWasBlank = false;

            foreach (var line in normalized.Split('\n'))
            {
                var trimmedLine = line.TrimEnd();
                var isBlank = string.IsNullOrWhiteSpace(trimmedLine);

                if (isBlank)
                {
                    if (previousLineWasBlank)
                    {
                        continue;
                    }

                    previousLineWasBlank = true;
                    builder.AppendLine();
                    continue;
                }

                previousLineWasBlank = false;
                builder.AppendLine(trimmedLine);
            }

            return builder.ToString().Trim();
        }

        private sealed record PreviewOcrRuntimeStatus(
            bool IsReady,
            string BaseDirectory,
            string TessDataPath,
            string NativeRuntimePath,
            string? ErrorCode = null,
            string? DiagnosticMessage = null);

        private sealed record PreviewOcrLanguageProfile(
            PreviewOcrLanguageOption Option,
            string[] DataCodes);

        private sealed record ChineseVariantRecognition(
            PreviewOcrRecognition Simplified,
            PreviewOcrRecognition Traditional);

        private sealed record RecognitionAttempt(
            string Description,
            PreviewOcrRecognition Result);

        private sealed class TextScriptStats
        {
            public int HangulCount { get; set; }

            public int HanCount { get; set; }

            public int KanaCount { get; set; }

            public int CyrillicCount { get; set; }

            public int ArabicCount { get; set; }

            public int LatinCount { get; set; }

            public int TraditionalDistinctCount { get; set; }

            public int SimplifiedDistinctCount { get; set; }

            public int ChineseVariantMixedCount { get; set; }
        }
    }
}
