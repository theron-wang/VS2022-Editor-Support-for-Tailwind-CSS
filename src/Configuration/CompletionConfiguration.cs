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
[PartCreationPolicy(CreationPolicy.Shared)]
public sealed partial class CompletionConfiguration
{
    internal Action ConfigurationUpdated;

    [Import]
    internal ConfigurationFileReloader Reloader { get; set; }

    [Import(typeof(GeneratorAggregator))]
    internal GeneratorAggregator DescriptionGenerator { get; set; }

    [Import]
    internal ColorIconGenerator ColorIconGenerator { get; set; }

    [Import]
    internal DirectoryVersionFinder DirectoryVersionFinder { get; set; }

    [Import]
    public ProjectConfigurationManager ProjectConfigurationManager { get; set; }

    internal TailwindConfiguration LastConfig { get; private set; }

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

        if (!failed && configurationFiles.Count > 0)
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
        if (ProjectConfigurationManager is not null)
        {
            await VS.StatusBar.ShowMessageAsync("Reloading Tailwind CSS configuration");

            try
            {
                var version = await DirectoryVersionFinder.GetTailwindVersionAsync(configurationFile.Path);

                var config = await ConfigFileParser.GetConfigurationAsync(configurationFile.Path, version);

                foreach (var imports in config.Imports)
                {
                    Reloader.AddImport(imports, configurationFile);
                }

                var projectCompletionValues = ProjectConfigurationManager.GetCompletionConfigurationByConfigFilePath(configurationFile.Path);

                projectCompletionValues.ApplicablePaths = [.. config.ContentPaths.Where(c => !c.StartsWith("!"))];
                projectCompletionValues.NotApplicablePaths = [.. config.ContentPaths.Where(c => c.StartsWith("!")).Select(c => c.Trim('!'))];

                LastConfig = config;
                projectCompletionValues.Prefix = config.Prefix;
                LoadGlobalConfiguration(projectCompletionValues, config);
                projectCompletionValues.Variants = [.. projectCompletionValues.Variants.Distinct()];

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
