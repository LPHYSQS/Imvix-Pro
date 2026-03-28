using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImvixPro.Services
{
    public static class AiEnhancementModelCatalog
    {
        public const string DefaultRealEsrganModelName = "realesrgan-x4plus";
        public const int MinRequestedOutputScale = 2;
        public const int MaxRequestedOutputScale = 16;
        public const int FixedInferenceScale = 4;
        public const int DoublePassOutputScale = FixedInferenceScale * FixedInferenceScale;

        private const string AnimeRealEsrganModelName = "realesrgan-x4plus-anime";
        private const string LightweightRealEsrganModelName = "realesr-general-x4v3";
        private const string LightweightAnimeVideoModelAlias = "realesr-animevideov3";
        private const string UpscaylRootFolderName = "UPSCAYL";
        private const string LegacyUpscaylRootFolderName = "Upscayl";

        public static IReadOnlyList<AiEnhancementModelDefinition> Definitions { get; } =
        [
            new(AiEnhancementModel.General, "AiModelSeries_RealEsrgan", "AiModel_General", "AiModelDescription_General"),
            new(AiEnhancementModel.Anime, "AiModelSeries_RealEsrgan", "AiModel_Anime", "AiModelDescription_Anime"),
            new(AiEnhancementModel.Lightweight, "AiModelSeries_RealEsrgan", "AiModel_Lightweight", "AiModelDescription_Lightweight"),
            new(AiEnhancementModel.UpscaylStandard, "AiModelSeries_Upscayl", "AiModel_UpscaylStandard", "AiModelDescription_UpscaylStandard"),
            new(AiEnhancementModel.UpscaylLite, "AiModelSeries_Upscayl", "AiModel_UpscaylLite", "AiModelDescription_UpscaylLite"),
            new(AiEnhancementModel.UpscaylHighFidelity, "AiModelSeries_Upscayl", "AiModel_UpscaylHighFidelity", "AiModelDescription_UpscaylHighFidelity"),
            new(AiEnhancementModel.UpscaylDigitalArt, "AiModelSeries_Upscayl", "AiModel_UpscaylDigitalArt", "AiModelDescription_UpscaylDigitalArt"),
            new(AiEnhancementModel.UpscaylRemacri, "AiModelSeries_Upscayl", "AiModel_UpscaylRemacri", "AiModelDescription_UpscaylRemacri", isCommercialUseRestricted: true),
            new(AiEnhancementModel.UpscaylUltramix, "AiModelSeries_Upscayl", "AiModel_UpscaylUltramix", "AiModelDescription_UpscaylUltramix", isCommercialUseRestricted: true),
            new(AiEnhancementModel.UpscaylUltrasharp, "AiModelSeries_Upscayl", "AiModel_UpscaylUltrasharp", "AiModelDescription_UpscaylUltrasharp", isCommercialUseRestricted: true)
        ];

        public static AiEnhancementModelDefinition GetDefinition(AiEnhancementModel model)
        {
            return Definitions.First(definition => definition.Model == model);
        }

        public static int NormalizeRequestedOutputScale(int requestedScale)
        {
            return Math.Clamp(requestedScale, MinRequestedOutputScale, MaxRequestedOutputScale);
        }

        public static int ResolveInferenceScale(int requestedScale)
        {
            _ = NormalizeRequestedOutputScale(requestedScale);
            return FixedInferenceScale;
        }

        public static bool RequiresSecondPass(int requestedScale)
        {
            return NormalizeRequestedOutputScale(requestedScale) > FixedInferenceScale;
        }

        public static int ResolveRawOutputScale(int requestedScale)
        {
            return RequiresSecondPass(requestedScale)
                ? DoublePassOutputScale
                : FixedInferenceScale;
        }

        public static string BuildListDisplayName(AiEnhancementModel model, Func<string, string> translate)
        {
            var definition = GetDefinition(model);
            return $"{translate(definition.SeriesTranslationKey)} / {translate(definition.DisplayTranslationKey)}";
        }

        public static bool TryResolveDefaultModel(string modelsDirectory, int requestedOutputScale, out ResolvedAiModel resolvedModel)
        {
            return TryResolveModel(
                modelsDirectory,
                AiEnhancementModel.General,
                ResolveInferenceScale(requestedOutputScale),
                out resolvedModel);
        }

        public static bool TryResolveModelSelection(
            string modelsDirectory,
            AiEnhancementModel selectedModel,
            int requestedOutputScale,
            out ResolvedAiModelSelection resolvedSelection)
        {
            var inferenceScale = ResolveInferenceScale(requestedOutputScale);
            if (TryResolveModel(modelsDirectory, selectedModel, inferenceScale, out var resolvedModel))
            {
                resolvedSelection = new ResolvedAiModelSelection(selectedModel, selectedModel, resolvedModel, false);
                return true;
            }

            if (selectedModel != AiEnhancementModel.General &&
                TryResolveModel(modelsDirectory, AiEnhancementModel.General, inferenceScale, out var defaultModel))
            {
                resolvedSelection = new ResolvedAiModelSelection(selectedModel, AiEnhancementModel.General, defaultModel, true);
                return true;
            }

            resolvedSelection = default;
            return false;
        }

        private static bool TryResolveModel(
            string modelsDirectory,
            AiEnhancementModel model,
            int inferenceScale,
            out ResolvedAiModel resolvedModel)
        {
            switch (model)
            {
                case AiEnhancementModel.General:
                    return TryResolveFlatModel(modelsDirectory, DefaultRealEsrganModelName, out resolvedModel);
                case AiEnhancementModel.Anime:
                    return TryResolveFlatModel(modelsDirectory, AnimeRealEsrganModelName, out resolvedModel);
                case AiEnhancementModel.Lightweight:
                    if (TryResolveFlatModel(modelsDirectory, LightweightRealEsrganModelName, out resolvedModel))
                    {
                        return true;
                    }

                    var lightweightVariantName = $"{LightweightAnimeVideoModelAlias}-x{Math.Clamp(inferenceScale, 2, 4)}";
                    if (HasModelPair(modelsDirectory, lightweightVariantName))
                    {
                        resolvedModel = new ResolvedAiModel(modelsDirectory, LightweightAnimeVideoModelAlias);
                        return true;
                    }

                    break;
                case AiEnhancementModel.UpscaylStandard:
                    return TryResolveUpscaylModel(modelsDirectory, ["UPSCAYL-STANDARD-4X"], ["upscayl-standard-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylLite:
                    return TryResolveUpscaylModel(modelsDirectory, ["UPSCAYL-LITE-4X"], ["upscayl-lite-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylHighFidelity:
                    return TryResolveUpscaylModel(modelsDirectory, ["HIGH-FIDELITY-4X"], ["high-fidelity-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylDigitalArt:
                    return TryResolveUpscaylModel(modelsDirectory, ["DIGITAL-ART"], ["digital-art-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylRemacri:
                    return TryResolveUpscaylModel(modelsDirectory, ["REMACRI-4X"], ["remacri-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylUltramix:
                    return TryResolveUpscaylModel(modelsDirectory, ["ULTRAMIX-BALANCED-4X"], ["ultramix-balanced-4x"], out resolvedModel);
                case AiEnhancementModel.UpscaylUltrasharp:
                    return TryResolveUpscaylModel(modelsDirectory, ["ULTRASHARP-4X"], ["ultrasharp-4x"], out resolvedModel);
            }

            resolvedModel = default;
            return false;
        }

        private static bool TryResolveFlatModel(string directoryPath, string modelName, out ResolvedAiModel resolvedModel)
        {
            if (HasModelPair(directoryPath, modelName))
            {
                resolvedModel = new ResolvedAiModel(directoryPath, modelName);
                return true;
            }

            resolvedModel = default;
            return false;
        }

        private static bool TryResolveUpscaylModel(
            string modelsDirectory,
            IReadOnlyList<string> candidateDirectoryNames,
            IReadOnlyList<string> preferredBaseNames,
            out ResolvedAiModel resolvedModel)
        {
            foreach (var rootDirectory in EnumerateUpscaylRoots(modelsDirectory))
            {
                foreach (var directoryName in candidateDirectoryNames)
                {
                    var candidateDirectory = Path.Combine(rootDirectory, directoryName);
                    if (TryResolveModelPairInDirectory(candidateDirectory, preferredBaseNames, out var modelName))
                    {
                        resolvedModel = new ResolvedAiModel(candidateDirectory, modelName);
                        return true;
                    }
                }

                if (TryResolveModelPairInDirectory(rootDirectory, preferredBaseNames, out var flatModelName))
                {
                    resolvedModel = new ResolvedAiModel(rootDirectory, flatModelName);
                    return true;
                }
            }

            resolvedModel = default;
            return false;
        }

        private static IEnumerable<string> EnumerateUpscaylRoots(string modelsDirectory)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folderName in new[] { UpscaylRootFolderName, LegacyUpscaylRootFolderName })
            {
                var candidatePath = Path.Combine(modelsDirectory, folderName);
                if (!Directory.Exists(candidatePath))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(candidatePath);
                if (seen.Add(fullPath))
                {
                    yield return fullPath;
                }
            }
        }

        private static bool TryResolveModelPairInDirectory(
            string directoryPath,
            IReadOnlyList<string> preferredBaseNames,
            out string resolvedModelName)
        {
            if (!Directory.Exists(directoryPath))
            {
                resolvedModelName = string.Empty;
                return false;
            }

            foreach (var preferredBaseName in preferredBaseNames)
            {
                if (HasModelPair(directoryPath, preferredBaseName))
                {
                    resolvedModelName = preferredBaseName;
                    return true;
                }
            }

            var discoveredPairs = Directory.EnumerateFiles(directoryPath, "*.param", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                // Upscayl packages stay ncnn-compatible, but their .param/.bin base names
                // do not have to mirror the parent folder name.
                .Where(name => HasModelPair(directoryPath, name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (discoveredPairs.Length == 1)
            {
                resolvedModelName = discoveredPairs[0];
                return true;
            }

            foreach (var preferredBaseName in preferredBaseNames)
            {
                var matched = discoveredPairs.FirstOrDefault(name =>
                    name.Contains(preferredBaseName, StringComparison.OrdinalIgnoreCase) ||
                    preferredBaseName.Contains(name, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(matched))
                {
                    resolvedModelName = matched;
                    return true;
                }
            }

            resolvedModelName = string.Empty;
            return false;
        }

        private static bool HasModelPair(string directoryPath, string modelName)
        {
            return File.Exists(Path.Combine(directoryPath, $"{modelName}.param")) &&
                   File.Exists(Path.Combine(directoryPath, $"{modelName}.bin"));
        }
    }

    public sealed class AiEnhancementModelDefinition
    {
        public AiEnhancementModelDefinition(
            AiEnhancementModel model,
            string seriesTranslationKey,
            string displayTranslationKey,
            string descriptionTranslationKey,
            bool isCommercialUseRestricted = false)
        {
            Model = model;
            SeriesTranslationKey = seriesTranslationKey;
            DisplayTranslationKey = displayTranslationKey;
            DescriptionTranslationKey = descriptionTranslationKey;
            IsCommercialUseRestricted = isCommercialUseRestricted;
        }

        public AiEnhancementModel Model { get; }

        public string SeriesTranslationKey { get; }

        public string DisplayTranslationKey { get; }

        public string DescriptionTranslationKey { get; }

        public bool IsCommercialUseRestricted { get; }
    }

    public readonly record struct ResolvedAiModel(string ModelDirectoryPath, string ModelNameArgument);

    public readonly record struct ResolvedAiModelSelection(
        AiEnhancementModel RequestedModel,
        AiEnhancementModel EffectiveModel,
        ResolvedAiModel ResolvedModel,
        bool UsedDefaultFallback);
}
