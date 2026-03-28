using Avalonia;
using ImvixPro.AI.Matting.Models;

namespace ImvixPro.AI.Matting.Inference
{
    public sealed record AiMattingResult(
        string WorkingDirectory,
        string ResultPath,
        PixelSize SourceSize,
        PixelSize PreviewSize,
        double AverageAlphaPercent,
        double ForegroundCoveragePercent,
        AiMattingModel EffectiveModel,
        bool UsedModelFallback,
        AiMattingDevice EffectiveDevice,
        bool UsedGpuFallback);
}
