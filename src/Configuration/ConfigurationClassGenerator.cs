using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        project.CustomDescriptionMapper = config?.PluginDescriptions ?? [];
        project.Blocklist = new HashSet<string>(config?.Blocklist ?? []);
        project.CssVariables = original.CssVariables;

        if (config is null)
        {
            // Reset to default; either user has changed/deleted config file or there is none
            return;
        }

        if (config.OverridenValues.ContainsKey("colors") && GetDictionary(config.OverridenValues["colors"], out Dictionary<string, object> dict))
        {
            var newColorToRgbMapper = GetColorMapper(dict);

            project.ColorMapper = newColorToRgbMapper;
            ColorIconGenerator.ClearCache(project);
        }
        if (config.ExtendedValues.ContainsKey("colors") && GetDictionary(config.ExtendedValues["colors"], out dict))
        {
            foreach (var pair in GetColorMapper(dict))
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
        project.Modifiers = original.Modifiers.ToList();
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
                    project.Modifiers.RemoveAll(c => c.StartsWith(s) && c.Replace($"{s}-", "").Count(ch => ch == '-') == 0 && c.Contains("[]") == false);

                    if (GetDictionary(config.OverridenValues[key], out var dict))
                    {
                        project.Modifiers.AddRange(dict.Keys.Select(k => k == "DEFAULT" ? s : $"{s}-{k}"));
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
                        project.CustomColorMappers[stem.Replace("{c}", "{0}")] = GetColorMapper(dict);
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

                            if (project.Modifiers.Contains(insert) == false)
                            {
                                project.Modifiers.Add(insert);
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
                        foreach (var pair in GetColorMapper(dict))
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
                            .Where(k => project.Classes.Any(c => c.Name == k) == false)
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
    /// Loads IntelliSense for plugins
    /// </summary>
    /// <param name="config">The configuration object</param>
    private void LoadPlugins(ProjectCompletionValues project, TailwindConfiguration config)
    {
        project.PluginClasses = config.PluginClasses;
        project.PluginModifiers = config.PluginModifiers;
    }

    private Dictionary<string, string> GetColorMapper(Dictionary<string, object> colors, string prev = "")
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
                        var values = text.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                        if (values.Length >= 3 && values.Take(3).All(v => float.TryParse(v, out _)))
                        {
                            newColorToRgbMapper[actual] = $"{float.Parse(values[0]):0},{float.Parse(values[1]):1},{float.Parse(values[2]):2}";
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
                foreach (var pair in GetColorMapper(colorVariants, actual))
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
