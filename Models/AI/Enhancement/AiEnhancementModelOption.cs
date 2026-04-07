namespace ImvixPro.Models
{
    public sealed class AiEnhancementModelOption
    {
        public AiEnhancementModelOption(
            AiEnhancementModel value,
            string seriesDisplayName,
            string displayName,
            string description)
        {
            Value = value;
            SeriesDisplayName = seriesDisplayName;
            DisplayName = displayName;
            Description = description;
            ListDisplayName = $"{seriesDisplayName} / {displayName}";
        }

        public AiEnhancementModel Value { get; }

        public string SeriesDisplayName { get; }

        public string DisplayName { get; }

        public string ListDisplayName { get; }

        public string Description { get; }

        public override string ToString() => ListDisplayName;
    }
}
