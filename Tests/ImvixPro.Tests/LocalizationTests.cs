using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ImvixPro.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void LocalizationFiles_AreValidJson_AndMatchEnglishKeySet()
    {
        var root = GetProjectRoot();
        var localizationDirectory = Path.Combine(root, "Assets", "Localization");
        var baseFilePath = Path.Combine(localizationDirectory, "en-US.json");

        var baseLocalization = LoadLocalization(baseFilePath);
        var expectedKeys = new HashSet<string>(baseLocalization.Keys, StringComparer.Ordinal);

        foreach (var filePath in Directory.EnumerateFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var localization = LoadLocalization(filePath);
            var localeName = Path.GetFileName(filePath);

            var missingKeys = expectedKeys.Except(localization.Keys, StringComparer.Ordinal).ToArray();
            var extraKeys = localization.Keys.Except(expectedKeys, StringComparer.Ordinal).ToArray();

            Assert.True(
                missingKeys.Length == 0,
                $"{localeName} is missing {missingKeys.Length} localization keys: {string.Join(", ", missingKeys)}");

            Assert.True(
                extraKeys.Length == 0,
                $"{localeName} contains {extraKeys.Length} unexpected localization keys: {string.Join(", ", extraKeys)}");
        }
    }

    [Theory]
    [InlineData("vi-VN.json")]
    [InlineData("th-TH.json")]
    public void VietnameseAndThai_AiSurfaceStrings_DoNotFallBackToEnglish(string localeFileName)
    {
        var root = GetProjectRoot();
        var localizationDirectory = Path.Combine(root, "Assets", "Localization");
        var english = LoadLocalization(Path.Combine(localizationDirectory, "en-US.json"));
        var localized = LoadLocalization(Path.Combine(localizationDirectory, localeFileName));

        var sentinelKeys = new[]
        {
            "AiEnhancementTab",
            "AiEnhancementDescription",
            "PreviewAiBusy",
            "PreviewAiCompareHint",
            "AiMattingPanelTitle",
            "AiMattingEdgeOptimization",
            "PreviewMattingButton",
            "PreviewEraserCompareHint",
            "AI_ERASER"
        };

        foreach (var key in sentinelKeys)
        {
            Assert.True(localized.TryGetValue(key, out var localizedValue), $"{localeFileName} is missing AI localization key '{key}'.");
            Assert.False(string.IsNullOrWhiteSpace(localizedValue), $"{localeFileName} AI localization key '{key}' is empty.");
            Assert.NotEqual(english[key], localizedValue);
        }
    }

    private static Dictionary<string, string> LoadLocalization(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var localization = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        Assert.NotNull(localization);
        return localization!;
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Imvix Pro.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Imvix Pro project root.");
    }
}
