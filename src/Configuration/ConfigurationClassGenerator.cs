using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Configuration
{
    public sealed partial class CompletionConfiguration
    {
        private TailwindConfiguration _lastConfig;

        /// <summary>
        /// Method called by <see cref="ReloadCustomAttributesAsync"/> to reconfigure colors, spacing, and screen
        /// </summary>
        private void LoadGlobalConfiguration(TailwindConfiguration config)
        {
            _completionBase.Modifiers = ModifiersOrig.ToList();
            _completionBase.Spacing = SpacingOrig.ToList();
            _completionBase.ColorToRgbMapper = ColorToRgbMapperOrig.ToDictionary(pair => pair.Key, pair => pair.Value);

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
                foreach (var key in dict.Keys)
                {
                    var value = dict[key];

                    if (value is string s)
                    {
                        if (IsHex(s, out string hex))
                        {
                            var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                            _completionBase.ColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                        }
                        else if (s.StartsWith("colors"))
                        {
                            var color = s.Split('.').Last();

                            if (ColorToRgbMapperOrig.ContainsKey(color) && ColorToRgbMapperOrig.ContainsKey(key) == false)
                            {
                                foreach (var pairing in ColorToRgbMapperOrig.Where(
                                    p => p.Key.StartsWith(color)))
                                {
                                    _completionBase.ColorToRgbMapper[pairing.Key.Replace(color, key)] = pairing.Value;
                                }
                            }
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
                                    var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                    _completionBase.ColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                                }
                            }
                            else
                            {
                                if (IsHex(colorVariants[colorVariant], out string hex))
                                {
                                    var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                    _completionBase.ColorToRgbMapper[key + "-" + colorVariant] = $"{color.R},{color.G},{color.B}";
                                }
                            }

                        }
                    }
                }
            }

            if (config.OverridenValues.ContainsKey("screens") && GetDictionary(config.OverridenValues["screens"], out dict))
            {
                _completionBase.Modifiers.RemoveAll(m => Screen.Contains(m));

                _completionBase.Modifiers.AddRange(dict.Keys.ToList());
            }
            if (config.ExtendedValues.ContainsKey("screens") && GetDictionary(config.ExtendedValues["screens"], out dict))
            {
                // In case user changes / removes overriden or extended values
                if (config.OverridenValues.ContainsKey("screens") == false)
                {
                    // Clear out any other values and put in the defaults
                    _completionBase.Modifiers.Clear();
                    _completionBase.Modifiers.AddRange(ModifiersOrig);
                }

                _completionBase.Modifiers.AddRange(dict.Keys.Where(k => _completionBase.Modifiers.Contains(k) == false));
            }

            if (config.OverridenValues.ContainsKey("spacing") && GetDictionary(config.OverridenValues["spacing"], out dict))
            {
                _completionBase.Spacing.Clear();
                _completionBase.Spacing.AddRange(dict.Keys);
            }
            if (config.ExtendedValues.ContainsKey("spacing") && GetDictionary(config.ExtendedValues["spacing"], out dict))
            {
                // In case user changes / removes overriden or extended values
                if (config.OverridenValues.ContainsKey("spacing") == false)
                {
                    // Clear out any other values and put in the defaults
                    _completionBase.Spacing.Clear();
                    _completionBase.Spacing.AddRange(SpacingOrig);
                }

                _completionBase.Spacing.AddRange(dict.Keys.Where(k => _completionBase.Spacing.Contains(k) == false));
            }

            _areValuesDefault = false;
        }

        private void LoadIndividualConfigurationOverride(TailwindConfiguration config)
        {
            var applicable = ConfigurationValueToClassStems.Keys.Where(k => config.OverridenValues.ContainsKey(k));
            var oldApplicable = ConfigurationValueToClassStems.Keys.Where(k => config.OverridenValues.ContainsKey(k));

            if (ShouldRegenerate(applicable, oldApplicable, config))
            {
                _completionBase.Classes = ClassesOrig.ToList();
                var classesToRemove = new List<TailwindClass>();
                var classesToAdd = new List<TailwindClass>();

                _completionBase.CustomSpacings = new Dictionary<string, List<string>>();
                _completionBase.CustomColorMappers = new Dictionary<string, Dictionary<string, string>>();

                foreach (var key in applicable)
                {
                    var stems = ConfigurationValueToClassStems[key];

                    foreach (var stem in stems)
                    {
                        if (stem.Contains("{s}"))
                        {
                            if (GetDictionary(config.OverridenValues[key], out var dict))
                            {
                                _completionBase.CustomSpacings[stem.Replace("{s}", "{0}")] = dict.Keys.ToList();
                            }
                            else
                            {
                                _completionBase.CustomSpacings[stem.Replace("{s}", "{0}")] = new List<string>();
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
                            var s = stem;
                            if (stem.Contains("{*}"))
                            {
                                s = stem.Replace("-{*}", "");

                                classesToRemove.AddRange(_completionBase.Classes.Where(c => c.Name.StartsWith(s)));
                            }
                            else
                            {
                                classesToRemove.AddRange(_completionBase.Classes.Where(c => c.Name.StartsWith(stem) && c.Name.Replace($"{stem}-", "").Count(ch => ch == '-') == 0));
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
                                classesToAdd.Add(new TailwindClass()
                                {
                                    Name = s,
                                    SupportsBrackets = true
                                });
                            }
                        }
                    }
                }

                _completionBase.Classes.RemoveAll(c => classesToRemove.Contains(c));
                _completionBase.Classes.AddRange(classesToAdd);
            }
        }

        private void LoadIndividualConfigurationExtend(TailwindConfiguration config)
        {
            var applicable = ConfigurationValueToClassStems.Keys.Where(k => config.ExtendedValues.ContainsKey(k));
            var oldApplicable = ConfigurationValueToClassStems.Keys.Where(k => config.ExtendedValues.ContainsKey(k));

            if (ShouldRegenerate(applicable, oldApplicable, config))
            {
                var classesToAdd = new List<TailwindClass>();

                foreach (var key in applicable)
                {
                    var stems = ConfigurationValueToClassStems[key];

                    foreach (var stem in stems)
                    {
                        if (stem.Contains("{s}"))
                        {
                            var newSpacing = _completionBase.Spacing.ToList();
                            if (GetDictionary(config.ExtendedValues[key], out var dict))
                            {
                                newSpacing.AddRange(dict.Keys);
                                _completionBase.CustomSpacings[stem.Replace("{c}", "{0}")] = newSpacing.Distinct().ToList();
                            }
                            // no else because that would just be using the default spacing scheme
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
                            // no else because that would just be using the default color scheme
                        }
                        else
                        {
                            var s = stem;
                            if (stem.Contains("{*}"))
                            {
                                s = stem.Replace("-{*}", "");
                            }

                            if (GetDictionary(config.ExtendedValues[key], out var dict))
                            {
                                var insertStem = s;
                                // row-span and col-span are actually row and col internally
                                if (s.EndsWith("-span"))
                                {
                                    insertStem = s.Replace("-span", "");
                                }
                                classesToAdd.AddRange(dict.Keys
                                    .Where(k => _completionBase.Classes.Any(c => c.Name == $"{s}-{k}") == false)
                                    .Select(k =>
                                    {
                                        return new TailwindClass()
                                        {
                                            Name = $"{insertStem}-{k}"
                                        };
                                    }));
                            }
                        }
                    }
                }

                _completionBase.Classes.AddRange(classesToAdd);
            }
        }

        private bool ShouldRegenerate(IEnumerable<string> applicable, IEnumerable<string> oldApplicable, TailwindConfiguration config)
        {
            var regenerate = false;

            if (_lastConfig == null)
            {
                return true;
            }

            if (applicable.Count() != oldApplicable.Count() || applicable.Any(a => oldApplicable.Contains(a) == false))
            {
                regenerate = true;
            }
            // Same count, same keys, maybe content keys changed
            else
            {
                foreach (var key in applicable)
                {
                    // both are null / empty string
                    if (config.OverridenValues[key] == _lastConfig.OverridenValues[key])
                    {
                        continue;
                    }
                    if (GetDictionary(config.OverridenValues[key], out var dict1) && GetDictionary(config.OverridenValues[key], out var dict2))
                    {
                        if (dict1.Keys.Count == dict2.Keys.Count && dict1.Keys.All(k => dict2.ContainsKey(k)))
                        {
                            foreach (var k in dict1.Keys)
                            {
                                var firstIsDict = GetDictionary(dict1[k], out var d1);
                                var secondIsDict = GetDictionary(dict2[k], out var d2);
                                if (firstIsDict && secondIsDict)
                                {
                                    if (d1.Keys.Count != d2.Keys.Count || d1.Keys.Any(ke => d2.ContainsKey(ke) == false))
                                    {
                                        regenerate = true;
                                        break;
                                    }
                                }
                                else if (firstIsDict != secondIsDict)
                                {
                                    regenerate = true;
                                    break;
                                }
                            }
                            if (regenerate)
                            {
                                break;
                            }
                        }
                        else
                        {
                            regenerate = true;
                            break;
                        }
                    }
                    else
                    {
                        regenerate = true;
                        break;
                    }
                }
            }
            return regenerate;
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
                        var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                        newColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                    }
                    else if (s.StartsWith("colors"))
                    {
                        var color = s.Split('.').Last();

                        if (ColorToRgbMapperOrig.Any(c => c.Key.StartsWith(color)))
                        {
                            foreach (var pairing in ColorToRgbMapperOrig.Where(
                                p => p.Key.StartsWith(color)))
                            {
                                newColorToRgbMapper[pairing.Key.Replace(color, key)] = pairing.Value;
                            }
                        }
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
                                var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                newColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                            }
                        }
                        else
                        {
                            if (IsHex(colorVariants[colorVariant], out string hex))
                            {
                                var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                newColorToRgbMapper[key + "-" + colorVariant] = $"{color.R},{color.G},{color.B}";
                            }
                        }

                    }
                }
            }

            return newColorToRgbMapper;
        }
    }
}
