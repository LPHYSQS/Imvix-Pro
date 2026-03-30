namespace ImvixPro.AI.Inpainting.Models
{
    public sealed class AiInpaintingOptions
    {
        public int MaskExpansionPixels { get; init; } = AiEraserSettings.DefaultMaskExpansionPixels;

        public int EdgeBlendStrength { get; init; } = AiEraserSettings.DefaultEdgeBlendStrength;
    }
}
