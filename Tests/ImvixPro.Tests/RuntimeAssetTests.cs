using ImvixPro.AI.Matting.Inference;
using ImvixPro.AI.Matting.Models;
using ImvixPro.Models;
using ImvixPro.Services;
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
        Assert.True(File.Exists(Path.Combine(root, "RuntimeAssets", "Ocr", "tessdata", "eng.traineddata")));
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
}
