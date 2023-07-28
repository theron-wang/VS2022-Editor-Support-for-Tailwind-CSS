using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.Completions
{
    [Export]
    public sealed class CompletionConfiguration
    {
        [Import]
        internal ConfigFileParser Parser { get; set; }

        [Import]
        internal ConfigurationFileReloader Reloader { get; set; }

        [Import]
        internal ConfigFileScanner Scanner { get; set; }

        private bool _areValuesDefault;
        private CompletionUtilities _completionBase;

        private List<string> ModifiersOrig { get; set; }
        private List<string> SpacingOrig { get; set; }
        private Dictionary<string, string> ColorToRgbMapperOrig { get; set; }
        private List<string> Screen { get; set; } = new List<string>() { "sm", "md", "lg", "xl", "2xl" };

        /// <summary>
        /// Initializes the configuration file (tailwind.config.js) for completion
        /// </summary>
        /// <param name="completionBase">The <see cref="CompletionUtilities"/> object calling the initialization</param>
        public async Task InitializeAsync(CompletionUtilities completionBase)
        {
            _completionBase = completionBase;
            ModifiersOrig = _completionBase.Modifiers.ToList();
            SpacingOrig = _completionBase.Spacing.ToList();
            ColorToRgbMapperOrig = _completionBase.ColorToRgbMapper.ToDictionary(pair => pair.Key, pair => pair.Value);

            await LoadCustomAttributesAsync();

            await Reloader.InitializeAsync(this);
        }

        /// <summary>
        /// Adjusts classes to match a change in the configuration file
        /// </summary>
        public async Task ReloadCustomAttributesAsync()
        {
            if (Scanner.HasConfigurationFile)
            {
                try
                {
                    await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                    await VS.StatusBar.ShowProgressAsync("Reloading Tailwind CSS configuration", 1, 2);
                    await LoadCustomAttributesAsync();
                    _completionBase.Spacing = _completionBase.Spacing.Distinct().ToList();
                    _completionBase.Modifiers = _completionBase.Modifiers.Distinct().ToList();

                    await VS.StatusBar.ShowProgressAsync("", 2, 2);
                    await VS.StatusBar.ShowMessageAsync("Finished reloading Tailwind CSS configuration");
                }
                catch (Exception e)
                {
                    await e.LogAsync();
                    await VS.StatusBar.ShowMessageAsync("An error occurred while loading Tailwind CSS configuration: check the 'Extensions' output window for more details");
                }
                finally
                {
                    await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
                }
            }
        }

        /// <summary>
        /// Method called by <see cref="ReloadCustomAttributesAsync"/> to reconfigure classes
        /// </summary>
        /// <returns></returns>
        private async Task LoadCustomAttributesAsync()
        {
            var config = await Parser.GetConfigurationAsync();

            _completionBase.Modifiers = ModifiersOrig.ToList();
            _completionBase.Spacing = SpacingOrig.ToList();
            _completionBase.ColorToRgbMapper = ColorToRgbMapperOrig.ToDictionary(pair => pair.Key, pair => pair.Value);

            if (config is null && _areValuesDefault == false)
            {
                // Reset to default; either user has changed/deleted config file or there is none
                return;
            }

            _areValuesDefault = true;

            bool isHex(object value, out string hex)
            {
                var content = value.ToString().Trim('#').ToUpper();
                var hexLetters = "ABCDEF";
                if (content.All(c => char.IsNumber(c) || hexLetters.Contains(c)))
                {
                    if (content.Length == 6 || content.Length == 8)
                    {
                        hex = content.Substring(0, 6);
                        return true;
                    }
                    else if (content.Length == 3)
                    {
                        hex = content;
                        return true;
                    }
                }

                hex = null;
                return false;
            }

            if (config.OverridenValues.ContainsKey("colors") && GetDictionary(config.OverridenValues["colors"], out Dictionary<string, object> dict))
            {
                var newColorToRgbMapper = new Dictionary<string, string>();

                foreach (var key in dict.Keys)
                {
                    var value = dict[key];

                    if (value is string s)
                    {
                        if (isHex(s, out string hex))
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
                                if (isHex(colorVariants[colorVariant], out string hex))
                                {
                                    var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                    newColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                                }
                            }
                            else
                            {
                                if (isHex(colorVariants[colorVariant], out string hex))
                                {
                                    var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                    newColorToRgbMapper[key + "-" + colorVariant] = $"{color.R},{color.G},{color.B}";
                                }
                            }

                        }
                    }
                }

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
                        if (isHex(s, out string hex))
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
                                if (isHex(colorVariants[colorVariant], out string hex))
                                {
                                    var color = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                                    _completionBase.ColorToRgbMapper[key] = $"{color.R},{color.G},{color.B}";
                                }
                            }
                            else
                            {
                                if (isHex(colorVariants[colorVariant], out string hex))
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

        private bool GetDictionary(object value, out Dictionary<string, object> dict)
        {
            if (value is Dictionary<string, object> values)
            {
                dict = values;
                return true;
            }
            dict = null;
            return false;
        }
    }
}
