using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Settings
{
    /// <summary>
    /// A singleton class to provide settings and provides an event which is raised when settings are changed
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SettingsProvider : IDisposable
    {
        public SettingsProvider()
        {
            General.Saved += GeneralSettingsChanged;
            VS.Events.SolutionEvents.OnAfterOpenFolder += InvalidateCache;
            VS.Events.SolutionEvents.OnAfterOpenProject += InvalidateCache;
        }

        [Import]
        public FileFinder FileFinder { get; set; }

        private const string ExtensionConfigFileName = "tailwind.extension.json";

        private Task _fileWritingTask;
        private TailwindSettings _cachedSettings;
        private bool _cacheValid;

        /// <summary>
        /// Event that is raised when the settings are changed.
        /// </summary>
        public Func<TailwindSettings, Task> OnSettingsChanged;

        /// <summary>
        /// Retrieves the TailwindCSSIntellisense settings asynchronously.
        /// </summary>
        /// <returns>The extension and project settings.</returns>
        public async Task<TailwindSettings> GetSettingsAsync()
        {
            TailwindSettings returnSettings;
            if (_cacheValid)
            {
                returnSettings = _cachedSettings;
            }
            else
            {
                var general = await General.GetLiveInstanceAsync();

                var activeProjectPath = await GetActiveProjectDirectoryAsync();

                if (activeProjectPath == null)
                {
                    return new TailwindSettings()
                    {
                        EnableTailwindCss = general.UseTailwindCss,
                        DefaultOutputCssName = general.TailwindOutputFileName.Trim()
                    };
                }

                TailwindSettingsProjectOnly projectSettings = null;

                var path = Path.Combine(activeProjectPath, ExtensionConfigFileName);

                if (File.Exists(path))
                {
                    if (_fileWritingTask != null)
                    {
                        if (_fileWritingTask.IsCompleted)
                        {
                            _fileWritingTask = null;
                        }
                        else
                        {
                            await _fileWritingTask;
                        }
                    }
                    try
                    {
                        using (var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            projectSettings = await JsonSerializer.DeserializeAsync<TailwindSettingsProjectOnly>(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Json file is malformed/empty

                        await VS.StatusBar.ShowMessageAsync("TailwindCSS extension configuration file failed to load properly (check 'Extensions' output window for more details)");
                        await ex.LogAsync();
                    }
                }

                returnSettings = new TailwindSettings()
                {
                    EnableTailwindCss = general.UseTailwindCss,
                    DefaultOutputCssName = general.TailwindOutputFileName.Trim(),
                    TailwindConfigurationFile = GetAbsolutePath(activeProjectPath, projectSettings?.ConfigurationFile?.Trim()),
                    TailwindCssFile = GetAbsolutePath(activeProjectPath, projectSettings?.InputCssFile?.Trim()),
                    TailwindOutputCssFile = GetAbsolutePath(activeProjectPath, projectSettings?.OutputCssFile?.Trim())
                };

                _cachedSettings = returnSettings;
                _cacheValid = true;
            }

            var changed = false;
            if (returnSettings.TailwindConfigurationFile != null && File.Exists(returnSettings.TailwindConfigurationFile) == false)
            {
                returnSettings.TailwindConfigurationFile = null;
                changed = true;
            }
            if (returnSettings.TailwindCssFile != null && File.Exists(returnSettings.TailwindCssFile) == false)
            {
                returnSettings.TailwindCssFile = null;
                changed = true;
            }
            if (returnSettings.TailwindOutputCssFile != null && File.Exists(returnSettings.TailwindOutputCssFile) == false)
            {
                returnSettings.TailwindOutputCssFile = null;
                changed = true;
            }

            if (changed)
            {
                await OverrideSettingsAsync(returnSettings);
            }

            return returnSettings;
        }

        /// <summary>
        /// Overrides the TailwindCSSIntellisense settings asynchronously.
        /// </summary>
        /// <param name="settings">The settings to override with.</param>
        public async Task OverrideSettingsAsync(TailwindSettings settings)
        {
            // Prevents two tasks from writing to the same file at the same time
            if (_fileWritingTask != null)
            {
                if (_fileWritingTask.IsCompleted)
                {
                    _fileWritingTask = null;
                }
                else
                {
                    await _fileWritingTask;
                }
            }

            var projectRoot = await GetActiveProjectDirectoryAsync();
            var projectSettings = new TailwindSettingsProjectOnly()
            {
                ConfigurationFile = GetRelativePath(settings.TailwindConfigurationFile, projectRoot),
                InputCssFile = GetRelativePath(settings.TailwindCssFile, projectRoot),
                OutputCssFile = GetRelativePath(settings.TailwindOutputCssFile, projectRoot)
            };

            using (var fs = File.Open(Path.Combine(projectRoot, ExtensionConfigFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                _fileWritingTask = JsonSerializer.SerializeAsync(fs, projectSettings);
                await _fileWritingTask;
            }

            _cachedSettings = settings;

            if (OnSettingsChanged != null)
            {
                await OnSettingsChanged(settings);
            }
        }

        public void Dispose()
        {
            General.Saved -= GeneralSettingsChanged;
            VS.Events.SolutionEvents.OnAfterOpenFolder -= InvalidateCache;
        }

        private async Task<string> GetActiveProjectDirectoryAsync()
        {
            var project = await VS.Solutions.GetActiveProjectAsync();

            if (project == null)
            {
                return await FileFinder.GetCurrentMiscellaneousProjectPathAsync();
            }
            else
            {
                foreach (var p in await VS.Solutions.GetAllProjectsAsync())
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(project.FullPath), "tailwind.extension.json")))
                    {
                        return Path.GetDirectoryName(p.FullPath);
                    }
                }

                return Path.GetDirectoryName(project.FullPath);
            }
        }

        private void GeneralSettingsChanged(General settings)
        {
            var origSettings = ThreadHelper.JoinableTaskFactory.Run(GetSettingsAsync);

            if (settings.UseTailwindCss != origSettings.EnableTailwindCss ||
                settings.TailwindOutputFileName != origSettings.DefaultOutputCssName)
            {
                origSettings.EnableTailwindCss = settings.UseTailwindCss;
                origSettings.DefaultOutputCssName = settings.TailwindOutputFileName;

                ThreadHelper.JoinableTaskFactory.Run(async () => await OnSettingsChanged(origSettings));
            }

            _cachedSettings = origSettings;
        }

        // https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
        private string GetRelativePath(string file, string folder)
        {
            if (file is null)
            {
                return null;
            }

            Uri pathUri = new Uri(file);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private string GetAbsolutePath(string dir, string rel)
        {
            if (rel is null)
            {
                return null;
            }
            // Folders must end in a slash
            if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                dir += Path.DirectorySeparatorChar;
            }

            var dirUri = new Uri(dir);
            var absUri = new Uri(dirUri, rel);

            return Uri.UnescapeDataString(absUri.AbsolutePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void InvalidateCache(string file)
        {
            _cacheValid = false;
        }

        private void InvalidateCache(Project project)
        {
            _cacheValid = false;
        }
    }
}