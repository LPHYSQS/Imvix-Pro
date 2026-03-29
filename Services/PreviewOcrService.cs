using RapidOcrNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private static readonly IReadOnlyDictionary<char, char> TraditionalToSimplifiedMap =
            new Dictionary<char, char>
            {
                ['识'] = '识',
                ['識'] = '识',
                ['別'] = '别',
                ['测'] = '测',
                ['測'] = '测',
                ['试'] = '试',
                ['試'] = '试',
                ['简'] = '简',
                ['簡'] = '简',
                ['体'] = '体',
                ['體'] = '体',
                ['开'] = '开',
                ['開'] = '开',
                ['关'] = '关',
                ['關'] = '关',
                ['发'] = '发',
                ['發'] = '发',
                ['现'] = '现',
                ['現'] = '现',
                ['图'] = '图',
                ['圖'] = '图',
                ['层'] = '层',
                ['層'] = '层',
                ['页'] = '页',
                ['頁'] = '页',
                ['网'] = '网',
                ['網'] = '网',
                ['点'] = '点',
                ['點'] = '点',
                ['录'] = '录',
                ['錄'] = '录',
                ['门'] = '门',
                ['門'] = '门',
                ['书'] = '书',
                ['書'] = '书',
                ['龙'] = '龙',
                ['龍'] = '龙',
                ['画'] = '画',
                ['畫'] = '画',
                ['云'] = '云',
                ['雲'] = '云',
                ['台'] = '台',
                ['臺'] = '台',
                ['档'] = '档',
                ['檔'] = '档',
                ['证'] = '证',
                ['證'] = '证',
                ['码'] = '码',
                ['碼'] = '码',
                ['错'] = '错',
                ['錯'] = '错',
                ['误'] = '误',
                ['誤'] = '误',
                ['预'] = '预',
                ['預'] = '预',
                ['览'] = '览',
                ['覽'] = '览',
                ['选'] = '选',
                ['選'] = '选',
                ['择'] = '择',
                ['擇'] = '择',
                ['资'] = '资',
                ['資'] = '资',
                ['讯'] = '讯',
                ['訊'] = '讯',
                ['显'] = '显',
                ['顯'] = '显',
                ['处'] = '处',
                ['處'] = '处',
                ['线'] = '线',
                ['線'] = '线',
                ['编'] = '编',
                ['編'] = '编',
                ['辑'] = '辑',
                ['輯'] = '辑',
                ['复'] = '复',
                ['復'] = '复',
                ['检'] = '检',
                ['檢'] = '检',
                ['权'] = '权',
                ['權'] = '权',
                ['转'] = '转',
                ['轉'] = '转',
                ['换'] = '换',
                ['換'] = '换',
                ['压'] = '压',
                ['壓'] = '压',
                ['缩'] = '缩',
                ['縮'] = '缩',
                ['报'] = '报',
                ['報'] = '报',
                ['导'] = '导',
                ['導'] = '导',
                ['输'] = '输',
                ['輸'] = '输',
                ['双'] = '双',
                ['雙'] = '双',
                ['击'] = '击',
                ['擊'] = '击',
                ['视'] = '视',
                ['視'] = '视',
                ['专'] = '专',
                ['專'] = '专',
                ['业'] = '业',
                ['業'] = '业',
                ['标'] = '标',
                ['標'] = '标',
                ['题'] = '题',
                ['題'] = '题',
                ['内'] = '内',
                ['內'] = '内',
                ['响'] = '响',
                ['響'] = '响',
                ['应'] = '应',
                ['應'] = '应',
                ['键'] = '键',
                ['鍵'] = '键',
                ['盘'] = '盘',
                ['盤'] = '盘',
                ['风'] = '风',
                ['風'] = '风',
                ['级'] = '级',
                ['級'] = '级',
                ['优'] = '优',
                ['優'] = '优',
                ['归'] = '归',
                ['歸'] = '归',
                ['经'] = '经',
                ['經'] = '经',
                ['验'] = '验',
                ['驗'] = '验',
                ['数'] = '数',
                ['數'] = '数',
                ['据'] = '据',
                ['據'] = '据',
                ['总'] = '总',
                ['總'] = '总',
                ['结'] = '结',
                ['結'] = '结',
                ['实'] = '实',
                ['實'] = '实',
                ['际'] = '际',
                ['際'] = '际',
                ['讨'] = '讨',
                ['討'] = '讨',
                ['论'] = '论',
                ['論'] = '论',
                ['与'] = '与',
                ['與'] = '与',
                ['为'] = '为',
                ['為'] = '为',
                ['时'] = '时',
                ['時'] = '时',
                ['间'] = '间',
                ['間'] = '间',
                ['纯'] = '纯',
                ['純'] = '纯',
                ['吗'] = '吗',
                ['嗎'] = '吗',
                ['这'] = '这',
                ['這'] = '这',
                ['个'] = '个',
                ['個'] = '个',
                ['会'] = '会',
                ['會'] = '会',
                ['让'] = '让',
                ['讓'] = '让',
                ['后'] = '后',
                ['後'] = '后'
            };

        private static readonly HashSet<char> TraditionalDistinctChars = TraditionalToSimplifiedMap
            .Where(pair => pair.Key != pair.Value)
            .Select(pair => pair.Key)
            .ToHashSet();

        private static readonly HashSet<char> SimplifiedDistinctChars = TraditionalToSimplifiedMap.Values.ToHashSet();

        private static readonly PreviewOcrLanguageProfile[] SupportedLanguageProfiles =
        [
            new(PreviewOcrLanguageOption.SimplifiedChinese),
            new(PreviewOcrLanguageOption.TraditionalChinese),
            new(PreviewOcrLanguageOption.English),
            new(PreviewOcrLanguageOption.Japanese),
            new(PreviewOcrLanguageOption.Korean),
            new(PreviewOcrLanguageOption.German),
            new(PreviewOcrLanguageOption.French),
            new(PreviewOcrLanguageOption.Italian),
            new(PreviewOcrLanguageOption.Russian),
            new(PreviewOcrLanguageOption.Arabic)
        ];

        private static readonly RapidOcrOptions DefaultOcrOptions = RapidOcrOptions.Default with
        {
            ImgResize = 1600,
            Padding = 40
        };

        private static readonly PreviewPaddleOcrProfile ChineseProfile = new(
            "ppocrv5-ch",
            "ch_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_dict.txt",
            PreviewOcrAutoFamily.Chinese,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile EnglishProfile = new(
            "ppocrv5-en",
            "en_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_en_dict.txt",
            PreviewOcrAutoFamily.English,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile LatinProfile = new(
            "ppocrv5-latin",
            "latin_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_latin_dict.txt",
            PreviewOcrAutoFamily.Latin,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile KoreanProfile = new(
            "ppocrv5-korean",
            "korean_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_korean_dict.txt",
            PreviewOcrAutoFamily.Korean,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile EastSlavicProfile = new(
            "ppocrv5-eslav",
            "eslav_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_eslav_dict.txt",
            PreviewOcrAutoFamily.Cyrillic,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile ArabicProfile = new(
            "ppocrv5-arabic",
            "arabic_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_arabic_dict.txt",
            PreviewOcrAutoFamily.Arabic,
            DefaultOcrOptions);

        private static readonly PreviewPaddleOcrProfile[] AutoProfiles =
        [
            ChineseProfile,
            EnglishProfile,
            LatinProfile,
            KoreanProfile,
            EastSlavicProfile,
            ArabicProfile
        ];

        private static readonly IReadOnlyDictionary<PreviewOcrLanguageOption, PreviewPaddleOcrProfile> LanguageProfileMap =
            new Dictionary<PreviewOcrLanguageOption, PreviewPaddleOcrProfile>
            {
                [PreviewOcrLanguageOption.SimplifiedChinese] = ChineseProfile,
                [PreviewOcrLanguageOption.TraditionalChinese] = ChineseProfile,
                [PreviewOcrLanguageOption.English] = EnglishProfile,
                [PreviewOcrLanguageOption.Japanese] = ChineseProfile,
                [PreviewOcrLanguageOption.Korean] = KoreanProfile,
                [PreviewOcrLanguageOption.German] = LatinProfile,
                [PreviewOcrLanguageOption.French] = LatinProfile,
                [PreviewOcrLanguageOption.Italian] = LatinProfile,
                [PreviewOcrLanguageOption.Russian] = EastSlavicProfile,
                [PreviewOcrLanguageOption.Arabic] = ArabicProfile
            };

        private static readonly string[] RequiredOcrFiles =
        [
            "ch_PP-OCRv5_mobile_det.onnx",
            "ch_ppocr_mobile_v2.0_cls_infer.onnx",
            "ch_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_dict.txt",
            "en_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_en_dict.txt",
            "latin_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_latin_dict.txt",
            "korean_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_korean_dict.txt",
            "eslav_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_eslav_dict.txt",
            "arabic_PP-OCRv5_rec_mobile_infer.onnx",
            "ppocrv5_arabic_dict.txt"
        ];

        private static readonly SemaphoreSlim EngineGate = new(1, 1);
        private static readonly Dictionary<string, PreviewPaddleOcrEngineAdapter> EngineCache = new(StringComparer.Ordinal);
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
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeSharedEngines();
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
                await EngineGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return RecognizeCore(imageBytes, languageOption, runtimeStatus.ModelsDirectory);
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
            }, cancellationToken).ConfigureAwait(false);
        }

        private static PreviewOcrRecognition RecognizeCore(
            byte[] imageBytes,
            PreviewOcrLanguageOption languageOption,
            string modelsDirectory)
        {
            if (!OperatingSystem.IsWindows())
            {
                return PreviewOcrRecognition.Error(UnsupportedPlatformErrorCode);
            }

            if (languageOption == PreviewOcrLanguageOption.Auto)
            {
                return RecognizeAuto(imageBytes, modelsDirectory);
            }

            if (!LanguageProfileMap.TryGetValue(languageOption, out var profile))
            {
                return RecognizeAuto(imageBytes, modelsDirectory);
            }

            return NormalizeRecognition(
                RecognizeWithProfile(imageBytes, profile, modelsDirectory),
                languageOption);
        }

        private static PreviewOcrRecognition RecognizeAuto(byte[] imageBytes, string modelsDirectory)
        {
            var attempts = AutoProfiles
                .Select(profile => new RecognitionAttempt(profile, RecognizeWithProfile(imageBytes, profile, modelsDirectory)))
                .ToArray();

            return attempts
                .OrderByDescending(attempt => ScoreAutoRecognition(attempt.Profile, attempt.Result))
                .ThenByDescending(attempt => attempt.Result.MeanConfidence)
                .First()
                .Result;
        }

        private static PreviewOcrRecognition RecognizeWithProfile(
            byte[] imageBytes,
            PreviewPaddleOcrProfile profile,
            string modelsDirectory)
        {
            var engine = EnsureEngine(profile, modelsDirectory);
            var rawResult = engine.Recognize(imageBytes);
            var text = NormalizeText(rawResult.Text);

            return string.IsNullOrWhiteSpace(text)
                ? PreviewOcrRecognition.Empty(rawResult.MeanConfidence)
                : new PreviewOcrRecognition(text, true, rawResult.MeanConfidence);
        }

        private static PreviewPaddleOcrEngineAdapter EnsureEngine(
            PreviewPaddleOcrProfile profile,
            string modelsDirectory)
        {
            if (EngineCache.TryGetValue(profile.CacheKey, out var cachedEngine))
            {
                return cachedEngine;
            }

            var detPath = Path.Combine(modelsDirectory, "ch_PP-OCRv5_mobile_det.onnx");
            var clsPath = Path.Combine(modelsDirectory, "ch_ppocr_mobile_v2.0_cls_infer.onnx");
            var recPath = Path.Combine(modelsDirectory, profile.RecognitionModelFile);
            var keysPath = Path.Combine(modelsDirectory, profile.DictionaryFile);

            var engine = new PreviewPaddleOcrEngineAdapter(
                detPath,
                clsPath,
                recPath,
                keysPath,
                profile.Options);

            EngineCache[profile.CacheKey] = engine;
            return engine;
        }

        private static void DisposeSharedEngines()
        {
            foreach (var engine in EngineCache.Values)
            {
                engine.Dispose();
            }

            EngineCache.Clear();
        }

        private static PreviewOcrRuntimeStatus InitializeRuntime()
        {
            if (!OperatingSystem.IsWindows())
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    AppContext.BaseDirectory,
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
                    PathErrorCode,
                    "AppContext.BaseDirectory is empty.");
            }

            var modelsDirectory = RuntimeAssetLocator.OcrPaddleV5Directory;
            if (!Directory.Exists(modelsDirectory))
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    modelsDirectory,
                    PathErrorCode,
                    $"OCR model folder was not found: {modelsDirectory}");
            }

            var missingFiles = RequiredOcrFiles
                .Where(file => !File.Exists(Path.Combine(modelsDirectory, file)))
                .ToArray();

            if (missingFiles.Length > 0)
            {
                return new PreviewOcrRuntimeStatus(
                    false,
                    baseDirectory,
                    modelsDirectory,
                    EngineFilesMissingErrorCode,
                    $"OCR model files are incomplete. Missing: {string.Join(", ", missingFiles)}");
            }

            return new PreviewOcrRuntimeStatus(true, baseDirectory, modelsDirectory);
        }

        private static PreviewOcrRecognition NormalizeRecognition(
            PreviewOcrRecognition recognition,
            PreviewOcrLanguageOption languageOption)
        {
            if (!recognition.HasText)
            {
                return recognition;
            }

            if (languageOption != PreviewOcrLanguageOption.SimplifiedChinese)
            {
                return recognition;
            }

            var normalizedText = ConvertTraditionalToSimplified(recognition.Text);
            return normalizedText == recognition.Text
                ? recognition
                : recognition with { Text = normalizedText };
        }

        private static double ScoreAutoRecognition(
            PreviewPaddleOcrProfile profile,
            PreviewOcrRecognition result)
        {
            if (!result.HasText)
            {
                return double.NegativeInfinity;
            }

            var stats = AnalyzeText(result.Text);
            var score = result.MeanConfidence * 100d;
            score += Math.Min(12d, stats.VisibleCharacterCount * 0.35d);

            switch (profile.AutoFamily)
            {
                case PreviewOcrAutoFamily.Chinese:
                    if (stats.KanaCount > 0)
                    {
                        score += 22d + (stats.KanaCount * 0.8d);
                    }

                    if (stats.HanCount > 0)
                    {
                        score += 18d + (stats.HanCount * 0.35d);
                        score -= stats.ChineseVariantMixedCount * 1.5d;
                    }

                    if (stats.KanaCount == 0 && stats.HanCount == 0 && stats.LatinCount > 0)
                    {
                        score += 6d + (stats.LatinCount * 0.12d);
                    }

                    score -= (stats.HangulCount * 1.6d) + (stats.CyrillicCount * 1.2d) + (stats.ArabicCount * 1.2d);
                    break;

                case PreviewOcrAutoFamily.English:
                    score += stats.LatinCount > 0
                        ? 18d + (stats.LatinCount * 0.3d)
                        : -28d;
                    score -= stats.HanCount + stats.KanaCount + (stats.HangulCount * 1.5d) + (stats.CyrillicCount * 1.4d) + (stats.ArabicCount * 1.4d);
                    score -= stats.LatinDiacriticCount * 0.35d;
                    if (stats.LatinCount is > 0 and < 4)
                    {
                        score -= 6d;
                    }
                    break;

                case PreviewOcrAutoFamily.Latin:
                    score += stats.LatinCount > 0
                        ? 18d + (stats.LatinCount * 0.32d)
                        : -28d;
                    score += stats.LatinDiacriticCount > 0
                        ? 6d + (stats.LatinDiacriticCount * 0.5d)
                        : 0d;
                    score -= stats.HanCount + stats.KanaCount + (stats.HangulCount * 1.5d) + (stats.CyrillicCount * 1.25d) + (stats.ArabicCount * 1.25d);
                    break;

                case PreviewOcrAutoFamily.Korean:
                    score += stats.HangulCount > 0
                        ? 24d + (stats.HangulCount * 0.9d)
                        : -30d;
                    score -= (stats.HanCount * 0.5d) + (stats.KanaCount * 1.5d) + (stats.CyrillicCount * 1.2d) + (stats.ArabicCount * 1.2d);
                    break;

                case PreviewOcrAutoFamily.Cyrillic:
                    score += stats.CyrillicCount > 0
                        ? 30d + (stats.CyrillicCount * 1.05d)
                        : -30d;
                    score -= stats.HanCount + stats.KanaCount + (stats.HangulCount * 1.5d) + (stats.ArabicCount * 1.25d);
                    break;

                case PreviewOcrAutoFamily.Arabic:
                    score += stats.ArabicCount > 0
                        ? 34d + (stats.ArabicCount * 1.15d)
                        : -30d;
                    score -= stats.HanCount + stats.KanaCount + (stats.HangulCount * 1.5d) + (stats.CyrillicCount * 1.25d);
                    break;
            }

            return score;
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

        private static TextScriptStats AnalyzeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TextScriptStats();
            }

            var stats = new TextScriptStats();
            foreach (var ch in text)
            {
                if (!char.IsWhiteSpace(ch) && !char.IsControl(ch))
                {
                    stats.VisibleCharacterCount++;
                }

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
                    case >= '\u0750' and <= '\u077F':
                    case >= '\u08A0' and <= '\u08FF':
                        stats.ArabicCount++;
                        break;
                    default:
                        if (IsLatinChar(ch))
                        {
                            stats.LatinCount++;
                            if (IsLatinDiacriticChar(ch))
                            {
                                stats.LatinDiacriticCount++;
                            }
                        }

                        break;
                }
            }

            stats.ChineseVariantMixedCount = Math.Min(
                stats.TraditionalDistinctCount,
                stats.SimplifiedDistinctCount);

            return stats;
        }

        private static bool IsLatinChar(char ch)
        {
            return ch is >= 'A' and <= 'Z'
                || ch is >= 'a' and <= 'z'
                || ch is >= '\u00C0' and <= '\u024F'
                || ch is >= '\u1E00' and <= '\u1EFF';
        }

        private static bool IsLatinDiacriticChar(char ch)
        {
            return ch is >= '\u00C0' and <= '\u024F'
                || ch is >= '\u1E00' and <= '\u1EFF';
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
            string ModelsDirectory,
            string? ErrorCode = null,
            string? DiagnosticMessage = null);

        private sealed record PreviewOcrLanguageProfile(
            PreviewOcrLanguageOption Option);

        private sealed record PreviewPaddleOcrProfile(
            string CacheKey,
            string RecognitionModelFile,
            string DictionaryFile,
            PreviewOcrAutoFamily AutoFamily,
            RapidOcrOptions Options);

        private sealed record RecognitionAttempt(
            PreviewPaddleOcrProfile Profile,
            PreviewOcrRecognition Result);

        private sealed class TextScriptStats
        {
            public int HangulCount { get; set; }

            public int HanCount { get; set; }

            public int KanaCount { get; set; }

            public int CyrillicCount { get; set; }

            public int ArabicCount { get; set; }

            public int LatinCount { get; set; }

            public int LatinDiacriticCount { get; set; }

            public int VisibleCharacterCount { get; set; }

            public int TraditionalDistinctCount { get; set; }

            public int SimplifiedDistinctCount { get; set; }

            public int ChineseVariantMixedCount { get; set; }
        }

        private enum PreviewOcrAutoFamily
        {
            Chinese,
            English,
            Latin,
            Korean,
            Cyrillic,
            Arabic
        }
    }
}
