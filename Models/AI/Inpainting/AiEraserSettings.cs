using System;

namespace ImvixPro.AI.Inpainting.Models
{
    public static class AiEraserSettings
    {
        public const int MinBrushSize = 8;
        public const int MaxBrushSize = 160;
        public const int DefaultBrushSize = 36;

        public const int MinMaskExpansionPixels = 0;
        public const int MaxMaskExpansionPixels = 48;
        public const int DefaultMaskExpansionPixels = 12;

        public const int MinEdgeBlendStrength = 0;
        public const int MaxEdgeBlendStrength = 100;
        public const int DefaultEdgeBlendStrength = 35;

        public static int NormalizeBrushSize(int value)
        {
            return Math.Clamp(value, MinBrushSize, MaxBrushSize);
        }

        public static int NormalizeMaskExpansionPixels(int value)
        {
            return Math.Clamp(value, MinMaskExpansionPixels, MaxMaskExpansionPixels);
        }

        public static int NormalizeEdgeBlendStrength(int value)
        {
            return Math.Clamp(value, MinEdgeBlendStrength, MaxEdgeBlendStrength);
        }
    }
}
