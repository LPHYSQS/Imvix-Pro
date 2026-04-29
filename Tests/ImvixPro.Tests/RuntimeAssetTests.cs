using ImvixPro.AI.Matting.Inference;
using ImvixPro.AI.Matting.Models;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.IO;

namespace ImvixPro.Tests;

public sealed class RuntimeAssetTests
{
    [Fact]
    public void OfflineRuntimeAssets_ContainCorePortableDependencies()
    {
        var root = GetProjectRoot();

        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "AI", "Enhancement", "engine", "realesrgan-ncnn-vulkan.exe")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "AI", "Enhancement", "engine", "vcomp140.dll")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ch_PP-OCRv5_mobile_det.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ch_ppocr_mobile_v2.0_cls_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ch_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "en_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_en_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "latin_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_latin_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "korean_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_korean_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "eslav_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_eslav_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "arabic_PP-OCRv5_rec_mobile_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_arabic_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "th_PP-OCRv5_mobile_rec_infer.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "Paddle", "v5", "ppocrv5_th_dict.txt")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Qr", "configs", "decoder.json")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Barcode", "configs", "decoder.json")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "AI", "Matting", "models", "MODNet", "model.onnx")));
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "AI", "Matting", "models", "U2Net", "model.onnx")));
    }

    [Theory]
    [InlineData(AiEnhancementModel.General, 4)]
    [InlineData(AiEnhancementModel.Anime, 4)]
    [InlineData(AiEnhancementModel.Lightweight, 4)]
    [InlineData(AiEnhancementModel.UpscaylStandard, 4)]
    [InlineData(AiEnhancementModel.UpscaylLite, 4)]
    [InlineData(AiEnhancementModel.UpscaylHighFidelity, 4)]
    [InlineData(AiEnhancementModel.UpscaylDigitalArt, 4)]
    public void AiEnhancementCatalog_ResolvesBundledOfflineModels(AiEnhancementModel model, int scale)
    {
        var modelsDirectory = Path.Combine(GetProjectRoot(), "RuntimeAssets", "AI", "Enhancement", "models");

        var resolved = AiEnhancementModelCatalog.TryResolveModelSelection(
            modelsDirectory,
            model,
            scale,
            out var selection);

        Assert.True(resolved);
        var paramFiles = Directory.EnumerateFiles(
                selection.ResolvedModel.ModelDirectoryPath,
                $"{selection.ResolvedModel.ModelNameArgument}*.param",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        var binFiles = Directory.EnumerateFiles(
                selection.ResolvedModel.ModelDirectoryPath,
                $"{selection.ResolvedModel.ModelNameArgument}*.bin",
                SearchOption.TopDirectoryOnly)
            .ToArray();

        Assert.NotEmpty(paramFiles);
        Assert.NotEmpty(binFiles);
    }

    [Fact]
    public void RetiredAiEnhancementModels_AreRemovedFromBundledRuntimeAssets()
    {
        var modelsDirectory = Path.Combine(GetProjectRoot(), "RuntimeAssets", "AI", "Enhancement", "models", "SCENE-TUNED");

        Assert.False(Directory.Exists(Path.Combine(modelsDirectory, "NATURAL-DETAIL-NC-4X")));
        Assert.False(Directory.Exists(Path.Combine(modelsDirectory, "NATURAL-BALANCED-NC-4X")));
        Assert.False(Directory.Exists(Path.Combine(modelsDirectory, "EXTRA-SHARP-NC-4X")));
    }

    [Theory]
    [InlineData(AiEnhancementModel.UpscaylRemacri)]
    [InlineData(AiEnhancementModel.UpscaylUltramix)]
    [InlineData(AiEnhancementModel.UpscaylUltrasharp)]
    public void RetiredAiEnhancementModels_NormalizeToCommercialFallback(AiEnhancementModel retiredModel)
    {
        Assert.True(AiEnhancementModelCatalog.IsRetiredModel(retiredModel));
        Assert.Equal(AiEnhancementModel.UpscaylStandard, AiEnhancementModelCatalog.NormalizeSelectableModel(retiredModel));
    }

    [Fact]
    public void AiEnhancementCatalog_MigratesLegacyNamesToFriendlyNames()
    {
        var modelsDirectory = Path.Combine(Path.GetTempPath(), $"ImvixPro-AiModels-{Guid.NewGuid():N}");
        Directory.CreateDirectory(modelsDirectory);

        try
        {
            CreateModelPair(modelsDirectory, "realesrgan-x4plus");

            var legacyUpscaylDirectory = Path.Combine(modelsDirectory, "UPSCAYL", "UPSCAYL-STANDARD-4X");
            Directory.CreateDirectory(legacyUpscaylDirectory);
            CreateModelPair(legacyUpscaylDirectory, "upscayl-standard-4x");

            Assert.True(AiEnhancementModelCatalog.TryResolveModelSelection(
                modelsDirectory,
                AiEnhancementModel.General,
                4,
                out var legacyGeneralSelection));
            Assert.Equal("realesrgan-x4plus", legacyGeneralSelection.ResolvedModel.ModelNameArgument);

            Assert.True(AiEnhancementModelCatalog.TryResolveModelSelection(
                modelsDirectory,
                AiEnhancementModel.UpscaylStandard,
                4,
                out var legacyUpscaylSelection));
            Assert.Equal("upscayl-standard-4x", legacyUpscaylSelection.ResolvedModel.ModelNameArgument);

            AiEnhancementModelCatalog.MigrateFriendlyModelNames(modelsDirectory);

            Assert.True(File.Exists(Path.Combine(modelsDirectory, "everyday-photo-4x.param")));
            Assert.True(File.Exists(Path.Combine(modelsDirectory, "everyday-photo-4x.bin")));
            Assert.True(File.Exists(Path.Combine(
                modelsDirectory,
                "SCENE-TUNED",
                "BALANCED-QUALITY-4X",
                "balanced-quality-4x.param")));
            Assert.True(File.Exists(Path.Combine(
                modelsDirectory,
                "SCENE-TUNED",
                "BALANCED-QUALITY-4X",
                "balanced-quality-4x.bin")));
            Assert.False(File.Exists(Path.Combine(modelsDirectory, "realesrgan-x4plus.param")));
            Assert.False(Directory.Exists(Path.Combine(modelsDirectory, "UPSCAYL")));

            Assert.True(AiEnhancementModelCatalog.TryResolveModelSelection(
                modelsDirectory,
                AiEnhancementModel.General,
                4,
                out var migratedGeneralSelection));
            Assert.Equal("everyday-photo-4x", migratedGeneralSelection.ResolvedModel.ModelNameArgument);

            Assert.True(AiEnhancementModelCatalog.TryResolveModelSelection(
                modelsDirectory,
                AiEnhancementModel.UpscaylStandard,
                4,
                out var migratedUpscaylSelection));
            Assert.Equal("balanced-quality-4x", migratedUpscaylSelection.ResolvedModel.ModelNameArgument);
        }
        finally
        {
            if (Directory.Exists(modelsDirectory))
            {
                Directory.Delete(modelsDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(AiMattingModel.GeneralClassic)]
    [InlineData(AiMattingModel.GeneralHighAccuracy)]
    [InlineData(AiMattingModel.Portrait)]
    [InlineData(AiMattingModel.Anime)]
    public void AiMattingCatalog_ResolvesBundledOfflineModels(AiMattingModel model)
    {
        var modelsDirectory = Path.Combine(GetProjectRoot(), "RuntimeAssets", "AI", "Matting", "models");

        var selection = AiMattingModelCatalog.ResolveModelSelection(modelsDirectory, model);

        Assert.True(File.Exists(selection.ModelPath));
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

    private static void CreateModelPair(string directoryPath, string baseName)
    {
        File.WriteAllText(Path.Combine(directoryPath, $"{baseName}.param"), "test");
        File.WriteAllText(Path.Combine(directoryPath, $"{baseName}.bin"), "test");
    }
}
