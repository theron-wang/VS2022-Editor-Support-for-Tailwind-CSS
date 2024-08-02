using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Build
{
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

        private string _cssFilePath;
        private string _cssOutputFilePath;
        private string _configPath;

        private bool? _hasScript = null;

        private string _packageJsonPath;

        private bool _initialized;
        private bool _subscribed;
        private bool _minify;
        private Process _process;
        private Process _otherProcess;

        private TailwindSettings _settings;

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
                _subscribed = true;
            }
            _initialized = true;
        }

        public void Dispose()
        {
            VS.Events.BuildEvents.ProjectBuildStarted -= OnBuild;
            VS.Events.DocumentEvents.Saved -= OnFileSave;
            SettingsProvider.OnSettingsChanged -= SettingsChangedAsync;

            _process?.Dispose();
            _otherProcess?.Dispose();

            _process = null;
            _otherProcess = null;
        }

        internal bool AreProcessesActive()
        {
            return IsProcessActive(_process) || IsProcessActive(_otherProcess);
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
        internal async Task SettingsChangedAsync(TailwindSettings settings)
        {
            var processActive = AreProcessesActive();
            await SetFilePathsAsync(settings);

            // Restart process since SetFilePathsAsync stops the process
            if (processActive)
            {
                StartProcess(_minify);
            }
        }

        /// <summary>
        /// Reloads if package.json has been modified + starts build process (OnBuild)
        /// </summary>
        internal void OnFileSave(string filePath)
        {
            if (filePath.Equals(_packageJsonPath, StringComparison.InvariantCultureIgnoreCase) && !string.IsNullOrWhiteSpace(_settings.BuildScript))
            {
                _hasScript = null;
                if (AreProcessesActive())
                {
                    EndProcess();
                    StartProcess(_settings.AutomaticallyMinify);
                }
            }

            var extension = Path.GetExtension(filePath);
            string[] extensions = new[] { ".css", ".html", ".cshtml", ".razor", ".js" };
            if (_settings.BuildType == BuildProcessOptions.OnSave && extensions.Contains(extension))
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Building..."));
                StartProcess(_settings.AutomaticallyMinify);
            }
        }

        internal void OnBuild(Project project = null)
        {
            if (_settings.BuildType != BuildProcessOptions.Manual)
            {
                StartProcess(_settings.AutomaticallyMinify);
            }
        }

        /// <summary>
        /// Starts the build process
        /// </summary>
        internal void StartProcess(bool minify = false)
        {
            if (Scanner.HasConfigurationFile == false || _cssFilePath == null || _settings.BuildType == BuildProcessOptions.None)
            {
                return;
            }

            _minify = minify;

            if (_hasScript == null)
            {
                (var exists, var fileName) = ThreadHelper.JoinableTaskFactory.Run(() => PackageJsonReader.ScriptExistsAsync(_settings.BuildScript));

                _packageJsonPath = fileName;
                _hasScript = exists;
            }

            var needOtherProcess = _settings.OverrideBuild == false && _hasScript.Value && string.IsNullOrWhiteSpace(_settings.BuildScript) == false;
            var dir = Path.GetDirectoryName(_configPath);
            var cssFile = GetRelativePath(_cssFilePath, dir);
            var outputFile = GetRelativePath(_cssOutputFilePath, dir);

            var processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "cmd",
                WorkingDirectory = dir
            };

            if (_settings.BuildType == BuildProcessOptions.OnSave)
            {
                // Is the default process active?
                #region Default process
                if (IsProcessActive(_process))
                {
                    if (_settings.OverrideBuild == false || _hasScript == false || string.IsNullOrWhiteSpace(_settings.BuildScript))
                    {
                        _process.StandardInput.WriteLine($"{GetCommand()} -i \"{cssFile}\" -o \"{outputFile}\" {(minify ? "--minify" : "")}");
                    }
                    else if (_settings.OverrideBuild)
                    {
                        _process.StandardInput.WriteLine($"cd {Path.GetDirectoryName(_packageJsonPath)} & npm run {_settings.BuildScript}");
                    }
                }
                else
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build started..."));

                    _process = Process.Start(processInfo);

                    _process.Exited += (s, e) =>
                    {
                        ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build stopped"));
                        _process.Dispose();
                        _process = null;
                    };

                    if (_settings.OverrideBuild == false || _hasScript == false || string.IsNullOrWhiteSpace(_settings.BuildScript))
                    {
                        _process.StandardInput.WriteLine($"{GetCommand()} -i \"{cssFile}\" -o \"{outputFile}\" {(minify ? "--minify" : "")}");
                    }
                    else if (_settings.OverrideBuild)
                    {
                        _process.StandardInput.WriteLine($"cd {Path.GetDirectoryName(_packageJsonPath)} & npm run {_settings.BuildScript}");
                    }

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _process.OutputDataReceived += OutputDataReceived;
                    _process.ErrorDataReceived += OutputDataReceived;
                }
                #endregion
                #region Secondary process
                if (needOtherProcess)
                {
                    if (IsProcessActive(_otherProcess))
                    {
                        _otherProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");
                    }
                    else
                    {
                        ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Running '{_settings.BuildScript}' script..."));

                        processInfo.WorkingDirectory = Path.GetDirectoryName(_packageJsonPath);
                        _otherProcess = Process.Start(processInfo);

                        _otherProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");

                        _otherProcess.BeginOutputReadLine();
                        _otherProcess.BeginErrorReadLine();
                        _otherProcess.OutputDataReceived += OutputDataReceived;
                        _otherProcess.ErrorDataReceived += OutputDataReceived;
                    }
                }
                #endregion
            }
            else
            {
                #region Default process

                if (!IsProcessActive(_process))
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build started..."));

                    _process = Process.Start(processInfo);

                    _process.Exited += (s, e) =>
                    {
                        ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("Tailwind CSS: Build stopped"));
                        _process.Dispose();
                        _process = null;
                    };

                    if (_settings.OverrideBuild == false || _hasScript == false || string.IsNullOrWhiteSpace(_settings.BuildScript))
                    {
                        _process.StandardInput.WriteLine($"{GetCommand()} -i \"{cssFile}\" -o \"{outputFile}\" {(_settings.BuildType == BuildProcessOptions.Default ? "--watch" : "")} {(minify ? "--minify" : "")} & exit");
                    }
                    else if (_settings.OverrideBuild)
                    {
                        _process.StandardInput.WriteLine($"cd {Path.GetDirectoryName(_packageJsonPath)} & npm run {_settings.BuildScript}");

                        _process.StandardInput.Flush();
                        _process.StandardInput.Close();
                    }

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _process.OutputDataReceived += OutputDataReceived;
                    _process.ErrorDataReceived += OutputDataReceived;
                }

                #endregion
                #region Secondary process

                if (IsProcessActive(_otherProcess) == false && needOtherProcess)
                {
                    ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Running '{_settings.BuildScript}' script..."));

                    processInfo.WorkingDirectory = Path.GetDirectoryName(_packageJsonPath);
                    _otherProcess = Process.Start(processInfo);

                    _otherProcess.StandardInput.WriteLine($"npm run {_settings.BuildScript}");
                    _otherProcess.StandardInput.Flush();
                    _otherProcess.StandardInput.Close();

                    _otherProcess.BeginOutputReadLine();
                    _otherProcess.BeginErrorReadLine();
                    _otherProcess.OutputDataReceived += OutputDataReceived;
                    _otherProcess.ErrorDataReceived += OutputDataReceived;
                }

                #endregion
            }
        }

        /// <summary>
        /// Ends the build process
        /// </summary>
        internal void EndProcess()
        {
            if (IsProcessActive(_process))
            {
                // Tailwind --watch keeps building even with kill; use \x3 to say Ctrl+c to stop
                // https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
                _process.StandardInput.WriteLine("\x3");
                _process.StandardInput.Close();
                _process.Kill();
                _process.Dispose();
                _process = null;
            }
            if (IsProcessActive(_otherProcess))
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync($"Tailwind CSS: Build script '{_settings.BuildScript}' stopped"));

                // Tailwind --watch keeps building even with kill; use \x3 to say Ctrl+c to stop
                // https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
                _otherProcess.StandardInput.WriteLine("\x3");
                _otherProcess.StandardInput.Close();
                _otherProcess.Kill();
                _otherProcess.Dispose();
                _otherProcess = null;
            }
        }

        private string GetCommand()
        {
            if (string.IsNullOrWhiteSpace(_settings.TailwindCliPath))
            {
                return "npx tailwindcss";
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
            _hasScript = null;

            if (string.IsNullOrEmpty(settings.TailwindCssFile))
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
                        _cssFilePath = file;
                        break;
                    }
                }
            }
            else
            {
                _cssFilePath = settings.TailwindCssFile;
            }

            if (_cssFilePath != null && string.IsNullOrEmpty(settings.TailwindOutputCssFile))
            {
                _cssOutputFilePath = Path.Combine(Path.GetDirectoryName(_cssFilePath), string.Format(settings.DefaultOutputCssName.EndsWith(".css") ? settings.DefaultOutputCssName : settings.DefaultOutputCssName + ".css", Path.GetFileNameWithoutExtension(_cssFilePath)));
            }
            else
            {
                _cssOutputFilePath = settings.TailwindOutputCssFile;
            }

            _configPath = await Scanner.FindConfigurationFilePathAsync();

            EndProcess();
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            if (_hasScript == true)
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
            }
        }

        private async Task LogErrorAsync(string error)
        {
            await VS.StatusBar.ShowMessageAsync("Tailwind CSS: An error occurred while building. Check the build output window for more details.");
            await WriteToBuildPaneAsync(error);
        }

        private async Task LogSuccessAsync(string seconds)
        {
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

                    if (text.Contains("@tailwind"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
        private string GetRelativePath(string file, string folder)
        {
            if (folder is null)
            {
                return folder;
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
    }
}
