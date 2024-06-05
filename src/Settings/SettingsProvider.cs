using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                General.Saved += GeneralSettingsChanged;
                VS.Events.SolutionEvents.OnAfterOpenFolder += InvalidateCacheAndSettingsChanged;
                VS.Events.SolutionEvents.OnAfterOpenProject += InvalidateCacheAndSettingsChanged;
                VS.Events.DocumentEvents.Saved += OnFileSaved;
            });
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
            var changed = false;
            if (_cacheValid)
            {
                returnSettings = _cachedSettings;
            }
            else
            {
                var general = await General.GetLiveInstanceAsync();

                var activeProjectPath = await GetTailwindProjectDirectoryAsync();

                if (activeProjectPath == null)
                {
                    return new TailwindSettings()
                    {
                        EnableTailwindCss = general.UseTailwindCss,
                        DefaultOutputCssName = general.TailwindOutputFileName.Trim(),
                        BuildType = general.BuildProcessType,
                        BuildScript = general.BuildScript,
                        OverrideBuild = general.OverrideBuild,
                        AutomaticallyMinify = general.AutomaticallyMinify,
                        TailwindCliPath = general.TailwindCliPath,
                        SortClassesType = general.ClassSortType
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

                        await VS.StatusBar.ShowMessageAsync("Tailwind CSS extension configuration file failed to load properly (check 'Extensions' output window for more details)");
                        await ex.LogAsync();
                    }
                }
                else
                {
                    projectSettings = new TailwindSettingsProjectOnly()
                    {
                        ConfigurationFile = await FindExistingConfigurationFileAsync()
                    };

                    if (projectSettings.ConfigurationFile != null)
                    {
                        changed = true;
                    }
                }

                returnSettings = new TailwindSettings()
                {
                    EnableTailwindCss = general.UseTailwindCss,
                    DefaultOutputCssName = general.TailwindOutputFileName.Trim(),
                    BuildType = general.BuildProcessType,
                    BuildScript = general.BuildScript,
                    OverrideBuild = general.OverrideBuild,
                    TailwindConfigurationFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.ConfigurationFile?.Trim()),
                    TailwindCssFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.InputCssFile?.Trim()),
                    TailwindOutputCssFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.OutputCssFile?.Trim()),
                    PackageConfigurationFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.PackageConfigurationFile?.Trim()),
                    AutomaticallyMinify = general.AutomaticallyMinify,
                    TailwindCliPath = general.TailwindCliPath,
                    UseCli = projectSettings.UseCli,
                    SortClassesType = general.ClassSortType
                };

                _cachedSettings = returnSettings;
                _cacheValid = true;
            }

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

            var projectRoot = await GetTailwindProjectDirectoryAsync();
            var projectSettings = new TailwindSettingsProjectOnly()
            {
                ConfigurationFile = PathHelpers.GetRelativePath(settings.TailwindConfigurationFile, projectRoot),
                InputCssFile = PathHelpers.GetRelativePath(settings.TailwindCssFile, projectRoot),
                OutputCssFile = PathHelpers.GetRelativePath(settings.TailwindOutputCssFile, projectRoot),
                PackageConfigurationFile = PathHelpers.GetRelativePath(settings.PackageConfigurationFile, projectRoot),
                UseCli = settings.UseCli
            };

            if (projectSettings.ConfigurationFile == null && projectSettings.InputCssFile == null && projectSettings.OutputCssFile == null && File.Exists(Path.Combine(projectRoot, ExtensionConfigFileName)))
            {
                try
                {
                    File.Delete(Path.Combine(projectRoot, ExtensionConfigFileName));
                }
                catch
                {

                }
            }
            else
            {
                using var fs = File.Open(Path.Combine(projectRoot, ExtensionConfigFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
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
            VS.Events.SolutionEvents.OnAfterOpenFolder -= InvalidateCacheAndSettingsChanged;
            VS.Events.SolutionEvents.OnAfterOpenProject -= InvalidateCacheAndSettingsChanged;
            VS.Events.DocumentEvents.Saved -= OnFileSaved;
        }

        private async Task<string> GetTailwindProjectDirectoryAsync()
        {
            var projects = await VS.Solutions.GetAllProjectsAsync();

            if (projects == null || projects.Any() == false)
            {
                return await FileFinder.GetCurrentMiscellaneousProjectPathAsync();
            }
            else
            {
                foreach (var p in projects)
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(p.FullPath), ExtensionConfigFileName)))
                    {
                        return Path.GetDirectoryName(p.FullPath);
                    }
                }

                return Path.GetDirectoryName(
                    projects.FirstOrDefault(p =>
                        File.Exists(
                            Path.Combine(
                                Path.GetDirectoryName(p.FullPath), "tailwind.config.js")
                            )
                        )?.FullPath ?? 
                    projects.First().FullPath);
            }
        }

        private async Task<string> FindExistingConfigurationFileAsync()
        {
            var paths = await FileFinder.GetJavascriptFilesAsync();

            return paths.FirstOrDefault(p => Path.GetFileName(p).Equals("tailwind.config.js", StringComparison.InvariantCultureIgnoreCase));
        }

        private void GeneralSettingsChanged(General settings)
        {
            var origSettings = ThreadHelper.JoinableTaskFactory.Run(GetSettingsAsync);

            if (settings.UseTailwindCss != origSettings.EnableTailwindCss ||
                settings.TailwindOutputFileName != origSettings.DefaultOutputCssName ||
                settings.BuildProcessType != origSettings.BuildType ||
                settings.BuildScript != origSettings.BuildScript ||
                settings.OverrideBuild != origSettings.OverrideBuild ||
                settings.AutomaticallyMinify != origSettings.AutomaticallyMinify ||
                settings.TailwindCliPath != origSettings.TailwindCliPath ||
                settings.ClassSortType != origSettings.SortClassesType)
            {
                origSettings.EnableTailwindCss = settings.UseTailwindCss;
                origSettings.DefaultOutputCssName = settings.TailwindOutputFileName;
                origSettings.BuildType = settings.BuildProcessType;
                origSettings.BuildScript = settings.BuildScript;
                origSettings.OverrideBuild = settings.OverrideBuild;
                origSettings.AutomaticallyMinify = settings.AutomaticallyMinify;
                origSettings.TailwindCliPath = settings.TailwindCliPath;
                origSettings.SortClassesType = settings.ClassSortType;

                ThreadHelper.JoinableTaskFactory.Run(async () => await OnSettingsChanged(origSettings));
            }

            _cachedSettings = origSettings;
        }

        private void InvalidateCacheAndSettingsChanged(string file)
        {
            _cacheValid = false;
            _cachedSettings = null;
            if (OnSettingsChanged != null)
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    var settings = await GetSettingsAsync();
                    await OnSettingsChanged(settings);
                });
            }
        }

        private void InvalidateCacheAndSettingsChanged(Project project)
        {
            _cacheValid = false;
            _cachedSettings = null;
            if (OnSettingsChanged != null)
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    var settings = await GetSettingsAsync();
                    await OnSettingsChanged(settings);
                });
            }
        }

        private void OnFileSaved(string file)
        {
            if (Path.GetFileName(file) == ExtensionConfigFileName)
            {
                InvalidateCacheAndSettingsChanged(file);
            }
        }
    }
}