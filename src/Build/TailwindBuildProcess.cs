using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Build;

/// <summary>
/// Provides methods to start, stop, and manage the process to build Tailwind
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class TailwindBuildProcess : IDisposable
{
    [Import]
    internal ConfigFileScanner Scanner { get; set; }
    [Import]
    internal SettingsProvider SettingsProvider { get; set; }
    [Import]
    internal PackageJsonReader PackageJsonReader { get; set; }
    [Import]
    internal ProjectConfigurationManager ProjectConfigurationManager { get; set; }

    private Dictionary<string, string> _inputToOutputFile = [];

    private bool _initialized;
    private bool _subscribed;
    private bool _minify;

    private bool _startingBuilds;

    private readonly Dictionary<string, string> _configsToPackageJsons = [];

    private readonly Dictionary<string, Process> _outputFileToProcesses = [];
    private Process _secondaryProcess;

    private TailwindSettings _settings;
    private General _general;

    private Guid _buildWindowGuid;

    /// <summary>
    /// Initializes the class; subscribes to events, sets up members
    /// </summary>
    /// <param name="newProjectOpened">A <see cref="bool"/> which should only be set to <see langword="true"/> in <see cref="TailwindCSSIntellisensePackage.InitializeAsync"/></param>
    public async Task InitializeAsync(bool newProjectOpened = false)
    {
        if (newProjectOpened)
        {
            _initialized = false;

            EndProcess();
        }

        if (_initialized)
        {
            return;
        }

        var settings = await SettingsProvider.GetSettingsAsync();

        await SetFilePathsAsync(settings);

        // Prevent more than one subscription when projects are changed
        if (_subscribed == false)
        {
            VS.Events.BuildEvents.ProjectBuildStarted += OnBuild;
            VS.Events.DocumentEvents.Saved += OnFileSave;
            SettingsProvider.OnSettingsChanged += SettingsChangedAsync;
            General.Saved += OnGeneralSettingsChanged;
            _general = await General.GetLiveInstanceAsync();
            _subscribed = true;
        }
        _initialized = true;
    }

    public void Dispose()
    {
        VS.Events.BuildEvents.ProjectBuildStarted -= OnBuild;
        VS.Events.DocumentEvents.Saved -= OnFileSave;
        SettingsProvider.OnSettingsChanged -= SettingsChangedAsync;
        General.Saved -= OnGeneralSettingsChanged;

        foreach (var process in _outputFileToProcesses.Values)
        {
            process?.Dispose();
        }
        _outputFileToProcesses.Clear();
        _secondaryProcess?.Dispose();

        _secondaryProcess = null;
    }

    internal bool AreProcessesActive()
    {
        return _outputFileToProcesses.Values.Any(IsProcessActive) || IsProcessActive(_secondaryProcess);
    }

    /// <summary>
    /// A <see cref="bool"/> representing whether or not the build process is currently active
    /// </summary>
    private bool IsProcessActive(Process process)
    {
        if (_settings?.BuildType == BuildProcessOptions.None)
        {
            return false;
        }
        else
        {
            return process != null && process.HasExited == false;
        }
    }

    /// <summary>
    /// Binds to <see cref="SettingsProvider.OnSettingsChanged"/> which updates the file paths and restarts the process if needed
    /// </summary>
    /// <param name="settings">The settings to pass on to <see cref="SetFilePathsAsync(TailwindSettings)"/></param>
    private async Task SettingsChangedAsync(TailwindSettings settings)
    {
        var processActive = AreProcessesActive();
        await SetFilePathsAsync(settings);

        // Restart process since SetFilePathsAsync stops the process
        if (processActive)
        {
            BuildAll(_minify);
        }
    }

    private void OnGeneralSettingsChanged(General general)
    {
        _general = general;
    }

    /// <summary>
    /// Reloads if package.json has been modified + starts build process (OnBuild)
    /// </summary>
    private void OnFileSave(string filePath)
    {
        if (Path.GetFileName(filePath).Equals("package.json", StringComparison.InvariantCultureIgnoreCase) && !string.IsNullOrWhiteSpace(_settings.BuildScript))
        {
            var keys = _configsToPackageJsons.Keys.Where(f => f.Equals(filePath, StringComparison.InvariantCultureIgnoreCase)).ToList();

            foreach (var key in keys)
            {
                _configsToPackageJsons.Remove(key);
            }

            if (AreProcessesActive())
            {
                EndProcess();
                BuildAll(_settings.AutomaticallyMinify);
            }
        }

        var extension = Path.GetExtension(filePath);
        if (_settings.BuildType == BuildProcessOptions.OnSave && _settings.OnSaveTriggerFileExtensions.Contains(extension))
        {
            ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Building..."));
            BuildAll(_settings.AutomaticallyMinify);
        }
    }

    private void OnBuild(Project project = null)
    {
        if (_settings.BuildType != BuildProcessOptions.Manual && _settings.BuildType != BuildProcessOptions.ManualJIT)
        {
            BuildAll(_settings.AutomaticallyMinify);
        }
    }

    /// <summary>
    /// Starts the build process
    /// </summary>
    internal void BuildAll(bool minify = false)
    {
        if (_settings.ConfigurationFiles.Count == 0 || _inputToOutputFile == null || _inputToOutputFile.Count == 0 || _settings.BuildType == BuildProcessOptions.None)
        {
            return;
        }

        _startingBuilds = true;

        try
        {
            foreach (var pair in _inputToOutputFile)
            {
                BuildOne(pair, minify);
            }
        }
        finally
        {
            _startingBuilds = false;
        }
    }

    private void BuildOne(KeyValuePair<string, string> pair, bool minify = false)
    {
        ProjectCompletionValues config;
        try
        {
            // V4
            config = ProjectConfigurationManager.GetCompletionConfigurationByConfigFilePath(pair.Key);
        }
        catch
        {
            config = ProjectConfigurationManager.GetCompletionConfigurationByFilePath(pair.Key);
        }

        _minify = minify;

        var hasScript = false;
        var packageJsonPath = "";

        if (!_configsToPackageJsons.ContainsKey(config.FilePath))
        {
            (hasScript, packageJsonPath) = ThreadHelper.JoinableTaskFactory.Run(() => PackageJsonReader.ScriptExistsAsync(config.FilePath, _settings.BuildScript));
            _configsToPackageJsons[config.FilePath] = packageJsonPath;
        }
        else
        {
            packageJsonPath = _configsToPackageJsons[config.FilePath];
            hasScript = packageJsonPath is not null;
        }

        var needOtherProcess = _settings.OverrideBuild == false && hasScript && string.IsNullOrWhiteSpace(_settings.BuildScript) == false;
        var dir = Path.GetDirectoryName(config.FilePath);
        var configFile = Path.GetFileName(config.FilePath);

        var inputFile = PathHelpers.GetRelativePath(pair.Key, dir);
        var outputFile = PathHelpers.GetRelativePath(pair.Value, dir);

        if (_settings.BuildType == BuildProcessOptions.OnSave)
        {
            // Are all default processes active?
            #region Default process
            if (_outputFileToProcesses.TryGetValue(outputFile, out var process) && IsProcessActive(process))
            {
                if (_settings.OverrideBuild == false || !hasScript || string.IsNullOrWhiteSpace(_settings.BuildScript))
                {
                    if (config.Version == TailwindVersion.V3)
                    {
                        process.StandardInput.WriteLine($"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" -c \"{configFile}\" {(minify ? "--minify" : "")}");
                    }
                    else
                    {
                        process.StandardInput.WriteLine($"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" {(minify ? "--minify" : "")}");
                    }
                }
                else if (_settings.OverrideBuild)
                {
                    _secondaryProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");
                }
            }
            else
            {
                if (_outputFileToProcesses.Count == 0)
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build started..."));
                }
                else if (process is not null && !IsProcessActive(process))
                {
                    _outputFileToProcesses.Remove(outputFile);
                }

                if (_settings.OverrideBuild == false || !hasScript || string.IsNullOrWhiteSpace(_settings.BuildScript))
                {
                    process = CreateAndStartProcess(GetProcessStartInfo(dir));

                    if (config.Version == TailwindVersion.V3)
                    {
                        process.StandardInput.WriteLine($"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" -c \"{configFile}\" {(minify ? "--minify" : "")}");
                    }
                    else
                    {
                        process.StandardInput.WriteLine($"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" {(minify ? "--minify" : "")}");
                    }

                    PostSetupProcess(process);

                    _outputFileToProcesses[outputFile] = process;
                }
                else if (_settings.OverrideBuild)
                {
                    CreateAndSetAndStartSecondaryProcess(GetProcessStartInfo(Path.GetDirectoryName(packageJsonPath)));

                    _secondaryProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");

                    PostSetupProcess(_secondaryProcess);
                }
            }
            #endregion
            #region Secondary process
            if (needOtherProcess)
            {
                if (IsProcessActive(_secondaryProcess))
                {
                    _secondaryProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");
                }
                else
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Running '{_settings.BuildScript}' script..."));

                    var processInfo = GetProcessStartInfo(Path.GetDirectoryName(packageJsonPath));
                    CreateAndSetAndStartSecondaryProcess(processInfo);

                    _secondaryProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");

                    PostSetupProcess(_secondaryProcess);
                }
            }
            #endregion
        }
        else
        {
            #region Default process

            if (_outputFileToProcesses.Count == 0 || _startingBuilds || !_outputFileToProcesses.Values.All(IsProcessActive))
            {
                if (_outputFileToProcesses.Count == 0)
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build started..."));
                }

                if (_settings.OverrideBuild == false || !hasScript || string.IsNullOrWhiteSpace(_settings.BuildScript))
                {
                    if (_outputFileToProcesses.TryGetValue(outputFile, out var process) && IsProcessActive(process))
                    {
                        return;
                    }

                    ProcessStartInfo processInfo;

                    // We need powershell to run --watch, otherwise the output file will not update.
                    // https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/111
                    if (_settings.BuildType == BuildProcessOptions.Default || _settings.BuildType == BuildProcessOptions.ManualJIT)
                    {
                        processInfo = GetProcessStartInfo(dir, true);
                        processInfo.Arguments = "-Command ";
                    }
                    else
                    {
                        processInfo = GetProcessStartInfo(dir);
                        processInfo.Arguments = "/c ";
                    }
                    
                    if (config.Version == TailwindVersion.V3)
                    {
                        processInfo.Arguments += $"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" -c \"{configFile}\" {(minify ? "--minify" : "")} {(_settings.BuildType == BuildProcessOptions.Default || _settings.BuildType == BuildProcessOptions.ManualJIT ? "--watch" : "& exit")}";
                    }
                    else
                    {
                        processInfo.Arguments += $"{GetCommand(config)} -i \"{inputFile}\" -o \"{outputFile}\" {(minify ? "--minify" : "")} {(_settings.BuildType == BuildProcessOptions.Default || _settings.BuildType == BuildProcessOptions.ManualJIT ? "--watch" : "& exit")}";
                    }

                    process = CreateAndStartProcess(processInfo);
                    PostSetupProcess(process);

                    _outputFileToProcesses[outputFile] = process;
                }
                else if (_settings.OverrideBuild)
                {
                    var processInfo = GetProcessStartInfo(Path.GetDirectoryName(packageJsonPath));
                    processInfo.Arguments = $"/c npm run {_settings.BuildScript}";

                    CreateAndSetAndStartSecondaryProcess(processInfo);

                    PostSetupProcess(_secondaryProcess);
                    _secondaryProcess.StandardInput.Flush();
                    _secondaryProcess.StandardInput.Close();
                }
            }

            #endregion
            #region Secondary process

            if (IsProcessActive(_secondaryProcess) == false && needOtherProcess)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Running '{_settings.BuildScript}' script..."));

                var processInfo = GetProcessStartInfo(Path.GetDirectoryName(packageJsonPath));
                processInfo.Arguments = $"/c npm run {_settings.BuildScript}";

                CreateAndSetAndStartSecondaryProcess(processInfo);

                PostSetupProcess(_secondaryProcess);

                _secondaryProcess.StandardInput.Flush();
                _secondaryProcess.StandardInput.Close();
            }

            #endregion
        }
    }

    /// <summary>
    /// Ends the build process
    /// </summary>
    internal void EndProcess()
    {
        foreach (var process in _outputFileToProcesses.Values)
        {
            if (IsProcessActive(process))
            {
                // Tailwind --watch keeps building even with kill; use \x3 to say Ctrl+c to stop
                // https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
                process.StandardInput.WriteLine("\x3");
                process.StandardInput.Close();
                process.Kill();
            }
        }

        var any = _outputFileToProcesses.Any();

        _outputFileToProcesses.Clear();

        if (any)
        {
            ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build stopped"));
        }

        if (IsProcessActive(_secondaryProcess))
        {
            ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Build script '{_settings.BuildScript}' stopped"));

            // Tailwind --watch keeps building even with kill; use \x3 to say Ctrl+c to stop
            // https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
            _secondaryProcess.StandardInput.WriteLine("\x3");
            _secondaryProcess.StandardInput.Close();
            _secondaryProcess.Kill();
            _secondaryProcess = null;
        }
    }

    private string GetCommand(ProjectCompletionValues project)
    {
        if (string.IsNullOrWhiteSpace(_settings.TailwindCliPath))
        {
            if (project.Version == TailwindVersion.V3)
            {
                return "npx tailwindcss";
            }
            else
            {
                return "npx @tailwindcss/cli";
            }
        }
        return _settings.TailwindCliPath;
    }

    /// <summary>
    /// Resets the file paths for the config file, output file, and input file 
    /// </summary>
    /// <param name="settings">The settings to consume</param>
    private async Task SetFilePathsAsync(TailwindSettings settings)
    {
        _settings = settings;
        _configsToPackageJsons.Clear();

        var buildFiles = settings.BuildFiles;

        if (buildFiles is null || buildFiles.Count == 0)
        {
            // Check the smallest css files first since tailwind css files (should) be small
            var cssFiles = (await Scanner.FileFinder.GetCssFilesAsync()).OrderBy(f => new FileInfo(f).Length).ToList();

            if (cssFiles.Count == 0)
            {
                return;
            }

            foreach (var file in cssFiles)
            {
                if (await IsFileTailwindCssAsync(file))
                {
                    buildFiles = [];
                    buildFiles.Add(new() { Input = file });
                    break;
                }
            }
        }
        else
        {
            buildFiles = buildFiles.Select(b => new BuildPair() { Input = b.Input, Output = b.Output }).ToList();
        }

        var buildFilesFiltered = buildFiles
            .Where(f => !string.IsNullOrEmpty(f.Input) && File.Exists(f.Input))
            .Select(f =>
            {
                f.Output = string.IsNullOrEmpty(f.Output) ?
                    Path.Combine(Path.GetDirectoryName(f.Input), string.Format(settings.DefaultOutputCssName.EndsWith(".css") ? settings.DefaultOutputCssName : settings.DefaultOutputCssName + ".css", Path.GetFileNameWithoutExtension(f.Input))) : f.Output;
                return f;
            });

        _inputToOutputFile = [];

        foreach (var pair in buildFilesFiltered)
        {
            _inputToOutputFile[pair.Input] = pair.Output;
        }

        EndProcess();
    }

    private void OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null)
        {
            return;
        }

        if (_settings.OverrideBuild || sender == _secondaryProcess)
        {
            if (sender is Process process && e.Data.TrimEnd(' ', '>', '\\') == process.StartInfo.WorkingDirectory.TrimEnd('\\'))
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Build script '{_settings.BuildScript}' finished"));
            }
            if (e.Data.ToLower().Contains("error") || e.Data.Contains("ERR!"))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await LogErrorAsync("Tailwind CSS: " + e.Data);
                });
            }
            // usually messages with backslashes (path) and > are the lines where the command is being written to the command prompt
            else if (string.IsNullOrWhiteSpace(e.Data) == false && e.Data.Contains("Microsoft") == false && (e.Data.Contains('>') == false || e.Data.Contains('\\') == false))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await VS.StatusBar.ShowMessageAsync($"Tailwind CSS: {e.Data}");
                    await WriteToBuildPaneAsync($"Tailwind CSS: {e.Data}");
                });
            }
        }
        else
        {
            if (e.Data.Contains("Error"))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await LogErrorAsync("Tailwind CSS: " + e.Data);
                });
            }
            else if (e.Data.Contains("Done in"))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await LogSuccessAsync(e.Data.Replace("Done in", "").Trim().Trim('.'));
                });
            }
            else if (_general.VerboseBuild)
            {
                if (string.IsNullOrWhiteSpace(e.Data) == false && e.Data.Contains("Microsoft") == false)
                {
                    // Regex: remove ANSI escape codes
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await WriteToBuildPaneAsync($"Tailwind CSS: {Regex.Replace(e.Data.Trim(), @"\x1B\[[0-9;]*[mK]", "")}");
                    });
                }
            }
        }
    }

    private async Task LogErrorAsync(string error)
    {
        await VS.StatusBar.ShowMessageAsync("Tailwind CSS: An error occurred while building. Check the build output window for more details.");
        await WriteToBuildPaneAsync(error);
    }

    private async Task LogSuccessAsync(string seconds)
    {
        // Remove ANSI color codes
        seconds = Regex.Replace(seconds, @"\x1B\[[0-9;]*[mK]", "");

        await VS.StatusBar.ShowMessageAsync($"Tailwind CSS: Build succeeded in {seconds} at {DateTime.Now.ToLongTimeString()}.");

        await WriteToBuildPaneAsync($"Tailwind CSS: Build succeeded in {seconds} at {DateTime.Now.ToLongTimeString()}.");
    }

    private async Task WriteToBuildPaneAsync(string message)
    {
        var windowPane = await VS.Windows.GetOutputWindowPaneAsync(Community.VisualStudio.Toolkit.Windows.VSOutputWindowPane.Build);

        await windowPane.ActivateAsync();
        if (windowPane == null)
        {
            if (_buildWindowGuid != default)
            {
                windowPane = await VS.Windows.GetOutputWindowPaneAsync(_buildWindowGuid);
            }
            else
            {
                windowPane = await VS.Windows.CreateOutputWindowPaneAsync("Build");
                _buildWindowGuid = windowPane.Guid;
            }
        }

        await windowPane.WriteLineAsync(message);
    }

    private async Task<bool> IsFileTailwindCssAsync(string filePath)
    {
        using (var fs = File.OpenRead(filePath))
        {
            using (var reader = new StreamReader(fs))
            {
                var text = await reader.ReadToEndAsync();

                if (text.Contains("@tailwind") || (text.Contains("@import") && text.Contains("tailwindcss")))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static ProcessStartInfo GetProcessStartInfo(string dir, bool powershell = false)
    {
        return new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            FileName = powershell ? "powershell" : "cmd",
            WorkingDirectory = dir,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private void CreateAndSetAndStartSecondaryProcess(ProcessStartInfo startInfo)
    {
        _secondaryProcess = Process.Start(startInfo);
        _secondaryProcess.EnableRaisingEvents = true;

        _secondaryProcess.Exited += (s, e) =>
        {
            ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build stopped"));
            _secondaryProcess?.Dispose();
            _secondaryProcess = null;
        };
    }

    private Process CreateAndStartProcess(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        process.EnableRaisingEvents = true;

        process.Exited += (s, e) =>
        {
            var proc = (Process)s;

            proc?.Dispose();
            string keyToRemove = null;
            foreach (var kvp in _outputFileToProcesses)
            {
                if (kvp.Value == proc)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
            {
                _outputFileToProcesses.Remove(keyToRemove);
            }

            if (_outputFileToProcesses.Count > 0)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Build stopped ({_outputFileToProcesses.Count} processes left)"));
            }
        };

        return process;
    }

    private void PostSetupProcess(Process process)
    {
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.OutputDataReceived += OutputDataReceived;
        process.ErrorDataReceived += OutputDataReceived;
    }
}
