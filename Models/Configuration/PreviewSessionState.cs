namespace ImvixPro.Models
{
    public sealed class PreviewSessionState
    {
        public ConversionJobDefinition JobDefinition { get; set; } = new();

        public PreviewToolState PreviewToolState { get; set; } = new();
    }
}
