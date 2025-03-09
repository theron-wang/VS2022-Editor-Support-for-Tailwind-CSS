using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Media;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Configuration;

public sealed partial class CompletionConfiguration
{
    /// <summary>
    /// Reconfigures colors, spacing, and screen as well as any non-theme properties (prefix, blocklist, etc.)
    /// </summary>
    private void LoadGlobalConfiguration(ProjectCompletionValues project, TailwindConfiguration config)
    {
        var original = _completionBase.GetUnsetCompletionConfiguration(project.Version);

        project.SpacingMapper = original.SpacingMapper.ToDictionary(pair => pair.Key, pair => pair.Value);
        project.Screen = [.. original.Screen];
        project.ColorMapper = original.ColorMapper.ToDictionary(pair => pair.Key, pair => pair.Value);
        ColorIconGenerator.ClearCache(project);
        project.Blocklist = new HashSet<string>(config?.Blocklist ?? []);
        project.CssVariables = original.CssVariables;

        if (config is null)
        {
            // Reset to default; either user has changed/deleted config file or there is none
            return;
        }

        if (config.OverridenValues.ContainsKey("colors") && GetDictionary(config.OverridenValues["colors"], out Dictionary<string, object> dict))
        {
            var newColorToRgbMapper = GetColorMapper(dict, project.Version);

            project.ColorMapper = newColorToRgbMapper;
            ColorIconGenerator.ClearCache(project);
        }
        if (config.ExtendedValues.ContainsKey("colors") && GetDictionary(config.ExtendedValues["colors"], out dict))
        {
            foreach (var pair in GetColorMapper(dict, project.Version))
            {
                project.ColorMapper[pair.Key] = pair.Value;
            }
        }

        if (config.OverridenValues.ContainsKey("screens") && GetDictionary(config.OverridenValues["screens"], out dict))
        {
            project.Screen = dict.Keys.ToList();
        }
        if (config.ExtendedValues.ContainsKey("screens") && GetDictionary(config.ExtendedValues["screens"], out dict))
        {
            project.Screen.AddRange(dict.Keys.Where(k => project.Screen.Contains(k) == false));
        }

        if (config.OverridenValues.ContainsKey("spacing") && GetDictionary(config.OverridenValues["spacing"], out dict))
        {
            project.SpacingMapper = dict.ToDictionary(p => p.Key, p => p.Value.ToString());
        }
        if (config.ExtendedValues.ContainsKey("spacing") && GetDictionary(config.ExtendedValues["spacing"], out dict))
        {
            foreach (var pair in dict)
            {
                project.SpacingMapper[pair.Key] = pair.Value.ToString();
            }
        }

        if (project.Version == TailwindVersion.V4)
        {
            DictionaryHelpers.MergeDictionaries(config.ThemeVariables, project.CssVariables);
            project.CssVariables = config.ThemeVariables;
        }
    }

