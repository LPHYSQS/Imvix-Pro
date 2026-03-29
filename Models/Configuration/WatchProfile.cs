namespace ImvixPro.Models
{
    public sealed class WatchProfile
    {
        public bool IsEnabled { get; set; }

        public string InputDirectory { get; set; } = string.Empty;

        public string OutputDirectory { get; set; } = string.Empty;

        public bool IncludeSubfolders { get; set; } = true;

        public ConversionJobDefinition JobDefinition { get; set; } = new();

        public WatchProfile Clone()
        {
            return new WatchProfile
            {
                IsEnabled = IsEnabled,
                InputDirectory = InputDirectory,
                OutputDirectory = OutputDirectory,
                IncludeSubfolders = IncludeSubfolders,
                JobDefinition = JobDefinition?.Clone() ?? new ConversionJobDefinition()
            };
        }
    }
}
