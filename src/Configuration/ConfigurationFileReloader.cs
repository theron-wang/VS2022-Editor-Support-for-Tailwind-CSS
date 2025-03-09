using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Configuration;

/// <summary>
/// Reloads Intellisense when the Tailwind CSS configuration file is modified
/// </summary>
[Export]
public sealed class ConfigurationFileReloader : IDisposable
{
    [Import]
    internal ConfigFileScanner Scanner { get; set; }
    [Import]
    internal SettingsProvider SettingsProvider { get; set; }

    private bool _subscribed;

    private CompletionConfiguration _config;
    private TailwindSettings _settings;

    private readonly Dictionary<string, HashSet<ConfigurationFile>> _importToConfigurationFiles = [];

    /// <summary>
    /// Initializes the class to subscribe to relevant events
    /// </summary>
    /// <param name="config">The <see cref="CompletionConfiguration"/> object calling the initialization</param>
    /// <param name="fromNewProject">Should only be called from <see cref="TailwindCSSIntellisensePackage"/> to reset the configuration file path</param>
    public async Task InitializeAsync(CompletionConfiguration config, bool fromNewProject = false)
    {
        _config = config;

        if (_subscribed == false)
        {
            VS.Events.DocumentEvents.Saved += OnFileSave;
            SettingsProvider.OnSettingsChanged += OnSettingsChangedAsync;

            _subscribed = true;
        }

        if (fromNewProject)
        {
            _settings = await SettingsProvider.GetSettingsAsync();
            await _config.ReloadCustomAttributesAsync(_settings);
        }
    }

    public void AddImport(string import, ConfigurationFile config)
    {
        if (_importToConfigurationFiles.TryGetValue(import.ToLower(), out var values))
        {
            values.Add(config);
        }
        else
        {
            _importToConfigurationFiles.Add(import.ToLower(), [config]);
        }
    }

    private void OnFileSave(string file)
    {
        List<ConfigurationFile> configFiles = [];

        var configFile = _settings.ConfigurationFiles.FirstOrDefault(c => c.Path.Equals(file, StringComparison.InvariantCultureIgnoreCase));
        
        if (configFile is not null)
        {
            configFiles.Add(configFile);
        }

        if (_importToConfigurationFiles.TryGetValue(file.ToLower(), out var values))
        {
            configFiles.AddRange(values);
        }

        foreach (var config in configFiles)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(() => _config.ReloadCustomAttributesAsync(config)).FireAndForget();
        }
    }

    private async Task OnSettingsChangedAsync(TailwindSettings settings)
    {
        _settings = settings;
        var added = settings.ConfigurationFiles.Except(_settings.ConfigurationFiles).ToList();

        foreach (var values in _importToConfigurationFiles.Values)
        {
            values.RemoveWhere(v => !settings.ConfigurationFiles.Contains(v));
        }

        if (added.Count > 0)
        {
            await _config.ReloadCustomAttributesAsync(settings);
        }
    }

    public void Dispose()
    {
        VS.Events.DocumentEvents.Saved -= OnFileSave;
        SettingsProvider.OnSettingsChanged -= OnSettingsChangedAsync;
    }
}
