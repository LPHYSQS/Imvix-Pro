using ImvixPro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImvixPro.Services
{
    public static class AiEnhancementModelCatalog
    {
        public const string DefaultRealEsrganModelName = "everyday-photo-4x";
        public const int MinRequestedOutputScale = 2;
        public const int MaxRequestedOutputScale = 16;
        public const int FixedInferenceScale = 4;
        public const int DoublePassOutputScale = FixedInferenceScale * FixedInferenceScale;

        private const string LegacyDefaultRealEsrganModelName = "realesrgan-x4plus";
        private const string AnimeRealEsrganModelName = "anime-illustration-4x";
        private const string LegacyAnimeRealEsrganModelName = "realesrgan-x4plus-anime";
        private const string LightweightRealEsrganModelName = "fast-lightweight-4x";
        private const string LegacyLightweightRealEsrganModelName = "realesr-general-x4v3";
        private const string LightweightAnimeVideoModelAlias = "fast-lightweight";
        private const string LegacyLightweightAnimeVideoModelAlias = "realesr-animevideov3";
        private const string FriendlyUpscaylRootFolderName = "SCENE-TUNED";
        private const string LegacyUpscaylRootFolderNameUpper = "UPSCAYL";
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

        public static void MigrateFriendlyModelNames(string modelsDirectory)
        {
            if (string.IsNullOrWhiteSpace(modelsDirectory) || !Directory.Exists(modelsDirectory))
            {
                return;
            }

            TryRenameModelPair(modelsDirectory, LegacyDefaultRealEsrganModelName, DefaultRealEsrganModelName);
            TryRenameModelPair(modelsDirectory, LegacyAnimeRealEsrganModelName, AnimeRealEsrganModelName);
            TryRenameModelPair(modelsDirectory, LegacyLightweightRealEsrganModelName, LightweightRealEsrganModelName);

            foreach (var scale in new[] { 2, 3, 4 })
            {
                TryRenameModelPair(
                    modelsDirectory,
                    $"{LegacyLightweightAnimeVideoModelAlias}-x{scale}",
                    $"{LightweightAnimeVideoModelAlias}-x{scale}");
            }

            MigrateUpscaylModelNames(modelsDirectory);
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
                    return TryResolveFlatModel(
                        modelsDirectory,
                        [DefaultRealEsrganModelName, LegacyDefaultRealEsrganModelName],
                        out resolvedModel);
                case AiEnhancementModel.Anime:
                    return TryResolveFlatModel(
                        modelsDirectory,
                        [AnimeRealEsrganModelName, LegacyAnimeRealEsrganModelName],
                        out resolvedModel);
                case AiEnhancementModel.Lightweight:
                    if (TryResolveFlatModel(
                            modelsDirectory,
                            [LightweightRealEsrganModelName, LegacyLightweightRealEsrganModelName],
                            out resolvedModel))
                    {
                        return true;
                    }

                    foreach (var lightweightVariantAlias in new[]
                             {
                                 LightweightAnimeVideoModelAlias,
                                 LegacyLightweightAnimeVideoModelAlias
                             })
                    {
                        var lightweightVariantName = $"{lightweightVariantAlias}-x{Math.Clamp(inferenceScale, 2, 4)}";
                        if (HasModelPair(modelsDirectory, lightweightVariantName))
                        {
                            resolvedModel = new ResolvedAiModel(modelsDirectory, lightweightVariantAlias);
                            return true;
                        }
                    }

                    break;
                case AiEnhancementModel.UpscaylStandard:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["BALANCED-QUALITY-4X", "UPSCAYL-STANDARD-4X"],
                        ["balanced-quality-4x", "upscayl-standard-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylLite:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["FAST-QUALITY-4X", "UPSCAYL-LITE-4X"],
                        ["fast-quality-4x", "upscayl-lite-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylHighFidelity:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["DETAIL-PRESERVE-4X", "HIGH-FIDELITY-4X"],
                        ["detail-preserve-4x", "high-fidelity-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylDigitalArt:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["ILLUSTRATION-ART-4X", "DIGITAL-ART"],
                        ["illustration-art-4x", "digital-art-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylRemacri:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["NATURAL-DETAIL-NC-4X", "REMACRI-4X"],
                        ["natural-detail-nc-4x", "remacri-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylUltramix:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["NATURAL-BALANCED-NC-4X", "ULTRAMIX-BALANCED-4X"],
                        ["natural-balanced-nc-4x", "ultramix-balanced-4x"],
                        out resolvedModel);
                case AiEnhancementModel.UpscaylUltrasharp:
                    return TryResolveUpscaylModel(
                        modelsDirectory,
                        ["EXTRA-SHARP-NC-4X", "ULTRASHARP-4X"],
                        ["extra-sharp-nc-4x", "ultrasharp-4x"],
                        out resolvedModel);
            }

            resolvedModel = default;
            return false;
        }

        private static bool TryResolveFlatModel(
            string directoryPath,
            IReadOnlyList<string> candidateModelNames,
            out ResolvedAiModel resolvedModel)
        {
            foreach (var candidateModelName in candidateModelNames)
            {
                if (HasModelPair(directoryPath, candidateModelName))
                {
                    resolvedModel = new ResolvedAiModel(directoryPath, candidateModelName);
                    return true;
                }
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
            foreach (var folderName in new[]
                     {
                         FriendlyUpscaylRootFolderName,
                         LegacyUpscaylRootFolderNameUpper,
                         LegacyUpscaylRootFolderName
                     })
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

        private static void MigrateUpscaylModelNames(string modelsDirectory)
        {
            var rootDirectory = EnsureFriendlyUpscaylRoot(modelsDirectory);
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                return;
            }

            TryRenameUpscaylPackage(
                rootDirectory,
                "UPSCAYL-STANDARD-4X",
                "BALANCED-QUALITY-4X",
                "upscayl-standard-4x",
                "balanced-quality-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "UPSCAYL-LITE-4X",
                "FAST-QUALITY-4X",
                "upscayl-lite-4x",
                "fast-quality-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "HIGH-FIDELITY-4X",
                "DETAIL-PRESERVE-4X",
                "high-fidelity-4x",
                "detail-preserve-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "DIGITAL-ART",
                "ILLUSTRATION-ART-4X",
                "digital-art-4x",
                "illustration-art-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "REMACRI-4X",
                "NATURAL-DETAIL-NC-4X",
                "remacri-4x",
                "natural-detail-nc-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "ULTRAMIX-BALANCED-4X",
                "NATURAL-BALANCED-NC-4X",
                "ultramix-balanced-4x",
                "natural-balanced-nc-4x");
            TryRenameUpscaylPackage(
                rootDirectory,
                "ULTRASHARP-4X",
                "EXTRA-SHARP-NC-4X",
                "ultrasharp-4x",
                "extra-sharp-nc-4x");
        }

        private static string EnsureFriendlyUpscaylRoot(string modelsDirectory)
        {
            var friendlyRootDirectory = Path.Combine(modelsDirectory, FriendlyUpscaylRootFolderName);
            if (Directory.Exists(friendlyRootDirectory))
            {
                return friendlyRootDirectory;
            }

            foreach (var legacyRootFolderName in new[] { LegacyUpscaylRootFolderNameUpper, LegacyUpscaylRootFolderName })
            {
                var legacyRootDirectory = Path.Combine(modelsDirectory, legacyRootFolderName);
                if (!Directory.Exists(legacyRootDirectory))
                {
                    continue;
                }

                TryRenameDirectory(legacyRootDirectory, friendlyRootDirectory);
                return Directory.Exists(friendlyRootDirectory)
                    ? friendlyRootDirectory
                    : legacyRootDirectory;
            }

            return friendlyRootDirectory;
        }

        private static void TryRenameUpscaylPackage(
            string rootDirectory,
            string legacyDirectoryName,
            string friendlyDirectoryName,
            string legacyBaseName,
            string friendlyBaseName)
        {
            var friendlyDirectoryPath = Path.Combine(rootDirectory, friendlyDirectoryName);
            var legacyDirectoryPath = Path.Combine(rootDirectory, legacyDirectoryName);

            TryRenameDirectory(legacyDirectoryPath, friendlyDirectoryPath);

            if (Directory.Exists(friendlyDirectoryPath))
            {
                TryRenameModelPair(friendlyDirectoryPath, legacyBaseName, friendlyBaseName);
                return;
            }

            if (Directory.Exists(legacyDirectoryPath))
            {
                TryRenameModelPair(legacyDirectoryPath, legacyBaseName, friendlyBaseName);
            }
        }

        private static void TryRenameDirectory(string sourceDirectoryPath, string targetDirectoryPath)
        {
            if (!Directory.Exists(sourceDirectoryPath) || Directory.Exists(targetDirectoryPath))
            {
                return;
            }

            var parentDirectory = Path.GetDirectoryName(targetDirectoryPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            try
            {
                Directory.Move(sourceDirectoryPath, targetDirectoryPath);
            }
            catch
            {
                // Keep legacy names in place when best-effort migration cannot complete.
            }
        }

        private static void TryRenameModelPair(string directoryPath, string legacyBaseName, string friendlyBaseName)
        {
            if (string.Equals(legacyBaseName, friendlyBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var legacyParamPath = Path.Combine(directoryPath, $"{legacyBaseName}.param");
            var legacyBinPath = Path.Combine(directoryPath, $"{legacyBaseName}.bin");
            var friendlyParamPath = Path.Combine(directoryPath, $"{friendlyBaseName}.param");
            var friendlyBinPath = Path.Combine(directoryPath, $"{friendlyBaseName}.bin");

            if (!File.Exists(legacyParamPath) ||
                !File.Exists(legacyBinPath) ||
                File.Exists(friendlyParamPath) ||
                File.Exists(friendlyBinPath))
            {
                return;
            }

            var paramMoved = false;
            try
            {
                File.Move(legacyParamPath, friendlyParamPath);
                paramMoved = true;
                File.Move(legacyBinPath, friendlyBinPath);
            }
            catch
            {
                if (paramMoved && File.Exists(friendlyParamPath) && !File.Exists(legacyParamPath))
                {
                    try
                    {
                        File.Move(friendlyParamPath, legacyParamPath);
                    }
                    catch
                    {
                        // Ignore rollback failures; future resolution still checks legacy names.
                    }
                }
            }
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
