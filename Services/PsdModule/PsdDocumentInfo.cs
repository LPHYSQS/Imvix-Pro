namespace ImvixPro.Services.PsdModule
{
    public sealed record PsdDocumentInfo(
        int Width,
        int Height,
        bool HasTransparency,
        int? BitDepth,
        string? ColorMode);
}
