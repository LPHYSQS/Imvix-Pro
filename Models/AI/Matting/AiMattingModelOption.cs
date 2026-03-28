namespace ImvixPro.AI.Matting.Models
{
    public sealed class AiMattingModelOption
    {
        public AiMattingModelOption(
            AiMattingModel value,
            string displayName,
            string description,
            bool isOptional = false)
        {
            Value = value;
            DisplayName = displayName;
            Description = description;
            IsOptional = isOptional;
        }

        public AiMattingModel Value { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public bool IsOptional { get; }

        public override string ToString() => DisplayName;
    }
}
