using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
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

    private void OnFileSave(string file)
    {
        var configFile = _settings.ConfigurationFiles.FirstOrDefault(c => c.Path.Equals(file, StringComparison.InvariantCultureIgnoreCase));
        if (configFile is not null)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(() => _config.ReloadCustomAttributesAsync(configFile)).FireAndForget();
        }
    }

    private async Task OnSettingsChangedAsync(TailwindSettings settings)
    {
        _settings = settings;
        var added = settings.ConfigurationFiles.Except(_settings.ConfigurationFiles).ToList();

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
