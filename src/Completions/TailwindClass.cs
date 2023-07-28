namespace TailwindCSSIntellisense.Completions
{
    internal class TailwindClass
    {
        public string Name { get; set; }

        public bool UseColors { get; set; }

        public bool UseOpacity { get; set; }

        public bool UseSpacing { get; set; }

        public bool SupportsBrackets { get; set; }

        public TailwindClass Clone()
        {
            return new TailwindClass()
            {
                Name = Name,
                UseColors = UseColors,
                UseOpacity = UseOpacity,
                UseSpacing = UseSpacing,
                SupportsBrackets = SupportsBrackets
            };
        }
    }
}
