using ImvixPro.AI.Inpainting.Models;
using ImvixPro.AI.Matting.Models;

namespace ImvixPro.Models
{
    public sealed class PreviewToolState
    {
        public AiMattingModel AiMattingModel { get; set; } = AiMattingModel.GeneralClassic;

        public AiMattingDevice AiMattingDevice { get; set; } = AiMattingDevice.Cpu;

        public OutputImageFormat AiMattingOutputFormat { get; set; } = OutputImageFormat.Png;

        public AiMattingBackgroundMode AiMattingBackgroundMode { get; set; } = AiMattingBackgroundMode.Transparent;

        public string AiMattingBackgroundColor { get; set; } = "#FFFFFFFF";

        public bool AiMattingEdgeOptimizationEnabled { get; set; } = true;

        public int AiMattingEdgeOptimizationStrength { get; set; } = 35;

        public AiMattingResolutionMode AiMattingResolutionMode { get; set; } = AiMattingResolutionMode.Original;

        public int AiEraserDefaultBrushSize { get; set; } = AiEraserSettings.DefaultBrushSize;

        public int AiEraserMaskExpansionPixels { get; set; } = AiEraserSettings.DefaultMaskExpansionPixels;

        public int AiEraserEdgeBlendStrength { get; set; } = AiEraserSettings.DefaultEdgeBlendStrength;

        public PreviewToolState Clone()
        {
            return new PreviewToolState
            {
                AiMattingModel = AiMattingModel,
                AiMattingDevice = AiMattingDevice,
                AiMattingOutputFormat = AiMattingOutputFormat,
                AiMattingBackgroundMode = AiMattingBackgroundMode,
                AiMattingBackgroundColor = AiMattingBackgroundColor,
                AiMattingEdgeOptimizationEnabled = AiMattingEdgeOptimizationEnabled,
                AiMattingEdgeOptimizationStrength = AiMattingEdgeOptimizationStrength,
                AiMattingResolutionMode = AiMattingResolutionMode,
                AiEraserDefaultBrushSize = AiEraserDefaultBrushSize,
                AiEraserMaskExpansionPixels = AiEraserMaskExpansionPixels,
                AiEraserEdgeBlendStrength = AiEraserEdgeBlendStrength
            };
        }
    }
}
