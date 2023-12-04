using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Configuration
{
    public sealed partial class CompletionConfiguration
    {
        /// <summary>
        /// Reconfigures colors, spacing, and screen
        /// </summary>
        private void LoadGlobalConfiguration(TailwindConfiguration config)
        {
            _completionBase.SpacingMapper = SpacingMapperOrig.ToDictionary(pair => pair.Key, pair => pair.Value);
            _completionBase.Screen = ScreenOrig.ToList();
            _completionBase.ColorToRgbMapper = ColorToRgbMapperOrig.ToDictionary(pair => pair.Key, pair => pair.Value);
            _completionBase.ColorToRgbMapperCache.Clear();

            if (config is null && _areValuesDefault == false)
            {
                // Reset to default; either user has changed/deleted config file or there is none
                return;
            }

            _areValuesDefault = true;

            if (config.OverridenValues.ContainsKey("colors") && GetDictionary(config.OverridenValues["colors"], out Dictionary<string, object> dict))
            {
                var newColorToRgbMapper = GetColorMapper(dict);

                _completionBase.ColorToRgbMapper = newColorToRgbMapper;
                _completionBase.ColorToRgbMapperCache.Clear();
            }
            if (config.ExtendedValues.ContainsKey("colors") && GetDictionary(config.ExtendedValues["colors"], out dict))
            {
                foreach (var pair in GetColorMapper(dict))
                {
                    _completionBase.ColorToRgbMapper[pair.Key] = pair.Value;
                }
            }

            if (config.OverridenValues.ContainsKey("screens") && GetDictionary(config.OverridenValues["screens"], out dict))
            {
                _completionBase.Screen = dict.Keys.ToList();
            }
            if (config.ExtendedValues.ContainsKey("screens") && GetDictionary(config.ExtendedValues["screens"], out dict))
            {
                _completionBase.Screen.AddRange(dict.Keys.Where(k => _completionBase.Screen.Contains(k) == false));
            }

            if (config.OverridenValues.ContainsKey("spacing") && GetDictionary(config.OverridenValues["spacing"], out dict))
            {
                _completionBase.SpacingMapper = dict.ToDictionary(p => p.Key, p => p.Value.ToString());
            }
            if (config.ExtendedValues.ContainsKey("spacing") && GetDictionary(config.ExtendedValues["spacing"], out dict))
            {
                foreach (var pair in dict)
                {
                    _completionBase.SpacingMapper[pair.Key] = pair.Value.ToString();
                }
            }

            _areValuesDefault = false;
        }

        /// <summary>
        /// Reconfigures all classes based on the specified configuration (configures theme.____)
        /// </summary>
        /// <param name="config">The configuration object</param>
        private void LoadIndividualConfigurationOverride(TailwindConfiguration config)
        {
            if (config is null)
            {
                return;
            }

            var applicable = _completionBase.ConfigurationValueToClassStems.Keys.Where(k => config.OverridenValues?.ContainsKey(k) == true);
            _completionBase.Classes = ClassesOrig.ToList();
            _completionBase.Modifiers = ModifiersOrig.ToList();
            var classesToRemove = new List<TailwindClass>();
            var classesToAdd = new List<TailwindClass>();

            _completionBase.CustomSpacingMappers = new Dictionary<string, Dictionary<string, string>>();
            _completionBase.CustomColorMappers = new Dictionary<string, Dictionary<string, string>>();

            foreach (var key in applicable)
            {
                var stems = _completionBase.ConfigurationValueToClassStems[key];

                foreach (var stem in stems)
                {
                    if (stem.Contains(':'))
                    {
                        var s = stem.Trim(':');
                        _completionBase.Modifiers.RemoveAll(c => c.StartsWith(s) && c.Replace($"{s}-", "").Count(ch => ch == '-') == 0 && c.Contains("[]") == false);

                        if (GetDictionary(config.OverridenValues[key], out var dict))
                        {
                            _completionBase.Modifiers.AddRange(dict.Keys.Select(k => k == "DEFAULT" ? s : $"{s}-{k}"));
                        }
                    }
                    else if (stem.Contains("{s}"))
                    {
                        if (GetDictionary(config.OverridenValues[key], out var dict))
                        {
                            _completionBase.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = dict.ToDictionary(p => p.Key == "DEFAULT" ? "" : p.Key, p => p.Value.ToString());
                        }
                        else
                        {
                            _completionBase.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = new Dictionary<string, string>();
                        }
                    }
                    else if (stem.Contains("{c}"))
                    {
                        if (GetDictionary(config.OverridenValues[key], out var dict))
                        {
                            _completionBase.CustomColorMappers[stem.Replace("{c}", "{0}")] = GetColorMapper(dict);
                        }
                        else
                        {
                            _completionBase.CustomColorMappers[stem.Replace("{c}", "{0}")] = new Dictionary<string, string>();
                        }
                    }
                    else
                    {
                        IEnumerable<TailwindClass> descClasses;
                        var s = stem;

                        if (stem.Contains("{*}"))
                        {
                            s = stem.Replace("-{*}", "");

                            descClasses = _completionBase.Classes.Where(c => c.Name.StartsWith(s) && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                            classesToRemove.AddRange(descClasses);
                        }
                        else if (stem.Contains('{'))
                        {
                            s = stem.Replace($"-{stem.Split('-').Last()}", "");
                            var values = stem.Split('-').Last().Trim('{', '}').Split('|').Select(v => $"{s}-{v}");

                            descClasses = _completionBase.Classes.Where(c => values.Contains(c.Name) && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                            classesToRemove.AddRange(descClasses);
                        }
                        else
                        {
                            descClasses = _completionBase.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                            classesToRemove.AddRange(descClasses);
                        }

                        if (GetDictionary(config.OverridenValues[key], out var dict))
                        {
                            // row-span and col-span are actually row and col internally
                            if (s.EndsWith("-span"))
                            {
                                s = s.Replace("-span", "");
                            }

                            classesToAdd.AddRange(dict.Keys.Select(k =>
                            {
                                if (k == "DEFAULT")
                                {
                                    return new TailwindClass()
                                    {
                                        Name = s
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

                            var texts = descClasses.Where(c => _completionBase.DescriptionMapper.ContainsKey(c.Name)).Select(c =>
                                _completionBase.DescriptionMapper[c.Name]);

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
                                    _completionBase.CustomDescriptionMapper[s] = DescriptionGenerator.GenerateDescription(key, pair.Value);
                                }
                                else if (pair.Key == "DEFAULT")
                                {
                                    _completionBase.CustomDescriptionMapper[s] = string.Format(format, pair.Value.ToString());
                                }
                                else
                                {
                                    _completionBase.CustomDescriptionMapper[$"{s}-{pair.Key}"] = string.Format(format, pair.Value.ToString());
                                }
                            }
                        }
                    }
                }
            }

            _completionBase.Classes.RemoveAll(c => classesToRemove.Contains(c));
            _completionBase.Classes.AddRange(classesToAdd);
        }

        /// <summary>
        /// Reconfigures all classes based on the specified configuration (configures theme.extend.____).
        /// </summary>
        /// <remarks>
        /// Should be called after <see cref="LoadIndividualConfigurationOverride(TailwindConfiguration)"/>.
        /// </remarks>
        /// <param name="config">The configuration object</param>
        private void LoadIndividualConfigurationExtend(TailwindConfiguration config)
        {
            if (config is null)
            {
                return;
            }

            var applicable = _completionBase.ConfigurationValueToClassStems.Keys.Where(k => config.ExtendedValues?.ContainsKey(k) == true);

            var classesToAdd = new List<TailwindClass>();

            foreach (var key in applicable)
            {
                var stems = _completionBase.ConfigurationValueToClassStems[key];

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

                                if (_completionBase.Modifiers.Contains(insert) == false)
                                {
                                    _completionBase.Modifiers.Add(insert);
                                }
                            };
                        }
                    }
                    else if (stem.Contains("{s}"))
                    {
                        if (GetDictionary(config.ExtendedValues[key], out var dict))
                        {
                            var newSpacing = _completionBase.SpacingMapper.ToDictionary(p => p.Key, p => p.Value);
                            foreach (var pair in dict)
                            {
                                newSpacing[pair.Key == "DEFAULT" ? "" : pair.Key] = pair.Value.ToString();
                            }
                            _completionBase.CustomSpacingMappers[stem.Replace("{s}", "{0}")] = newSpacing;
                        }
                    }
                    else if (stem.Contains("{c}"))
                    {
                        if (GetDictionary(config.ExtendedValues[key], out var dict))
                        {
                            var newMapper = _completionBase.ColorToRgbMapper.ToDictionary(p => p.Key, p => p.Value);
                            foreach (var pair in GetColorMapper(dict))
                            {
                                newMapper[pair.Key] = pair.Value;
                            }

                            _completionBase.CustomColorMappers[stem.Replace("{c}", "{0}")] = newMapper;
                        }
                    }
                    else
                    {
                        var s = stem;
                        IEnumerable<TailwindClass> descClasses;
                        if (stem.Contains("{*}"))
                        {
                            s = stem.Replace("-{*}", "");

                            descClasses = _completionBase.Classes.Where(c => c.Name.StartsWith(s) && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                        }
                        else if (stem.Contains('{'))
                        {
                            s = stem.Replace($"-{stem.Split('-').Last()}", "");
                            var values = stem.Split('-').Last().Trim('{', '}').Split('|').Select(v => $"{s}-{v}");

                            descClasses = _completionBase.Classes.Where(c => values.Contains(c.Name) && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                        }
                        else
                        {
                            descClasses = _completionBase.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0 && c.SupportsBrackets == false && c.UseColors == false && c.UseSpacing == false);
                        }

                        var insertStem = s;
                        // row-span and col-span are actually row and col internally
                        if (s.EndsWith("-span"))
                        {
                            insertStem = s.Replace("-span", "");
                        }

                        if (GetDictionary(config.ExtendedValues[key], out var dict))
                        {
                            classesToAdd.AddRange(dict.Keys
                                .Where(k => _completionBase.Classes.Any(c => c.Name == $"{s}-{k}") == false)
                                .Select(k =>
                                {
                                    return new TailwindClass()
                                    {
                                        Name = $"{insertStem}-{k}"
                                    };
                                }));

                            var texts = descClasses.Where(c => _completionBase.DescriptionMapper.ContainsKey(c.Name)).Select(c =>
                                _completionBase.DescriptionMapper[c.Name]);

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
                                    _completionBase.CustomDescriptionMapper[insertStem] = description;
                                }
                                else
                                {
                                    _completionBase.CustomDescriptionMapper[$"{insertStem}-{pair.Key}"] = description;
                                }
                            }
                        }
                    }
                }
            }
            _completionBase.Classes.AddRange(classesToAdd);

            // fix order
            _completionBase.Classes.Sort((x, y) => x.Name.CompareTo(y.Name));
        }

        /// <summary>
        /// Loads IntelliSense for plugins
        /// </summary>
        /// <param name="config">The configuration object</param>
        private void LoadPlugins(TailwindConfiguration config)
        {
            _completionBase.PluginClasses = config.PluginClasses;
            _completionBase.PluginModifiers = config.PluginModifiers;
        }

        private Dictionary<string, string> GetColorMapper(Dictionary<string, object> colors)
        {
            var newColorToRgbMapper = new Dictionary<string, string>();

            foreach (var key in colors.Keys)
            {
                var value = colors[key];

                if (value is string s)
                {
                    if (IsHex(s, out string hex))
                    {
                        var color = ColorTranslator.FromHtml($"#{hex}");
                        newColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
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
                                newColorToRgbMapper[key] = $"{float.Parse(values[0]):0},{float.Parse(values[1]):1},{float.Parse(values[2]):2}";
                            }
                            else
                            {
                                newColorToRgbMapper[key] = "{noparse}" + s;
                            }
                        }
                    }
                    else
                    {
                        newColorToRgbMapper[key] = "{noparse}" + s;
                    }
                }
                else if (value is Dictionary<string, object> colorVariants)
                {
                    foreach (var colorVariant in colorVariants.Keys)
                    {
                        if (colorVariant == "DEFAULT")
                        {
                            if (IsHex(colorVariants[colorVariant], out string hex))
                            {
                                var color = ColorTranslator.FromHtml($"#{hex}");
                                newColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                            }
                            else if (colorVariants[colorVariant].ToString().StartsWith("rgb"))
                            {
                                var openParen = colorVariants[colorVariant].ToString().IndexOf('(');
                                var closeParen = colorVariants[colorVariant].ToString().IndexOf(')', openParen);
                                if (openParen != -1 && closeParen != -1 && closeParen - openParen > 1)
                                {
                                    var text = colorVariants[colorVariant].ToString().Substring(openParen + 1, closeParen - openParen - 1);
                                    var values = text.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    if (values.Length >= 3 && values.Take(3).All(v => float.TryParse(v, out _)))
                                    {
                                        newColorToRgbMapper[key] = $"{float.Parse(values[0]):0},{float.Parse(values[1]):1},{float.Parse(values[2]):2}";
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (IsHex(colorVariants[colorVariant], out string hex))
                            {
                                var color = ColorTranslator.FromHtml($"#{hex}");
                                newColorToRgbMapper[key + "-" + colorVariant] = $"{color.R},{color.G},{color.B}";
                            }
                            else if (colorVariants[colorVariant].ToString().StartsWith("rgb"))
                            {
                                var openParen = colorVariants[colorVariant].ToString().IndexOf('(');
                                var closeParen = colorVariants[colorVariant].ToString().IndexOf(')', openParen);
                                if (openParen != -1 && closeParen != -1 && closeParen - openParen > 1)
                                {
                                    var text = colorVariants[colorVariant].ToString().Substring(openParen + 1, closeParen - openParen - 1);
                                    var values = text.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    if (values.Length >= 3 && values.Take(3).All(v => float.TryParse(v, out _)))
                                    {
                                        newColorToRgbMapper[key + "-" + colorVariant] = $"{float.Parse(values[0]):0},{float.Parse(values[1]):1},{float.Parse(values[2]):2}";
                                    }
                                }
                            }
                        }

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
}
