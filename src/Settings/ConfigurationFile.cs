using System.Collections.Generic;

namespace TailwindCSSIntellisense.Settings;
public class ConfigurationFile
{
    public string Path { get; set; }
    public bool IsDefault { get; set; }
    public List<string> ApplicableLocations { get; set; }
}
