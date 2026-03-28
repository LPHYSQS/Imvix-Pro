using ImvixPro.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var installedFonts = new HashSet<string>(
    new InstalledFontCollection()
        .Families
        .Select(family => family.Name),
    StringComparer.OrdinalIgnoreCase);
var traditionalDistinctChinese = new HashSet<char>("\u8B58\u5225\u6E2C\u8A66\u7C21\u9AD4\u958B\u767C\u9801");

Font CreateFont(string[] preferredFamilies, float size)
{
    var familyName = preferredFamilies.FirstOrDefault(name => installedFonts.Contains(name)) ?? "Arial";
    return new Font(familyName, size, FontStyle.Bold, GraphicsUnit.Pixel);
}

byte[] CreateSampleImage(string[] preferredFamilies, params string[] lines)
{
    using var bitmap = new Bitmap(1480, 420);
    using var graphics = Graphics.FromImage(bitmap);
    using var background = new SolidBrush(Color.White);
    using var foreground = new SolidBrush(Color.Black);
    using var font = CreateFont(preferredFamilies, 88);

    graphics.Clear(Color.White);
    graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

    for (var index = 0; index < lines.Length; index++)
    {
        graphics.DrawString(lines[index], font, foreground, new PointF(42, 42 + (index * 152)));
    }

    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

byte[] CreateChallengingKoreanImage()
{
    using var bitmap = new Bitmap(1120, 320);
    using var graphics = Graphics.FromImage(bitmap);
    using var background = new SolidBrush(Color.FromArgb(248, 248, 248));
    using var foreground = new SolidBrush(Color.FromArgb(82, 82, 82));
    using var accent = new Pen(Color.FromArgb(224, 224, 224), 2f);
    using var titleFont = CreateFont(["Malgun Gothic", "Segoe UI"], 54);
    using var subtitleFont = CreateFont(["Malgun Gothic", "Segoe UI"], 46);

    graphics.Clear(Color.White);
    graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    graphics.DrawRectangle(accent, 18, 18, bitmap.Width - 36, bitmap.Height - 36);
    graphics.DrawString("\uD55C\uAD6D\uC5B4 \uBB38\uC7A5 \uC778\uC2DD", titleFont, foreground, new PointF(56, 58));
    graphics.DrawString("OCR \uD488\uC9C8 \uAC1C\uC120 \uD14C\uC2A4\uD2B8", subtitleFont, foreground, new PointF(56, 168));

    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

byte[] CreateChallengingSimplifiedChineseImage()
{
    using var bitmap = new Bitmap(1180, 320);
    using var graphics = Graphics.FromImage(bitmap);
    using var background = new SolidBrush(Color.FromArgb(249, 249, 249));
    using var foreground = new SolidBrush(Color.FromArgb(76, 76, 76));
    using var accent = new Pen(Color.FromArgb(228, 228, 228), 2f);
    using var titleFont = CreateFont(["Microsoft YaHei UI", "Microsoft JhengHei UI"], 54);
    using var subtitleFont = CreateFont(["Microsoft YaHei UI", "Microsoft JhengHei UI"], 46);

    graphics.Clear(Color.White);
    graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    graphics.DrawRectangle(accent, 18, 18, bitmap.Width - 36, bitmap.Height - 36);
    graphics.DrawString("\u7B80\u4F53\u8BC6\u522B\u6D4B\u8BD5", titleFont, foreground, new PointF(56, 58));
    graphics.DrawString("OCR \u5F00\u53D1\u9875\u9762\u5B57\u4F53", subtitleFont, foreground, new PointF(56, 168));

    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

string CanonicalText(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return string.Empty;
    }

    var normalized = text.Normalize(NormalizationForm.FormD);
    var builder = new StringBuilder(normalized.Length);

    foreach (var ch in normalized)
    {
        if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
        {
            continue;
        }

        if (char.IsLetterOrDigit(ch))
        {
            builder.Append(char.ToUpperInvariant(ch));
        }
    }

    return builder.ToString();
}

bool ContainsAllTokens(string? text, params string[] tokens)
{
    var canonical = CanonicalText(text);
    return tokens.All(token => canonical.Contains(CanonicalText(token), StringComparison.Ordinal));
}

bool ContainsInRange(string? text, int start, int end)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    return text.Any(ch => ch >= start && ch <= end);
}

bool ContainsHan(string? text) => ContainsInRange(text, 0x4E00, 0x9FFF);
bool ContainsJapaneseKana(string? text) =>
    ContainsInRange(text, 0x3040, 0x309F) || ContainsInRange(text, 0x30A0, 0x30FF);
bool ContainsHangul(string? text) => ContainsInRange(text, 0xAC00, 0xD7AF);
bool ContainsCyrillic(string? text) => ContainsInRange(text, 0x0400, 0x04FF);
bool ContainsArabic(string? text) => ContainsInRange(text, 0x0600, 0x06FF);

bool ContainsTraditionalDistinctChinese(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    return text.Any(traditionalDistinctChinese.Contains);
}

bool LooksLikeSimplifiedChinese(string? text, params string[] tokens)
{
    return ContainsAllTokens(text, tokens) && !ContainsTraditionalDistinctChinese(text);
}

async Task<int> VerifyCaseAsync(PreviewOcrService service, OcrCase testCase)
{
    var imageBytes = testCase.ImageFactory?.Invoke()
        ?? CreateSampleImage(testCase.PreferredFamilies, testCase.Lines);
    var autoResult = await service.RecognizeAsync(imageBytes, PreviewOcrLanguageOption.Auto);
    var exactResult = await service.RecognizeAsync(imageBytes, testCase.ExactOption);
    var autoPassed = testCase.Validator(autoResult.Text);
    var exactPassed = testCase.Validator(exactResult.Text);

    Console.WriteLine($"== {testCase.Name} ==");
    Console.WriteLine($"Auto   [{autoPassed}] {autoResult.MeanConfidence:0.000}");
    Console.WriteLine(autoResult.Text);
    Console.WriteLine();
    Console.WriteLine($"{testCase.ExactOption} [{exactPassed}] {exactResult.MeanConfidence:0.000}");
    Console.WriteLine(exactResult.Text);

    if (testCase.WrongOption is not null)
    {
        var wrongResult = await service.RecognizeAsync(imageBytes, testCase.WrongOption.Value);
        Console.WriteLine();
        Console.WriteLine($"Wrong {testCase.WrongOption} [{testCase.Validator(wrongResult.Text)}] {wrongResult.MeanConfidence:0.000}");
        Console.WriteLine(wrongResult.Text);
    }

    Console.WriteLine();
    return autoPassed && exactPassed ? 0 : 1;
}

var cases = new[]
{
    new OcrCase(
        "English",
        PreviewOcrLanguageOption.English,
        ["Arial", "Segoe UI"],
        ["HELLO OCR 123", "TEST PREVIEW OCR"],
        text => ContainsAllTokens(text, "HELLO OCR 123", "TEST PREVIEW OCR")),
    new OcrCase(
        "Simplified Chinese",
        PreviewOcrLanguageOption.SimplifiedChinese,
        ["Microsoft YaHei UI", "Microsoft JhengHei UI"],
        ["\u4E2D\u6587\u8BC6\u522B\u6D4B\u8BD5", "\u7B80\u4F53\u4E2D\u6587 OCR"],
        text => LooksLikeSimplifiedChinese(text, "\u4E2D\u6587\u8BC6\u522B\u6D4B\u8BD5", "\u7B80\u4F53\u4E2D\u6587 OCR"),
        PreviewOcrLanguageOption.TraditionalChinese),
    new OcrCase(
        "Simplified Chinese Low Contrast",
        PreviewOcrLanguageOption.SimplifiedChinese,
        ["Microsoft YaHei UI", "Microsoft JhengHei UI"],
        ["\u7B80\u4F53\u8BC6\u522B\u6D4B\u8BD5", "OCR \u5F00\u53D1\u9875\u9762\u5B57\u4F53"],
        text => ContainsHan(text) && !ContainsTraditionalDistinctChinese(text),
        PreviewOcrLanguageOption.TraditionalChinese,
        CreateChallengingSimplifiedChineseImage),
    new OcrCase(
        "Traditional Chinese",
        PreviewOcrLanguageOption.TraditionalChinese,
        ["Microsoft JhengHei UI", "Microsoft JhengHei UI Light", "Microsoft YaHei UI"],
        ["\u4E2D\u6587\u6E2C\u8A66", "\u7E41\u9AD4 OCR"],
        ContainsHan,
        PreviewOcrLanguageOption.English),
    new OcrCase(
        "Japanese",
        PreviewOcrLanguageOption.Japanese,
        ["Yu Gothic UI", "MS UI Gothic"],
        ["\u65E5\u672C\u8A9E\u30C6\u30B9\u30C8", "OCR \u30B5\u30F3\u30D7\u30EB"],
        text => ContainsJapaneseKana(text) || ContainsHan(text),
        PreviewOcrLanguageOption.English),
    new OcrCase(
        "Korean",
        PreviewOcrLanguageOption.Korean,
        ["Malgun Gothic", "Segoe UI"],
        ["\uD55C\uAD6D\uC5B4 \uD14C\uC2A4\uD2B8", "OCR \uC0D8\uD50C"],
        ContainsHangul,
        PreviewOcrLanguageOption.English),
    new OcrCase(
        "Korean Low Contrast",
        PreviewOcrLanguageOption.Korean,
        ["Malgun Gothic", "Segoe UI"],
        ["\uD55C\uAD6D\uC5B4 \uBB38\uC7A5 \uC778\uC2DD", "OCR \uD488\uC9C8 \uAC1C\uC120 \uD14C\uC2A4\uD2B8"],
        ContainsHangul,
        PreviewOcrLanguageOption.English,
        CreateChallengingKoreanImage),
    new OcrCase(
        "German",
        PreviewOcrLanguageOption.German,
        ["Arial", "Segoe UI"],
        ["GUTEN MORGEN", "DEUTSCH OCR"],
        text => ContainsAllTokens(text, "GUTEN MORGEN", "DEUTSCH OCR")),
    new OcrCase(
        "French",
        PreviewOcrLanguageOption.French,
        ["Arial", "Segoe UI"],
        ["FRAN\u00C7AIS OCR", "\u00C9COLE MOD\u00C8LE"],
        text => ContainsAllTokens(text, "FRAN\u00C7AIS OCR", "\u00C9COLE MOD\u00C8LE")),
    new OcrCase(
        "Italian",
        PreviewOcrLanguageOption.Italian,
        ["Arial", "Segoe UI"],
        ["ITALIANO OCR", "CITT\u00C0 MODERNA"],
        text => ContainsAllTokens(text, "ITALIANO OCR", "CITT\u00C0 MODERNA")),
    new OcrCase(
        "Russian",
        PreviewOcrLanguageOption.Russian,
        ["Arial", "Segoe UI"],
        ["\u0420\u0443\u0441\u0441\u043A\u0438\u0439 \u0442\u0435\u0441\u0442", "OCR \u043F\u0440\u0438\u043C\u0435\u0440"],
        ContainsCyrillic,
        PreviewOcrLanguageOption.English),
    new OcrCase(
        "Arabic",
        PreviewOcrLanguageOption.Arabic,
        ["Tahoma", "Segoe UI"],
        ["\u0627\u062E\u062A\u0628\u0627\u0631 \u0639\u0631\u0628\u064A", "OCR \u062A\u062C\u0631\u064A\u0628\u064A"],
        ContainsArabic,
        PreviewOcrLanguageOption.English)
};

var service = new PreviewOcrService();
var failures = 0;

foreach (var testCase in cases)
{
    failures += await VerifyCaseAsync(service, testCase);
}

Console.WriteLine($"Failures={failures}");
return failures == 0 ? 0 : 1;

sealed record OcrCase(
    string Name,
    PreviewOcrLanguageOption ExactOption,
    string[] PreferredFamilies,
    string[] Lines,
    Func<string, bool> Validator,
    PreviewOcrLanguageOption? WrongOption = null,
    Func<byte[]>? ImageFactory = null);