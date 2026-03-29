using System;

namespace ImvixPro.Models
{
    public sealed class ConversionJobDefinition
    {
        public string Name { get; set; } = string.Empty;

        public ConversionOptions Options { get; set; } = new();

        public ConversionJobDefinition Clone()
        {
            return new ConversionJobDefinition
            {
                Name = Name,
                Options = Options?.Clone() ?? new ConversionOptions()
            };
        }

        public ConversionOptions ToConversionOptions()
        {
            return Options?.Clone() ?? new ConversionOptions();
        }
    }
}
