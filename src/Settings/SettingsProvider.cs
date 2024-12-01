using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                ClassRegexHelper.GetTailwindSettings = GetSettingsAsync;
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
                        OnSaveTriggerFileExtensions = general.TailwindOnSaveTriggerFileExtensions.Split(';'),
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
                        using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        projectSettings = await JsonSerializer.DeserializeAsync<TailwindSettingsProjectOnly>(fs);
                    }
                    catch (Exception ex)
                    {
                        // Json file is malformed/empty

                        await VS.StatusBar.ShowMessageAsync("Tailwind CSS extension configuration file failed to load properly (check 'Extensions' output window for more details)");
                        await ex.LogAsync();

                        projectSettings = new TailwindSettingsProjectOnly()
                        {
                            ConfigurationFile = await FindExistingConfigurationFileAsync()
                        };

                        if (projectSettings.ConfigurationFile != null)
                        {
                            changed = true;
                        }
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

#pragma warning disable CS0612 // Type or member is obsolete
                // Backwards compatibility
                if (projectSettings.InputCssFile != null)
                {
                    projectSettings.BuildFiles = [
                        new BuildPair()
                        {
                            Input = projectSettings.InputCssFile,
                            Output = projectSettings.OutputCssFile
                        }
                    ];
                    projectSettings.InputCssFile = null;
                    projectSettings.OutputCssFile = null;
                    changed = true;
                }
#pragma warning restore CS0612 // Type or member is obsolete

                var inputFiles = new HashSet<string>();

                if (projectSettings.BuildFiles is not null)
                {
                    for (int i = 0; i < projectSettings.BuildFiles.Count; i++)
                    {
                        var buildPair = projectSettings.BuildFiles[i];

                        buildPair.Input = PathHelpers.GetAbsolutePath(activeProjectPath, buildPair.Input?.Trim());
                        buildPair.Output = PathHelpers.GetAbsolutePath(activeProjectPath, buildPair.Output?.Trim());
                        if (buildPair.Input == null || File.Exists(buildPair.Input) == false || inputFiles.Contains(buildPair.Input))
                        {
                            projectSettings.BuildFiles.Remove(buildPair);
                            i--;
                            changed = true;
                            continue;
                        }

                        inputFiles.Add(buildPair.Input);
                    }
                }

                returnSettings = new TailwindSettings()
                {
                    EnableTailwindCss = general.UseTailwindCss,
                    DefaultOutputCssName = general.TailwindOutputFileName.Trim(),
                    OnSaveTriggerFileExtensions = general.TailwindOnSaveTriggerFileExtensions.Split(';'),
                    BuildType = general.BuildProcessType,
                    BuildScript = general.BuildScript,
                    OverrideBuild = general.OverrideBuild,
                    TailwindConfigurationFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.ConfigurationFile?.Trim()),
                    BuildFiles = projectSettings.BuildFiles ?? [],
                    PackageConfigurationFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.PackageConfigurationFile?.Trim()),
                    AutomaticallyMinify = general.AutomaticallyMinify,
                    TailwindCliPath = general.TailwindCliPath,
                    UseCli = projectSettings.UseCli,
                    SortClassesType = general.ClassSortType,
                    CustomRegexes = projectSettings.CustomRegexes
                };

                _cachedSettings = returnSettings;
                _cacheValid = true;
            }

            if (returnSettings.TailwindConfigurationFile != null && File.Exists(returnSettings.TailwindConfigurationFile) == false)
            {
                returnSettings.TailwindConfigurationFile = null;
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
            var desiredProjectRoot = await GetDesiredConfigurationDirectoryAsync(settings.TailwindConfigurationFile);
            var copyBuildFilePair = new List<BuildPair>();

            foreach (var buildFilePair in settings.BuildFiles)
            {
                copyBuildFilePair.Add(new()
                {
                    Input = PathHelpers.GetRelativePath(buildFilePair.Input, projectRoot),
                    Output = PathHelpers.GetRelativePath(buildFilePair.Output, projectRoot)
                });
            }

            var projectSettings = new TailwindSettingsProjectOnly()
            {
                ConfigurationFile = PathHelpers.GetRelativePath(settings.TailwindConfigurationFile, projectRoot),
                BuildFiles = copyBuildFilePair,
                PackageConfigurationFile = PathHelpers.GetRelativePath(settings.PackageConfigurationFile, projectRoot),
                UseCli = settings.UseCli
            };

            if (projectSettings.ConfigurationFile == null && (projectSettings.BuildFiles == null || projectSettings.BuildFiles.Count == 0) && File.Exists(Path.Combine(projectRoot, ExtensionConfigFileName)))
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
                // If the configuration file is not located in the same project as the configuration file,
                // move it there. If the configuration file has not been changed, however, respect the user's
                // decision to keep it where it is.
                if (desiredProjectRoot != null && desiredProjectRoot != projectRoot &&
                    (_cachedSettings is null || _cachedSettings.TailwindConfigurationFile != settings.TailwindConfigurationFile))
                {
                    if (File.Exists(Path.Combine(projectRoot, ExtensionConfigFileName)))
                    {
                        try
                        {
                            File.Delete(Path.Combine(projectRoot, ExtensionConfigFileName));
                        }
                        catch (Exception ex)
                        {
                            await ex.LogAsync($"Tailwind CSS: Failed to delete old configuration file at ");
                        }
                    }

                    projectRoot = desiredProjectRoot;
                }

                using var fs = File.Open(Path.Combine(projectRoot, ExtensionConfigFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                _fileWritingTask = JsonSerializer.SerializeAsync(fs, projectSettings, options: new()
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
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

        public async Task<string> GetFilePathAsync()
        {
            return Path.Combine(await GetTailwindProjectDirectoryAsync(), ExtensionConfigFileName);
        }

        private async Task<string> GetDesiredConfigurationDirectoryAsync(string configPath)
        {
            var projects = await VS.Solutions.GetAllProjectsAsync();

            if (projects.Any() == false)
            {
                return null;
            }

            // Try to find the project that contains the config file, and return
            // its path
            string bestMatch = null;
            var numberOfSlashes = configPath.Replace(Path.DirectorySeparatorChar, '/').Count(c => c == '/');

            foreach (var p in projects)
            {
                var projectRoot = Path.GetDirectoryName(p.FullPath);

                var relativePath = PathHelpers.GetRelativePath(configPath, projectRoot);

                if (!configPath.StartsWith(".."))
                {
                    var numSlashes = relativePath.Count(c => c == Path.DirectorySeparatorChar);

                    if (numSlashes < numberOfSlashes)
                    {
                        bestMatch = projectRoot;
                        numberOfSlashes = numSlashes;
                    }
                }
            }

            return bestMatch;
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
                !settings.TailwindOnSaveTriggerFileExtensions.Equals(origSettings.OnSaveTriggerFileExtensions) ||
                settings.BuildProcessType != origSettings.BuildType ||
                settings.BuildScript != origSettings.BuildScript ||
                settings.OverrideBuild != origSettings.OverrideBuild ||
                settings.AutomaticallyMinify != origSettings.AutomaticallyMinify ||
                settings.TailwindCliPath != origSettings.TailwindCliPath ||
                settings.ClassSortType != origSettings.SortClassesType)
            {
                origSettings.EnableTailwindCss = settings.UseTailwindCss;
                origSettings.DefaultOutputCssName = settings.TailwindOutputFileName;
                origSettings.OnSaveTriggerFileExtensions = settings.TailwindOnSaveTriggerFileExtensions.Split(';');
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