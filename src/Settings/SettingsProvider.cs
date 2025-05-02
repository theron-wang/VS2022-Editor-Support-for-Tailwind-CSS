using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Settings;

/// <summary>
/// A singleton class to provide settings and provides an event which is raised when settings are changed
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
public sealed class SettingsProvider : IDisposable
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

    [Import]
    public ConfigFileScanner ConfigFileScanner { get; set; }

    [Import]
    public ProjectConfigurationManager ProjectConfigurationManager { get; set; }

    private const string ExtensionConfigFileName = "tailwind.extension.json";

    private Task _fileWritingTask;
    private TailwindSettings _cachedSettings;
    private bool _cacheValid;

    /// <summary>
    /// Event that is raised when the settings are changed.
    /// </summary>
    public Func<TailwindSettings, Task> OnSettingsChanged;

    public void RefreshSettings()
    {
        _cacheValid = false;
    }

    /// <summary>
    /// Not recommended to use this method. Use <see cref="GetSettingsAsync"/> instead.
    /// </summary>
    public TailwindSettings GetSettings()
    {
        return ThreadHelper.JoinableTaskFactory.Run(GetSettingsAsync);
    }

    /// <summary>
    /// Retrieves extension settings asynchronously.
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

                    var file = await ConfigFileScanner.TryFindConfigurationFileAsync();

                    projectSettings = new TailwindSettingsProjectOnly()
                    {
                        ConfigurationFiles = [new() { Path = file }]
                    };

                    if (file != null)
                    {
                        changed = true;
                    }
                }
            }
            else
            {
                var file = await ConfigFileScanner.TryFindConfigurationFileAsync();

                projectSettings = new TailwindSettingsProjectOnly()
                {
                    ConfigurationFiles = [new() { Path = file }]
                };

                if (file != null)
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

            if (projectSettings.ConfigurationFile != null)
            {
                projectSettings.ConfigurationFiles = [new() { Path = projectSettings.ConfigurationFile }];
                projectSettings.ConfigurationFile = null;
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

            if (projectSettings.ConfigurationFiles is not null)
            {
                for (int i = 0; i < projectSettings.ConfigurationFiles.Count; i++)
                {
                    var config = projectSettings.ConfigurationFiles[i];
                    config.Path = PathHelpers.GetAbsolutePath(activeProjectPath, config.Path?.Trim());

                    if (string.IsNullOrWhiteSpace(config.Path) || File.Exists(config.Path) == false)
                    {
                        projectSettings.ConfigurationFiles.Remove(config);
                        i--;
                        changed = true;
                    }
                    else if (Path.GetExtension(config.Path) == ".css")
                    {
                        projectSettings.ConfigurationFiles.Remove(config);
                        i--;

                        if (projectSettings.BuildFiles is not null && !projectSettings.BuildFiles.Any(b => b.Input.Equals(config.Path, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            projectSettings.BuildFiles.Add(new() { Input = config.Path });
                        }
                        changed = true;
                    }
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
                ConfigurationFiles = projectSettings.ConfigurationFiles ?? [],
                BuildFiles = projectSettings.BuildFiles ?? [],
                PackageConfigurationFile = PathHelpers.GetAbsolutePath(activeProjectPath, projectSettings?.PackageConfigurationFile?.Trim()),
                AutomaticallyMinify = general.AutomaticallyMinify,
                TailwindCliPath = general.TailwindCliPath,
                UseCli = projectSettings.UseCli,
                SortClassesType = general.ClassSortType,
                CustomRegexes = projectSettings.CustomRegexes
            };

            await ProjectConfigurationManager.OnSettingsChangedAsync(returnSettings);

            _cachedSettings = returnSettings;

            _cacheValid = true;
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

        if (settings.ConfigurationFiles is not null)
        {
            for (int i = 0; i < settings.ConfigurationFiles.Count; i++)
            {
                var config = settings.ConfigurationFiles[i];

                if (string.IsNullOrWhiteSpace(config.Path) || File.Exists(config.Path) == false)
                {
                    settings.ConfigurationFiles.Remove(config);
                    i--;
                }
            }
        }

        var defaultConfigFile = settings.ConfigurationFiles.FirstOrDefault()?.Path ??
            settings.ConfigurationFiles.FirstOrDefault()?.Path;

        string oldDefaultConfigFile = null;
        if (_cachedSettings is not null)
        {
            oldDefaultConfigFile = _cachedSettings.ConfigurationFiles.FirstOrDefault()?.Path ??
            _cachedSettings.ConfigurationFiles.FirstOrDefault()?.Path;
        }

        var projectRoot = await GetTailwindProjectDirectoryAsync();
        var desiredProjectRoot = await GetDesiredConfigurationDirectoryAsync(defaultConfigFile);
        var copyBuildFilePair = new List<BuildPair>();

        foreach (var buildFilePair in settings.BuildFiles)
        {
            copyBuildFilePair.Add(new()
            {
                Input = PathHelpers.GetRelativePath(buildFilePair.Input, projectRoot),
                Output = string.IsNullOrWhiteSpace(buildFilePair.Output) ? "" : PathHelpers.GetRelativePath(buildFilePair.Output, projectRoot)
            });
        }

        List<ConfigurationFile> configurationFiles = [..
            settings.ConfigurationFiles
            .Where(cf => settings.BuildFiles.Any(b => !b.Input.Equals(cf.Path, StringComparison.InvariantCultureIgnoreCase)))
            .Select(cf =>
            {
                return new ConfigurationFile()
                {
                    Path = PathHelpers.GetRelativePath(cf.Path?.Trim(), projectRoot)
                };
            })
        ];

        var projectSettings = new TailwindSettingsProjectOnly()
        {
            ConfigurationFiles = configurationFiles.Count > 0 ? configurationFiles : null,
            BuildFiles = copyBuildFilePair,
            PackageConfigurationFile = PathHelpers.GetRelativePath(settings.PackageConfigurationFile, projectRoot),
            UseCli = settings.UseCli
        };

        if ((projectSettings.ConfigurationFiles is null || projectSettings.ConfigurationFiles.Count == 0)
            && (projectSettings.BuildFiles is null || projectSettings.BuildFiles.Count == 0))
        {
            // Delete if empty, do not save if it doesn't exist yet

            if (File.Exists(Path.Combine(projectRoot, ExtensionConfigFileName)))
            {
                try
                {
                    File.Delete(Path.Combine(projectRoot, ExtensionConfigFileName));
                }
                catch
                {

                }
            }
        }
        else
        {
            // If the configuration file is not located in the same project as the configuration file,
            // move it there. If the configuration file has not been changed, however, respect the user's
            // decision to keep it where it is.
            if (desiredProjectRoot != null && desiredProjectRoot != projectRoot &&
                (_cachedSettings is null || (defaultConfigFile is not null && defaultConfigFile != oldDefaultConfigFile)))
            {
                if (File.Exists(Path.Combine(projectRoot, ExtensionConfigFileName)))
                {
                    try
                    {
                        File.Delete(Path.Combine(projectRoot, ExtensionConfigFileName));
                    }
                    catch (Exception ex)
                    {
                        await ex.LogAsync($"Tailwind CSS: Failed to delete old configuration file at ${Path.Combine(projectRoot, ExtensionConfigFileName)}");
                    }
                }

                projectRoot = desiredProjectRoot;
            }

            using var fs = File.Open(Path.Combine(projectRoot, ExtensionConfigFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            _fileWritingTask = JsonSerializer.SerializeAsync(fs, projectSettings, options: new()
            {
                WriteIndented = true
            });
            await _fileWritingTask;
        }

        _cachedSettings = settings;

        await ProjectConfigurationManager.OnSettingsChangedAsync(settings);

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

        if (projects.Any() == false || string.IsNullOrWhiteSpace(configPath))
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
                    DefaultConfigurationFileNames.Names.Any(n =>
                        File.Exists(
                            Path.Combine(
                                Path.GetDirectoryName(p.FullPath), n)
                            )
                        )
                    )?.FullPath ??
                projects.First().FullPath);
        }
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