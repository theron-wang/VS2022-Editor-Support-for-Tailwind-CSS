using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Configuration
{
    /// <summary>
    /// Reloads Intellisense when the TailwindCSS configuration file is modified
    /// </summary>
    [Export]
    public sealed class ConfigurationFileReloader : IDisposable
    {
        [Import]
        internal ConfigFileScanner Scanner { get; set; }
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        private string _fileName;
        private bool _subscribed;

        private CompletionConfiguration _config;

        /// <summary>
        /// Initializes the class to subscribe to relevant events
        /// </summary>
        /// <param name="config">The <see cref="CompletionConfiguration"/> object calling the initialization</param>
        /// <param name="fromNewProject">Should only be called from <see cref="TailwindCSSIntellisensePackage"/> to reset the configuration file path</param>
        public async Task InitializeAsync(CompletionConfiguration config, bool fromNewProject = false)
        {
            _config = config;
            _fileName = await Scanner.FindConfigurationFilePathAsync(fromNewProject);

            if (_subscribed == false)
            {
                VS.Events.DocumentEvents.Saved += OnFileSave;
                SettingsProvider.OnSettingsChanged += OnSettingsChangedAsync;

                _subscribed = true;
            }

            if (fromNewProject)
            {
                await _config.ReloadCustomAttributesAsync();
            }
        }

        private void OnFileSave(string file)
        {
            if (file.Equals(_fileName, StringComparison.InvariantCultureIgnoreCase))
            {
                ThreadHelper.JoinableTaskFactory.Run(_config.ReloadCustomAttributesAsync);
            }
        }

        private async Task OnSettingsChangedAsync(TailwindSettings settings)
        {
            if (settings.TailwindConfigurationFile != await Scanner.FindConfigurationFilePathAsync())
            {
                _fileName = await Scanner.FindConfigurationFilePathAsync(true);
                await _config.ReloadCustomAttributesAsync();
            }
        }

        public void Dispose()
        {
            VS.Events.DocumentEvents.Saved -= OnFileSave;
            SettingsProvider.OnSettingsChanged -= OnSettingsChangedAsync;
        }
    }
}
