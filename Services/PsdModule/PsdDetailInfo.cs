using System.Collections.Generic;

namespace ImvixPro.Services.PsdModule
{
    public sealed record PsdDetailInfo(
        int Width,
        int Height,
        int? BitDepth,
        string? ColorMode,
        double? DpiX,
        double? DpiY,
        int? ChannelCount,
        bool? HasAlphaChannel,
        bool? SupportsTransparency,
        int? LayerCount,
        int? VisibleLayerCount,
        int? HiddenLayerCount,
        int? LayerGroupCount,
        int? MaxLayerWidth,
        int? MaxLayerHeight,
        bool? HasTransparencyChannel,
        int LockedLayerCount,
        int TextLayerCount,
        int ImageLayerCount,
        int ShapeLayerCount,
        IReadOnlyList<string> LayerNameSamples,
        string? IccProfileName,
        string? ColorSpace,
        bool? HasEmbeddedColorProfile,
        bool? HasMergedImage,
        string? CompatibilityVersion,
        bool? IsCompressed,
        string? Compression,
        bool? HasTextLayers,
        bool? HasSmartObjects,
        bool? HasVectorPaths);
}
