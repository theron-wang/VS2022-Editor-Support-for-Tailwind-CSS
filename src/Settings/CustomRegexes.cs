using System.Collections.Generic;

namespace TailwindCSSIntellisense.Settings;
public class CustomRegexes
{
    public CustomRegex Razor { get; set; }
    public CustomRegex HTML { get; set; }
    public CustomRegex JavaScript { get; set; }

    public class CustomRegex
    {
        public bool Override { get; set; }
        public List<string> Values { get; set; }
    }
}
