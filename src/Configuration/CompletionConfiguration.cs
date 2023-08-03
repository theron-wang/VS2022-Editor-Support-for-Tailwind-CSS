using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.Configuration
{
    [Export]
    public sealed partial class CompletionConfiguration
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
        private List<string> ScreenOrig { get; set; }
        private List<TailwindClass> ClassesOrig { get; set; }
        private Dictionary<string, string> ColorToRgbMapperOrig { get; set; }

        /// <summary>
        /// Initializes the configuration file (tailwind.config.js) for completion
        /// </summary>
        /// <param name="completionBase">The <see cref="CompletionUtilities"/> object calling the initialization</param>
        public async Task InitializeAsync(CompletionUtilities completionBase)
        {
            _completionBase = completionBase;
            ModifiersOrig = _completionBase.Modifiers.ToList();
            SpacingOrig = _completionBase.Spacing.ToList();
            ClassesOrig = _completionBase.Classes.ToList();
            ScreenOrig = _completionBase.Screen.ToList();
            ColorToRgbMapperOrig = _completionBase.ColorToRgbMapper.ToDictionary(pair => pair.Key, pair => pair.Value);

            var config = await Parser.GetConfigurationAsync();
            LoadGlobalConfiguration(config);
            LoadIndividualConfigurationOverride(config);
            LoadIndividualConfigurationExtend(config);

            await Reloader.InitializeAsync(this);
        }

        /// <summary>
        /// Adjusts classes to match a change in the configuration file
        /// </summary>
        public async Task ReloadCustomAttributesAsync()
        {
            if (Scanner.HasConfigurationFile)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowProgressAsync("Reloading TailwindCSS configuration", 1, 2);

                var config = await Parser.GetConfigurationAsync();
                LoadGlobalConfiguration(config);
                _completionBase.Spacing = _completionBase.Spacing.Distinct().ToList();
                _completionBase.Modifiers = _completionBase.Modifiers.Distinct().ToList();

                LoadIndividualConfigurationOverride(config);
                LoadIndividualConfigurationExtend(config);

                await VS.StatusBar.ShowProgressAsync("", 2, 2);
                await VS.StatusBar.ShowMessageAsync("Finished reloading TailwindCSS configuration");
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }
        }

        private static bool IsHex(object value, out string hex)
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
