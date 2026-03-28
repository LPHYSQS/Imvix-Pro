namespace ImvixPro.Models
{
    public sealed class AiEnhancementModelOption
    {
        public AiEnhancementModelOption(
            AiEnhancementModel value,
            string seriesDisplayName,
            string displayName,
            string description,
            bool isCommercialUseRestricted)
        {
            Value = value;
            SeriesDisplayName = seriesDisplayName;
            DisplayName = displayName;
            Description = description;
            IsCommercialUseRestricted = isCommercialUseRestricted;
            ListDisplayName = $"{seriesDisplayName} / {displayName}";
        }

        public AiEnhancementModel Value { get; }

        public string SeriesDisplayName { get; }

        public string DisplayName { get; }

        public string ListDisplayName { get; }

        public string Description { get; }

        public bool IsCommercialUseRestricted { get; }

        public override string ToString() => ListDisplayName;
    }
}
