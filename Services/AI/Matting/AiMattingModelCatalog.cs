using ImvixPro.AI.Matting.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace ImvixPro.AI.Matting.Inference
{
    public static class AiMattingModelCatalog
    {
        private static readonly AiMattingModelDefinition[] Definitions =
        [
            new(
                AiMattingModel.GeneralClassic,
                "AiMattingModel_GeneralClassic",
                "AiMattingModelDescription_GeneralClassic",
                Path.Combine("U2Net", "model.onnx"),
                320,
                320,
                true,
                true,
                [0.485f, 0.456f, 0.406f],
                [0.229f, 0.224f, 0.225f],
                false),
            new(
                AiMattingModel.GeneralHighAccuracy,
                "AiMattingModel_GeneralHighAccuracy",
                "AiMattingModelDescription_GeneralHighAccuracy",
                Path.Combine("ISNet", "model.onnx"),
                1024,
                1024,
                false,
                false,
                [0.5f, 0.5f, 0.5f],
                [1f, 1f, 1f],
                false),
            new(
                AiMattingModel.Portrait,
                "AiMattingModel_Portrait",
                "AiMattingModelDescription_Portrait",
                Path.Combine("MODNet", "model.onnx"),
                256,
                256,
                false,
                false,
                [0.5f, 0.5f, 0.5f],
                [0.5f, 0.5f, 0.5f],
                false),
            new(
                AiMattingModel.Anime,
                "AiMattingModel_Anime",
                "AiMattingModelDescription_Anime",
                Path.Combine("AnimeSeg", "model.onnx"),
                1024,
                1024,
                false,
                false,
                [0.5f, 0.5f, 0.5f],
                [1f, 1f, 1f],
                true)
        ];

        public static IReadOnlyList<AiMattingModelDefinition> GetDefinitions() => Definitions;

        public static string BuildDisplayName(AiMattingModel model, Func<string, string> translator)
        {
            foreach (var definition in Definitions)
            {
                if (definition.Model == model)
                {
                    return translator(definition.DisplayTranslationKey);
                }
            }

            return model.ToString();
        }

        public static ResolvedAiMattingModelSelection ResolveModelSelection(string modelsDirectory, AiMattingModel requestedModel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);

            AiMattingModelDefinition? requestedDefinition = null;
            AiMattingModelDefinition? fallbackDefinition = null;

            foreach (var definition in Definitions)
            {
                if (definition.Model == requestedModel)
                {
                    requestedDefinition = definition;
                }

                if (fallbackDefinition is null && definition.Model == AiMattingModel.GeneralClassic)
                {
                    fallbackDefinition = definition;
                }
            }

            requestedDefinition ??= fallbackDefinition ?? throw new InvalidOperationException("No AI matting model definition is available.");
            fallbackDefinition ??= requestedDefinition;

            var requestedPath = Path.Combine(modelsDirectory, requestedDefinition.RelativeModelPath);
            if (File.Exists(requestedPath))
            {
                return new ResolvedAiMattingModelSelection(requestedDefinition, requestedPath, false);
            }

            var fallbackPath = Path.Combine(modelsDirectory, fallbackDefinition.RelativeModelPath);
            if (!File.Exists(fallbackPath))
            {
                throw new FileNotFoundException("No local AI matting model was found.", fallbackPath);
            }

            return new ResolvedAiMattingModelSelection(
                fallbackDefinition,
                fallbackPath,
                requestedDefinition.Model != fallbackDefinition.Model);
        }

        public sealed record AiMattingModelDefinition(
            AiMattingModel Model,
            string DisplayTranslationKey,
            string DescriptionTranslationKey,
            string RelativeModelPath,
            int DefaultInputWidth,
            int DefaultInputHeight,
            bool KeepAspectRatio,
            bool PadToInputSize,
            float[] Mean,
            float[] Std,
            bool IsOptional);

        public sealed record ResolvedAiMattingModelSelection(
            AiMattingModelDefinition Definition,
            string ModelPath,
            bool UsedFallback);
    }
}

