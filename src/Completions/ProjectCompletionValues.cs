using System.Collections.Generic;
using System.Linq;

namespace TailwindCSSIntellisense.Completions;
public class ProjectCompletionValues
{
    /// <summary>
    /// When set, set to lowercase
    /// </summary>
    public string FilePath { get; set; }
    public List<string> ApplicablePaths { get; set; } = [];

    internal bool Initialized { get; set; }
    internal List<TailwindClass> Classes { get; set; } = [];
    internal List<string> Modifiers { get; set; } = [];
    internal List<string> Screen { get; set; } = ["sm", "md", "lg", "xl", "2xl"];

    internal string Prefix { get; set; }

    internal Dictionary<string, string> ColorToRgbMapper { get; set; } = [];
    internal Dictionary<string, string> SpacingMapper { get; set; } = [];
    internal Dictionary<string, List<string>> ConfigurationValueToClassStems { get; set; } = [];

    internal Dictionary<string, Dictionary<string, string>> CustomColorMappers { get; set; } = [];
    internal Dictionary<string, Dictionary<string, string>> CustomSpacingMappers { get; set; } = [];

    internal Dictionary<string, string> DescriptionMapper { get; set; } = [];
    internal Dictionary<string, string> CustomDescriptionMapper { get; set; } = [];

    internal List<string> PluginClasses { get; set; } = [];
    internal List<string> PluginModifiers { get; set; } = [];

    internal HashSet<string> Blocklist { get; set; } = [];
#warning test
    internal List<string> AllowedCorePlugins { get; set; } = [];

    /// <summary>
    /// Is the class in the blocklist?
    /// </summary>
    /// <param name="className">The class to check</param>
    public bool IsClassAllowed(string className)
    {
        return !Blocklist.Contains(className);
    }

    public ProjectCompletionValues Copy()
    {
        return new ProjectCompletionValues
        {
            Initialized = Initialized,
            Classes = Classes?.Select(c => c).ToList(),
            Modifiers = Modifiers?.ToList(),
            Screen = Screen?.ToList(),
            Prefix = Prefix,
            ColorToRgbMapper = new Dictionary<string, string>(ColorToRgbMapper),
            SpacingMapper = new Dictionary<string, string>(SpacingMapper),
            ConfigurationValueToClassStems = ConfigurationValueToClassStems?.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value.ToList()
            ),
            CustomColorMappers = CustomColorMappers?.ToDictionary(
                kvp => kvp.Key, kvp => new Dictionary<string, string>(kvp.Value)
            ),
            CustomSpacingMappers = CustomSpacingMappers?.ToDictionary(
                kvp => kvp.Key, kvp => new Dictionary<string, string>(kvp.Value)
            ),
            DescriptionMapper = new Dictionary<string, string>(DescriptionMapper),
            CustomDescriptionMapper = new Dictionary<string, string>(CustomDescriptionMapper),
            PluginClasses = PluginClasses?.ToList(),
            PluginModifiers = PluginModifiers?.ToList(),
            Blocklist = new HashSet<string>(Blocklist),
            AllowedCorePlugins = AllowedCorePlugins?.ToList()
        };
    }
}