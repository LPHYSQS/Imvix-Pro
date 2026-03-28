using System;
using System.IO;

namespace ImvixPro.Services
{
    public static class RuntimeAssetLocator
    {
        private const string RuntimeRootFolderName = "runtime";

        public static string RuntimeRootDirectory => CombineFromBase(RuntimeRootFolderName);

        public static string OcrTessDataDirectory => CombineFromRuntime("ocr", "tessdata");

        public static string QrConfigFilePath => CombineFromRuntime("qr", "configs", "decoder.json");

        public static string BarcodeConfigFilePath => CombineFromRuntime("barcode", "configs", "decoder.json");

        public static string AiEnhancementRootDirectory => CombineFromRuntime("ai", "enhancement");

        public static string AiEnhancementEngineDirectory => CombineFromRuntime("ai", "enhancement", "engine");

        public static string AiEnhancementModelsDirectory => CombineFromRuntime("ai", "enhancement", "models");

        public static string AiEnhancementExecutablePath => CombineFromRuntime("ai", "enhancement", "engine", "realesrgan-ncnn-vulkan.exe");

        public static string AiMattingModelsDirectory => CombineFromRuntime("ai", "matting", "models");

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