    /// <summary>
    /// If config.EnabledCorePlugins is not null, all classes will be disabled except
    /// for those in enabled core plugins. If config.DisabledCorePlugins is not null and empty,
    /// all classes will exist except for those explicitly disabled.
    /// </summary>
    private void HandleCorePlugins(ProjectCompletionValues project, TailwindConfiguration config)
    {
        var original = _completionBase.GetUnsetCompletionConfiguration(project.Version);
        var enabledClasses = new List<TailwindClass>();
        if (config.EnabledCorePlugins is not null)
        {
            foreach (var plugin in config.EnabledCorePlugins)
            {
                if (project.ConfigurationValueToClassStems.ContainsKey(plugin))
                {
                    var stems = project.ConfigurationValueToClassStems[plugin];

                    foreach (var stem in stems)
                    {
                        if (stem.Contains("{*}"))
                        {
                            var s = stem.Replace("-{*}", "");

                            enabledClasses.AddRange(original.Classes.Where(c => c.Name.StartsWith(s) && c.UseColors == false && c.UseSpacing == false));
                        }
                        else if (stem.Contains("{s}"))
                        {
                            var s = stem.Replace("-{s}", "");

                            enabledClasses.AddRange(original.Classes.Where(c => c.Name.StartsWith(s) && c.UseColors == false && c.UseSpacing));
                        }
                        else if (stem.Contains("{c}"))
                        {
                            var s = stem.Replace("-{c}", "");

                            enabledClasses.AddRange(original.Classes.Where(c => c.Name.StartsWith(s) && c.UseColors && c.UseSpacing == false));
                        }
                        else if (stem.Contains('{'))
                        {
                            var s = stem.Replace($"-{stem.Split('-').Last()}", "");
                            var values = stem.Split('-').Last().Trim('{', '}').Split('|');

                            bool negate = false;
                            if (values[0].StartsWith("!"))
                            {
                                negate = true;
                                values[0] = values[0].Trim('!');
                            }

                            var classes = values.Select(v => $"{s}-{v}");

                            if (negate)
                            {
                                enabledClasses.AddRange(original.Classes.Where(c => c.Name.StartsWith(s) && classes.Contains(c.Name) == false && c.UseColors == false && c.UseSpacing == false));
                            }
                            else
                            {
                                enabledClasses.AddRange(original.Classes.Where(c => classes.Contains(c.Name) && c.UseColors == false && c.UseSpacing == false));
                            }
                        }
                        else
                        {
                            enabledClasses.AddRange(original.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.UseColors == false && c.UseSpacing == false));
                        }
                    }
                }
            }
        }
        else
        {
            enabledClasses = [.. original.Classes];

            if (config.DisabledCorePlugins is not null && config.DisabledCorePlugins.Count > 0)
            {
                foreach (var plugin in config.DisabledCorePlugins)
                {
                    if (project.ConfigurationValueToClassStems.ContainsKey(plugin))
                    {
                        var stems = project.ConfigurationValueToClassStems[plugin];

                        foreach (var stem in stems)
                        {
                            if (stem.Contains("{*}"))
                            {
                                var s = stem.Replace("-{*}", "");

                                enabledClasses.RemoveAll(c => c.Name.StartsWith(s) && c.UseColors == false && c.UseSpacing == false);
                            }
                            else if (stem.Contains("{s}"))
                            {
                                var s = stem.Replace("-{s}", "");

                                enabledClasses.RemoveAll(c => c.Name.StartsWith(s) && c.UseColors == false && c.UseSpacing);
                            }
                            else if (stem.Contains("{c}"))
                            {
                                var s = stem.Replace("-{c}", "");

                                enabledClasses.RemoveAll(c => c.Name.StartsWith(s) && c.UseColors && c.UseSpacing == false);
                            }
                            else if (stem.Contains('{'))
                            {
                                var s = stem.Replace($"-{stem.Split('-').Last()}", "");
                                var values = stem.Split('-').Last().Trim('{', '}').Split('|');

                                bool negate = false;
                                if (values[0].StartsWith("!"))
                                {
                                    negate = true;
                                    values[0] = values[0].Trim('!');
                                }

                                var classes = values.Select(v => $"{s}-{v}");

                                if (negate)
                                {
                                    enabledClasses.RemoveAll(c => c.Name.StartsWith(s) && classes.Contains(c.Name) == false && c.UseColors == false && c.UseSpacing == false);
                                }
                                else
                                {
                                    enabledClasses.RemoveAll(c => classes.Contains(c.Name) && c.UseColors == false && c.UseSpacing == false);
                                }
                            }
                            else
                            {
                                enabledClasses.RemoveAll(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.UseColors == false && c.UseSpacing == false);
                            }
                        }
                    }
                }
            }
        }

