using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration.Descriptions;

namespace TailwindCSSIntellisense.Configuration
{
    /// <summary>
    /// Check ConfigurationClassGenerator.cs for the other half
    /// </summary>
    [Export]
    public sealed partial class CompletionConfiguration
    {
        internal Action<TailwindConfiguration> ConfigurationUpdated;

        [Import]
        internal ConfigFileParser Parser { get; set; }

        [Import]
        internal ConfigurationFileReloader Reloader { get; set; }

        [Import]
        internal ConfigFileScanner Scanner { get; set; }

        [Import(typeof(GeneratorAggregator))]
        internal GeneratorAggregator DescriptionGenerator { get; set; }

        private bool _areValuesDefault;
        private CompletionUtilities _completionBase;

        private List<string> ModifiersOrig { get; set; }
        private List<string> ScreenOrig { get; set; }
        private List<TailwindClass> ClassesOrig { get; set; }
        private Dictionary<string, string> ColorToRgbMapperOrig { get; set; }
        private Dictionary<string, string> SpacingMapperOrig { get; set; }

        internal TailwindConfiguration LastConfig { get; private set; }

        /// <summary>
        /// Initializes the configuration file (tailwind.config.js) for completion
        /// </summary>
        /// <param name="completionBase">The <see cref="CompletionUtilities"/> object calling the initialization</param>
        public async Task InitializeAsync(CompletionUtilities completionBase)
        {
            _completionBase = completionBase;
            ModifiersOrig = _completionBase.Modifiers.ToList();
            SpacingMapperOrig = _completionBase.SpacingMapper.ToDictionary(pair => pair.Key, pair => pair.Value);
            ClassesOrig = _completionBase.Classes.ToList();
            ScreenOrig = _completionBase.Screen.ToList();
            ColorToRgbMapperOrig = _completionBase.ColorToRgbMapper.ToDictionary(pair => pair.Key, pair => pair.Value);

            try
            {
                var config = await Parser.GetConfigurationAsync();
                LastConfig = config;
                _completionBase.Prefix = config.Prefix;
                LoadGlobalConfiguration(config);
                LoadIndividualConfigurationOverride(config);
                LoadIndividualConfigurationExtend(config);
                LoadPlugins(config);

                if (ConfigurationUpdated is not null)
                {
                    ConfigurationUpdated(config);
                }
            }
            catch (Exception ex)
            {
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS: Failed to load configuration file; check the 'Extensions' output window for more details");
                await ex.LogAsync();
            }

            await Reloader.InitializeAsync(this);
        }

        /// <summary>
        /// Adjusts classes to match a change in the configuration file
        /// </summary>
        public async Task ReloadCustomAttributesAsync()
        {
            if (_completionBase is not null && Scanner.HasConfigurationFile)
            {
                await VS.StatusBar.ShowMessageAsync("Reloading Tailwind CSS configuration");

                try
                {
                    var config = await Parser.GetConfigurationAsync();
                    LastConfig = config;
                    _completionBase.Prefix = config.Prefix;
                    LoadGlobalConfiguration(config);
                    _completionBase.Modifiers = _completionBase.Modifiers.Distinct().ToList();

                    LoadIndividualConfigurationOverride(config);
                    LoadIndividualConfigurationExtend(config);

                    LoadPlugins(config);

                    if (ConfigurationUpdated is not null)
                    {
                        ConfigurationUpdated(config);
                    }

                    await VS.StatusBar.ShowMessageAsync("Finished reloading Tailwind CSS configuration");
                }
                catch (Exception ex)
                {
                    await VS.StatusBar.ShowMessageAsync("Tailwind CSS: Failed to load configuration file; check the 'Extensions' output window for more details");
                    await ex.LogAsync();
                }
            }
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
