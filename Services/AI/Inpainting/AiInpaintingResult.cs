using Avalonia;

namespace ImvixPro.AI.Inpainting.Inference
{
    public sealed record AiInpaintingResult(
        string WorkingDirectory,
        string ResultPath,
        PixelSize SourceSize,
        double MaskCoveragePercent);
}