        project.Classes = enabledClasses;
    }

    /// <summary>
    /// Reconfigures all classes based on the specified configuration (configures theme.____)
    /// </summary>
    /// <param name="config">The configuration object</param>
    private void LoadIndividualConfigurationOverride(ProjectCompletionValues project, TailwindConfiguration config)
    {
        if (config is null)
        {
            return;
        }

        HandleCorePlugins(project, config);

        var original = _completionBase.GetUnsetCompletionConfiguration(project.Version);

        var applicable = project.ConfigurationValueToClassStems.Keys.Where(k => config.OverridenValues?.ContainsKey(k) == true);
        project.Variants = [.. original.Variants];
        var classesToRemove = new List<TailwindClass>();
        var classesToAdd = new List<TailwindClass>();

        project.CustomSpacingMappers = new Dictionary<string, Dictionary<string, string>>();
        project.CustomColorMappers = new Dictionary<string, Dictionary<string, string>>();

        foreach (var key in applicable)
        {
            var stems = project.ConfigurationValueToClassStems[key];

            foreach (var stem in stems)
            {
                if (stem.Contains(':'))
                {
                    var s = stem.Trim(':');
                    project.Variants.RemoveAll(c => c.StartsWith(s) && c.Replace($"{s}-", "").Count(ch => ch == '-') == 0 && c.Contains("[]") == false);

                    if (GetDictionary(config.OverridenValues[key], out var dict))
                    {
                        project.Variants.AddRange(dict.Keys.Select(k => k == "DEFAULT" ? s : $"{s}-{k}"));
                    }
                }
                else if (stem.Contains("{s}"))
                {
                    if (GetDictionary(config.OverridenValues[key], out var dict))
                    {
                        project.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = dict.ToDictionary(p => p.Key == "DEFAULT" ? "" : p.Key, p => p.Value.ToString());
                    }
                    else
                    {
                        project.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = new Dictionary<string, string>();
                    }
                }
                else if (stem.Contains("{c}"))
                {
                    if (GetDictionary(config.OverridenValues[key], out var dict))
                    {
                        project.CustomColorMappers[stem.Replace("{c}", "{0}")] = GetColorMapper(dict, project.Version);
                    }
                    else
                    {
                        project.CustomColorMappers[stem.Replace("{c}", "{0}")] = new Dictionary<string, string>();
                    }
                }
                else
                {
                    IEnumerable<TailwindClass> descClasses;
                    var s = stem;

                    if (stem.Contains("{*}"))
                    {
                        s = stem.Replace("-{*}", "");

                        descClasses = project.Classes.Where(c => c.Name.StartsWith(s) && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                        classesToRemove.AddRange(descClasses);
                    }
                    else if (stem.Contains('{'))
                    {
                        s = stem.Replace($"-{stem.Split('-').Last()}", "");
                        var values = stem.Split('-').Last().Trim('{', '}').Split('|');

                        bool negate = false;
                        if (values[0].StartsWith("!"))
                        {
                            negate = true;
                            values[0] = values[0].Trim('!');
                        }

                        var classes = values.Select(v => $"{s}-{v}");

                        if (negate)
                        {
                            descClasses = project.Classes.Where(c => c.Name.StartsWith(s) && classes.Contains(c.Name) == false && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                        }
                        else
                        {
                            descClasses = project.Classes.Where(c => classes.Contains(c.Name) && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                        }
                        classesToRemove.AddRange(descClasses);
                    }
                    else
                    {
                        descClasses = project.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                        classesToRemove.AddRange(descClasses);
                    }

                    if (GetDictionary(config.OverridenValues[key], out var dict))
                    {
                        // row-span and col-span are actually row and col internally
                        if (s.EndsWith("-span"))
                        {
                            s = s.Replace("-span", "");
                        }

                        classesToAdd.AddRange(dict.Keys
                            .Where(k => (project.CustomSpacingMappers.ContainsKey(stem + "-{0}") == false || project.CustomSpacingMappers[stem + "-{0}"].ContainsKey(k) == false) &&
                                              (project.CustomColorMappers.ContainsKey(stem + "-{0}") == false || project.CustomColorMappers[stem + "-{0}"].ContainsKey(k) == false))
                            .Select(k =>
                            {
                                if (k == "DEFAULT")
                                {
                                    return new TailwindClass()
                                    {
                                        Name = s
                                    };
                                }
                                else if (k.StartsWith("-"))
                                {
                                    return new TailwindClass()
                                    {
                                        Name = $"-{s}-{k.Substring(1)}"
                                    };
                                }
                                else
                                {
                                    return new TailwindClass()
                                    {
                                        Name = $"{s}-{k}"
                                    };
                                }
                            }));

                        var texts = descClasses.Where(c => project.DescriptionMapper.ContainsKey(c.Name)).Select(c =>
                            project.DescriptionMapper[c.Name]);

                        string format;

                        if (DescriptionGenerator.Handled(key))
                        {
                            format = null;
                        }
                        else if (texts.Count() == 0)
                        {
                            continue;
                        }
                        else if (texts.Count() == 1)
                        {
                            var text = texts.First();
                            var colon = text.IndexOf(':');
                            var length = text.IndexOf(';') - colon - 1;
                            format = text.Replace(text.Substring(colon + 2, length), "{0}");
                        }
                        else
                        {
                            var text = texts.First();
                            if (text.Count(c => c == ':') == 1)
                            {
                                var colon = text.IndexOf(':');
                                var length = text.IndexOf(';') - colon - 1;
                                format = text.Replace(text.Substring(colon + 2, length), "{0}");
                            }
                            else
                            {
                                var prefix = FindCommonPrefix(texts);
                                var suffix = FindCommonSuffix(texts);
                                var replace = text.Substring(prefix.Length, text.IndexOf(suffix) - prefix.Length);
                                format = text.Replace(replace, "{0}");
                            }
                        }

                        foreach (var pair in dict)
                        {
                            if (DescriptionGenerator.Handled(key))
                            {
                                project.CustomDescriptionMapper[s] = DescriptionGenerator.GenerateDescription(key, pair.Value);
                            }
                            else if (pair.Key == "DEFAULT")
                            {
                                project.CustomDescriptionMapper[s] = string.Format(format, pair.Value.ToString());
                            }
                            else
                            {
                                project.CustomDescriptionMapper[$"{s}-{pair.Key}"] = string.Format(format, pair.Value.ToString());
                            }
                        }
                    }
                }
            }
        }

        project.Classes.RemoveAll(classesToRemove.Contains);
        project.Classes.AddRange(classesToAdd);
    }

    /// <summary>
    /// Reconfigures all classes based on the specified configuration (configures theme.extend.____).
    /// </summary>
    /// <remarks>
    /// Should be called after <see cref="LoadIndividualConfigurationOverride(TailwindConfiguration)"/>.
    /// </remarks>
    /// <param name="config">The configuration object</param>
    private void LoadIndividualConfigurationExtend(ProjectCompletionValues project, TailwindConfiguration config)
    {
        if (config is null)
        {
            return;
        }

        var applicable = project.ConfigurationValueToClassStems.Keys.Where(k => config.ExtendedValues?.ContainsKey(k) == true);

        if (project.Version == TailwindVersion.V4 && config.ExtendedValues.TryGetValue("screens", out var obj) && obj is Dictionary<string, object> screens)
        {
            foreach (var screen in screens.Keys)
            {
                string[] toInsert = [$"not-{screen}", $"max-{screen}", $"min-{screen}", $"@max-{screen}", $"@min-{screen}"];

                foreach (var insert in toInsert)
                {
                    if (project.Variants.Contains(insert) == false)
                    {
                        project.Variants.Add(insert);
                    }
                }
            }
        }

        var classesToAdd = new List<TailwindClass>();

        foreach (var key in applicable)
        {
            var stems = project.ConfigurationValueToClassStems[key];

            foreach (var stem in stems)
            {
                if (stem.Contains(':'))
                {
                    var s = stem.Trim(':');

                    if (GetDictionary(config.ExtendedValues[key], out var dict))
                    {
                        foreach (var k in dict.Keys)
                        {
                            var insert = k == "DEFAULT" ? s : $"{s}-{k}";

                            if (project.Variants.Contains(insert) == false)
                            {
                                project.Variants.Add(insert);
                            }
                        };
                    }
                }
                else if (stem.Contains("{s}"))
                {
                    if (GetDictionary(config.ExtendedValues[key], out var dict))
                    {
                        var newSpacing = project.SpacingMapper.ToDictionary(p => p.Key, p => p.Value);
                        foreach (var pair in dict)
                        {
                            newSpacing[pair.Key == "DEFAULT" ? "" : pair.Key] = pair.Value.ToString();
                        }
                        project.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = newSpacing;
                    }
                }
                else if (stem.Contains("{c}"))
                {
                    if (GetDictionary(config.ExtendedValues[key], out var dict))
                    {
                        var newMapper = project.ColorMapper.ToDictionary(p => p.Key, p => p.Value);
                        foreach (var pair in GetColorMapper(dict, project.Version))
                        {
                            newMapper[pair.Key] = pair.Value;
                        }

                        project.CustomColorMappers[stem.Replace("{c}", "{0}")] = newMapper;
                    }
                }
                else
                {
                    var s = stem;
                    IEnumerable<TailwindClass> descClasses;
                    if (stem.Contains("{*}"))
                    {
                        s = stem.Replace("-{*}", "");

                        descClasses = project.Classes.Where(c => c.Name.StartsWith(s) && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                    }
                    else if (stem.Contains('{'))
                    {
                        s = stem.Replace($"-{stem.Split('-').Last()}", "");
                        var values = stem.Split('-').Last().Trim('{', '}').Split('|').Select(v => $"{s}-{v}");

                        descClasses = project.Classes.Where(c => values.Contains(c.Name) && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                    }
                    else
                    {
                        descClasses = project.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.HasArbitrary == false && c.UseColors == false && c.UseSpacing == false);
                    }

                    var insertStem = s;
                    // row-span and col-span are actually row and col internally
                    if (s.EndsWith("-span"))
                    {
                        insertStem = s.Replace("-span", "");
                    }

                    if (GetDictionary(config.ExtendedValues[key], out var dict))
                    {
                        classesToAdd.AddRange(
                            dict.Keys.Select(k =>
                            {
                                if (k == "DEFAULT")
                                {
                                    return insertStem;
                                }
                                else if (k.StartsWith("-"))
                                {
                                    return $"-{insertStem}-{k.Substring(1)}";
                                }
                                else
                                {
                                    return $"{insertStem}-{k}";
                                }
                            })
                            .Where(k => !string.IsNullOrWhiteSpace(k) && project.Classes.Any(c => c.Name == k) == false)
                            .Select(k =>
                            {
                                return new TailwindClass()
                                {
                                    Name = k
                                };
                            }));

                        var texts = descClasses.Where(c => project.DescriptionMapper.ContainsKey(c.Name)).Select(c =>
                            project.DescriptionMapper[c.Name]);

                        string format;

                        if (DescriptionGenerator.Handled(key))
                        {
                            format = null;
                        }
                        else if (texts.Count() == 0)
                        {
                            continue;
                        }
                        else if (texts.Count() == 1)
                        {
                            var text = texts.First();
                            var colon = text.IndexOf(':');
                            var length = text.IndexOf(';') - colon - 1;
                            format = text.Replace(text.Substring(colon + 2, length), "{0}");
                        }
                        else
                        {
                            var text = texts.First();
                            var colon = text.IndexOf(':');
                            var length = text.IndexOf(';') - colon - 2;
                            var replace = text.Substring(colon + 2, length);
                            if (text.Count(c => c == ':') == CountSubstring(text, replace))
                            {
                                format = text.Replace(replace, "{0}");
                            }
                            else
                            {
                                var prefix = FindCommonPrefix(texts);
                                var suffix = FindCommonSuffix(texts);
                                replace = text.Substring(prefix.Length, text.IndexOf(suffix) - prefix.Length);
                                format = text.Replace(replace, "{0}");
                            }
                        }

                        foreach (var pair in dict)
                        {
                            string description;

                            if (DescriptionGenerator.Handled(key))
                            {
                                description = DescriptionGenerator.GenerateDescription(key, pair.Value);
                            }
                            else
                            {
                                description = string.Format(format, pair.Value.ToString());
                            }

                            if (pair.Key == "DEFAULT")
                            {
                                project.CustomDescriptionMapper[insertStem] = description;
                            }
                            else
                            {
                                project.CustomDescriptionMapper[$"{insertStem}-{pair.Key}"] = description;
                            }
                        }
                    }
                }
            }
        }
        project.Classes.AddRange(classesToAdd);

        // fix order

        // Order by ending number, if applicable, then any text after
        // i.e. inherit, 10, 20, 30, 40, 5, 50 -> 5, 10, 20, 30, 40, 50, inherit

        project.Classes.Sort((x, y) =>
        {
            if (!x.Name.Contains('-') || !y.Name.Contains('-'))
            {
                return x.Name.CompareTo(y.Name);
            }

            // Compare the base names (before the hyphen)
            var xBaseName = x.Name.Substring(0, x.Name.LastIndexOf('-'));
            var yBaseName = y.Name.Substring(0, y.Name.LastIndexOf('-'));

            var baseNameComparison = xBaseName.CompareTo(yBaseName);
            if (baseNameComparison != 0)
            {
                return baseNameComparison; // If base names are different, return comparison
            }

            // If base names are the same, compare the numeric part after the last hyphen (if present)
            if (xBaseName == yBaseName)
            {
                var xIsNumeric = double.TryParse(x.Name.Substring(x.Name.LastIndexOf('-') + 1), out double xNumber);
                var yIsNumeric = double.TryParse(y.Name.Substring(y.Name.LastIndexOf('-') + 1), out double yNumber);

                if (xIsNumeric && yIsNumeric)
                {
                    // Compare numerically if both are valid numbers
                    return xNumber.CompareTo(yNumber);
                }
                else if (xIsNumeric)
                {
                    // If only x is numeric, x comes before y
                    return -1;
                }
                else if (yIsNumeric)
                {
                    // If only y is numeric, y comes before x
                    return 1;
                }
            }

            // If either has no numeric part or neither can be parsed as a number, compare lexicographically
            return string.Join(x.Name).CompareTo(y.Name);
        });
    }

    /// <summary>
    /// Loads IntelliSense for plugins, @custom-variant and @utility
    /// </summary>
    /// <param name="config">The configuration object</param>
    private void LoadPlugins(ProjectCompletionValues project, TailwindConfiguration config)
    {
        if (project.Version == TailwindVersion.V4)
        {
            // Handle plugin variants

            if (config.PluginVariantDescriptions is not null)
            {
                project.VariantsToDescriptions =
                    _completionBase.GetUnsetCompletionConfiguration(TailwindVersion.V4).VariantsToDescriptions
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                foreach (var pair in config.PluginVariantDescriptions)
                {
                    if (project.VariantsToDescriptions.ContainsKey(pair.Key) == false)
                    {
                        // @slot; and @slot are both valid
                        project.VariantsToDescriptions[pair.Key] = pair.Value.Replace("@slot;", "{0}").Replace("@slot", "{0}");
                    }
                }
            }

            if (config.PluginDescriptions is not null)
            {
                project.PluginClasses = [];
                project.CustomDescriptionMapper = [];
                var classesToAdd = new List<TailwindClass>();

                foreach (var pair in config.PluginDescriptions)
                {
                    // Case 1: Simple/complex utilities
                    /* 
                    @utility content-auto {
                        content-visibility: auto;
                    }
                    OR 
                    @utility scrollbar-hidden {
                        &::-webkit-scrollbar {
                            display: none;
                        }
                    }
                    */
                    if (!pair.Key.Contains("*"))
                    {
                        project.PluginClasses.Add(pair.Key);
                        project.CustomDescriptionMapper.Add(pair.Key, pair.Value);
                    }
                    // Case 2 (edge): no --value, but has a wildcard
                    else if (!pair.Value.Contains("--value("))
                    {
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{a}"), pair.Value);
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{f}"), pair.Value);
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{%}"), pair.Value);
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{n}"), pair.Value);
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{c}"), pair.Value);
                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{s}"), pair.Value);
                    }
                    // Case 3: --value is present
                    else
                    {
                        // Split based on --value type
                        var valueToDescription = new Dictionary<string, string>();

                        var description = pair.Value.Trim();
                        var splitBySemicolon = CssConfigSplitter.Split(description);

                        var standard = "";

                        foreach (var split in splitBySemicolon)
                        {
                            var attribute = split;

                            // Handle media queries / nested statements
                            if (split.Contains('{'))
                            {
                                var media = split.Substring(0, split.IndexOf('{') + 1);
                                attribute = split.Substring(media.Length + 1);

                                if (attribute.Trim().StartsWith("}"))
                                {
                                    // Edge case: empty block
                                    attribute = attribute.Substring(attribute.IndexOf('}') + 1);
                                }
                                else
                                {
                                    standard += media;

                                    foreach (var key in valueToDescription.Keys.ToList())
                                    {
                                        valueToDescription[key] += media;
                                    }
                                }
                            }
                            // Handle ending brackets
                            else if (split.Contains('}'))
                            {
                                var bracket = split.Substring(0, split.LastIndexOf('}') + 1);
                                attribute = split.Substring(bracket.Length + 1);
                                standard += bracket;

                                foreach (var key in valueToDescription.Keys.ToList())
                                {
                                    valueToDescription[key] += bracket;
                                }
                            }

                            attribute = attribute?.Trim();

                            if (string.IsNullOrWhiteSpace(attribute))
                            {
                                continue;
                            }
                            // Case 1: No --value
                            else if (!attribute.Contains("--value("))
                            {
                                standard += attribute;

                                foreach (var key in valueToDescription.Keys.ToList())
                                {
                                    valueToDescription[key] += attribute;
                                }
                            }
                            else
                            {
                                if (!attribute.Contains(':'))
                                {
                                    continue;
                                }

                                var attributeKey = attribute.Substring(0, attribute.IndexOf(':'));

                                // Handle multiple or single --value
                                // --value(--tab-size-*, integer, [integer])
                                var start = attribute.IndexOf("--value(");

                                var end = attribute.IndexOf(')', start + 1);

                                // Malformed
                                if (start == -1 || end == -1)
                                {
                                    continue;
                                }

                                // integer in --value(integer)
                                var values = attribute.Substring(start + 8, end - start - 8);
                                // --value(integer), full thing
                                var valueMethod = attribute.Substring(start, end - start + 1);

                                var descriptionFormat = $"{attribute.Replace(valueMethod, "{0}")};";

                                foreach (var value in values.Split(','))
                                {
                                    var trimmed = value.Trim();

                                    // Case 1: --theme-variable-*
                                    if (trimmed.StartsWith("--"))
                                    {
                                        // --value does not handle without *
                                        if (!trimmed.EndsWith("-*"))
                                        {
                                            continue;
                                        }

                                        if (valueToDescription.ContainsKey(trimmed) == false)
                                        {
                                            valueToDescription[trimmed] = standard;
                                        }
                                        valueToDescription[trimmed] += descriptionFormat;
                                    }
                                    // Case 2: integer/number
                                    // Case 3: ratio
                                    else if (trimmed == "integer" || trimmed == "number" || trimmed == "ratio")
                                    {
                                        if (valueToDescription.ContainsKey(trimmed) == false)
                                        {
                                            valueToDescription[trimmed] = standard;
                                        }
                                        valueToDescription[trimmed] += descriptionFormat;
                                    }
                                    // Case 4: [...] (arbitrary, such as [integer])
                                    else if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                                    {
                                        if (valueToDescription.ContainsKey(trimmed) == false)
                                        {
                                            valueToDescription[trimmed] = standard;
                                        }
                                        valueToDescription[trimmed] += descriptionFormat;
                                    }
                                }
                            }
                        }

                        foreach (var typeToDescriptionPair in valueToDescription)
                        {
                            var type = typeToDescriptionPair.Key;
                            var desc = typeToDescriptionPair.Value;

                            // Remove --spacing(...)
                            int spacingIndex;
                            var shouldContinue = false;

                            while ((spacingIndex = desc.IndexOf("--spacing(")) != -1)
                            {
                                // Find a balancing pair of parenthesis

                                var start = spacingIndex + "--spacing(".Length;

                                var levels = 1;
                                int end;
                                for (end = start; end < desc.Length; end++)
                                {
                                    var current = desc[end];

                                    if (current == '(')
                                    {
                                        levels++;
                                    }
                                    else if (current == ')')
                                    {
                                        levels--;
                                        if (levels == 0)
                                        {
                                            break;
                                        }
                                    }
                                }

                                if (levels > 0)
                                {
                                    shouldContinue = true;
                                    break;
                                }

                                var parameter = desc.Substring(start, end - start);
                                var total = desc.Substring(spacingIndex, end - spacingIndex + 1);

                                desc = desc.Replace(total, $"calc(var(--spacing) * {parameter})");
                            }

                            if (shouldContinue)
                            {
                                continue;
                            }

                            if (type.StartsWith("--"))
                            {
                                var stem = type.Substring(0, type.Length - 2).Trim();

                                // Special case:
                                // --color-*
                                if (stem == "--color")
                                {
                                    classesToAdd.Add(new()
                                    {
                                        Name = pair.Key.Replace("*", "{c}"),
                                        UseColors = true
                                    });

                                    project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{c}"), desc);
                                }
                                // Special case 2:
                                // --color-red-*
                                else if (stem.StartsWith("--color-"))
                                {
                                    var colorRoot = stem.Substring("--color-".Length);

                                    var colors = 
                                        project.ColorMapper.Where(k => k.Key.StartsWith(colorRoot));
                                    var classes = colors.Select(v => pair.Key.Replace("*", v.Key));

                                    project.PluginClasses.AddRange(classes);

                                    foreach (var color in colors)
                                    {
                                        project.CustomDescriptionMapper.Add(pair.Key.Replace("*", color.Key), desc.Replace("{0}", $"var(--color-{color.Key})"));
                                    }
                                }
                                else
                                {
                                    var variables = project.CssVariables.Where(k => k.Key.StartsWith(stem));

                                    project.PluginClasses.AddRange(variables.Select(
                                        v => pair.Key.Replace("-*", v.Key.Substring(stem.Length))));

                                    foreach (var var in variables)
                                    {
                                        project.CustomDescriptionMapper.Add(pair.Key.Replace("-*", var.Key.Substring(stem.Length)), desc.Replace("{0}", $"var({var.Key})"));
                                    }
                                }
                            }
                            else if (type == "number" || type == "integer")
                            {
                                project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{n}"), desc);
                            }
                            else if (type == "ratio")
                            {
                                project.CustomDescriptionMapper.Add(pair.Key.Replace("*", "{f}"), desc);
                            }
                            else if (type.StartsWith("[") && type.EndsWith("]"))
                            {
                                var stem = pair.Key.Replace("*", "{a}");
                                if (!project.CustomDescriptionMapper.ContainsKey(stem))
                                {
                                    project.CustomDescriptionMapper.Add(stem, desc);
                                }
                            }
                        }
                    }
                }

                project.Classes.AddRange(classesToAdd);
            }
        }
        else
        {
            project.CustomDescriptionMapper = config?.PluginDescriptions ?? [];
            project.PluginClasses = config.PluginClasses;
        }
        project.PluginVariants = config.PluginVariants;
    }

    private Dictionary<string, string> GetColorMapper(Dictionary<string, object> colors, TailwindVersion version, string prev = "")
    {
        var newColorToRgbMapper = new Dictionary<string, string>();

        foreach (var key in colors.Keys)
        {
            var value = colors[key];

            var actual = prev;

            // when the root key is DEFAULT, it takes no effect on class names
            if (prev == "" || key != "DEFAULT")
            {
                if (actual != "")
                {
                    actual += "-";
                }
                actual += key;
            }

            if (value is string s)
            {
                if (version == TailwindVersion.V4)
                {
                    newColorToRgbMapper[actual] = s;
                    continue;
                }
                if (ColorHelpers.IsHex(s, out string hex))
                {
                    var color = ColorTranslator.FromHtml($"#{hex}");
                    newColorToRgbMapper[actual] = $"{color.R},{color.G},{color.B}";
                }
                else if (s.StartsWith("rgb"))
                {
                    var openParen = s.IndexOf('(');
                    var closeParen = s.IndexOf(')', openParen);
                    if (openParen != -1 && closeParen != -1 && closeParen - openParen > 1)
                    {
                        var text = s.Substring(openParen + 1, closeParen - openParen - 1);
                        var values = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                            .Take(3)
                            .Where(v => byte.TryParse(v, out _))
                            .Select(byte.Parse)
                            .ToArray();

                        if (values.Length == 3)
                        {
                            newColorToRgbMapper[actual] = $"{values[0]},{values[1]},{values[2]}";
                        }
                        else
                        {
                            newColorToRgbMapper[actual] = "{noparse}" + s;
                        }
                    }
                }
                else
                {
                    newColorToRgbMapper[actual] = "{noparse}" + s;
                }
            }
            else if (value is Dictionary<string, object> colorVariants)
            {
                foreach (var pair in GetColorMapper(colorVariants, version, actual))
                {
                    newColorToRgbMapper[pair.Key] = pair.Value;
                }
            }
        }

        return newColorToRgbMapper;
    }

    private string FindCommonPrefix(IEnumerable<string> texts)
    {
        if (texts.Any() == false)
        {
            return "";
        }

        var prefix = texts.First();
        foreach (var text in texts)
        {
            var i = 0;
            while (i < prefix.Length && i < text.Length && prefix[i] == text[i])
            {
                i++;
            }
            prefix = prefix.Substring(0, i);
        }

        return prefix;
    }

    private string FindCommonSuffix(IEnumerable<string> texts)
    {
        var suffix = new string(FindCommonPrefix(texts.Select(t => new string(t.Reverse().ToArray()))).Reverse().ToArray());

        // Prevents duplicate endings
        if (suffix.Count(char.IsPunctuation) == 1)
        {
            return ";";
        }
        return suffix;
    }

    public int CountSubstring(string text, string value)
    {
        int count = 0, minIndex = text.IndexOf(value, 0);
        while (minIndex != -1)
        {
            minIndex = text.IndexOf(value, minIndex + value.Length);
            count++;
        }
        return count;
    }
}
