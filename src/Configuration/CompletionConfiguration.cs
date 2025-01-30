using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration.Descriptions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Configuration;

/// <summary>
/// Check ConfigurationClassGenerator.cs for the other half
/// </summary>
[Export]
public sealed partial class CompletionConfiguration
{
    internal Action ConfigurationUpdated;

    [Import]
    internal ConfigurationFileReloader Reloader { get; set; }

    [Import]
    internal ConfigFileScanner Scanner { get; set; }

    [Import(typeof(GeneratorAggregator))]
    internal GeneratorAggregator DescriptionGenerator { get; set; }

    [Import]
    internal ColorIconGenerator ColorIconGenerator { get; set; }

    [Import]
    internal SettingsProvider SettingsProvider { get; set; }

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

        var defaultCompletionConfiguration = _completionBase.GetUnsetCompletionConfiguration();

        ModifiersOrig = [.. defaultCompletionConfiguration.Modifiers];
        SpacingMapperOrig = defaultCompletionConfiguration.SpacingMapper.ToDictionary(pair => pair.Key, pair => pair.Value);
        ClassesOrig = [.. defaultCompletionConfiguration.Classes];
        ScreenOrig = [.. defaultCompletionConfiguration.Screen];
        ColorToRgbMapperOrig = defaultCompletionConfiguration.ColorToRgbMapper.ToDictionary(pair => pair.Key, pair => pair.Value);

        await ReloadCustomAttributesAsync(await SettingsProvider.GetSettingsAsync());

        await Reloader.InitializeAsync(this);
    }

    /// <summary>
    /// Shorthand for calling <see cref="ReloadCustomAttributesAsync(ConfigurationFile)"/> on all projects
    /// </summary>
    public async Task ReloadCustomAttributesAsync(TailwindSettings settings)
    {
        await ReloadCustomAttributesAsync(settings.ConfigurationFiles);
    }

    /// <summary>
    /// Shorthand for calling <see cref="ReloadCustomAttributesAsync(ConfigurationFile)"/> on all specified projects
    /// </summary>
    public async Task ReloadCustomAttributesAsync(List<ConfigurationFile> configurationFiles)
    {
        var failed = false;

        foreach (var configurationFile in configurationFiles)
        {
            var success = await ReloadCustomAttributesImplAsync(configurationFile);

            if (!success)
            {
                failed = true;
            }
        }

        if (ConfigurationUpdated is not null)
        {
            ConfigurationUpdated();
        }

        if (!failed)
        {
            await VS.StatusBar.ShowMessageAsync("Successfully reloaded Tailwind CSS configuration");
        }
    }

    /// <summary>
    /// Adjusts classes to match a change in the configuration file
    /// </summary>
    public async Task ReloadCustomAttributesAsync(ConfigurationFile configurationFile)
    {
        var success = await ReloadCustomAttributesImplAsync(configurationFile);

        if (ConfigurationUpdated is not null)
        {
            ConfigurationUpdated();
        }

        if (success)
        {
            await VS.StatusBar.ShowMessageAsync("Successfully reloaded Tailwind CSS configuration");
        }
    }

    /// <summary>
    /// Implementation for <see cref="ReloadCustomAttributesAsync(ConfigurationFile)"/>
    /// </summary>
    /// <returns>
    /// True if run successfully, false if an error occurred
    /// </returns>
    private async Task<bool> ReloadCustomAttributesImplAsync(ConfigurationFile configurationFile)
    {
        if (_completionBase is not null)
        {
            await VS.StatusBar.ShowMessageAsync("Reloading Tailwind CSS configuration");

            try
            {
                var config = await ConfigFileParser.GetConfigurationAsync(configurationFile.Path);

                var projectCompletionValues = _completionBase.GetCompletionConfigurationByConfigFilePath(configurationFile.Path);

                LastConfig = config;
                projectCompletionValues.Prefix = config.Prefix;
                LoadGlobalConfiguration(projectCompletionValues, config);
                projectCompletionValues.Modifiers = projectCompletionValues.Modifiers.Distinct().ToList();

                LoadIndividualConfigurationOverride(projectCompletionValues, config);
                LoadIndividualConfigurationExtend(projectCompletionValues, config);

                LoadPlugins(projectCompletionValues, config);
            }
            catch (Exception ex)
            {
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS: Failed to load configuration file; check the 'Extensions' output window for more details");
                await ex.LogAsync();
                return false;
            }
        }

        return true;
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
