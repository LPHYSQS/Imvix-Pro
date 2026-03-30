using System;
using System.IO;

namespace ImvixPro.Services
{
    public static class RuntimeAssetLocator
    {
        private const string RuntimeRootFolderName = "runtime";

        public static string RuntimeRootDirectory => CombineFromBase(RuntimeRootFolderName);

        public static string OcrRootDirectory => CombineFromRuntime("ocr");

        public static string OcrPaddleRootDirectory => CombineFromRuntime("ocr", "paddle");

        public static string OcrPaddleV5Directory => CombineFromRuntime("ocr", "paddle", "v5");

        public static string QrConfigFilePath => CombineFromRuntime("qr", "configs", "decoder.json");

        public static string BarcodeConfigFilePath => CombineFromRuntime("barcode", "configs", "decoder.json");

        public static string AiEnhancementRootDirectory => CombineFromRuntime("ai", "enhancement");

        public static string AiEnhancementEngineDirectory => CombineFromRuntime("ai", "enhancement", "engine");

        public static string AiEnhancementModelsDirectory => CombineFromRuntime("ai", "enhancement", "models");

        public static string AiEnhancementExecutablePath => CombineFromRuntime("ai", "enhancement", "engine", "realesrgan-ncnn-vulkan.exe");

        public static string AiMattingModelsDirectory => CombineFromRuntime("ai", "matting", "models");

        public static string AiInpaintingModelsDirectory => CombineFromRuntime("ai", "inpainting", "models");

        public static string AiInpaintingLaMaDirectory => Path.Combine(AiInpaintingModelsDirectory, "LaMa");

        public static string AiInpaintingLaMaModelPath
        {
            get
            {
                var runtimePath = Path.Combine(AiInpaintingLaMaDirectory, "lama.onnx");
                if (File.Exists(runtimePath))
                {
                    return runtimePath;
                }

                var legacyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppIdentity.DisplayName,
                    "Models",
                    "AI",
                    "Inpainting",
                    "LaMa",
                    "lama.onnx");

                return File.Exists(legacyPath)
                    ? legacyPath
                    : runtimePath;
            }
        }

        private static string CombineFromRuntime(params string[] segments)
        {
            return CombineFromBase([RuntimeRootFolderName, .. segments]);
        }

        private static string CombineFromBase(params string[] segments)
        {
            return Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, .. segments]));
        }
    }
}
