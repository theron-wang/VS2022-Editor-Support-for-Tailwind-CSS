using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
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

        private string _cssFilePath;
        private string _cssOutputFilePath;
        private string _configPath;

        private bool _initialized;
        private bool _subscribed;
        private Process _process;

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
                VS.Events.BuildEvents.ProjectBuildStarted += StartProcess;
                SettingsProvider.OnSettingsChanged += SettingsChangedAsync;
                _subscribed = true;
            }
            _initialized = true;
        }

        public void Dispose()
        {
            VS.Events.BuildEvents.ProjectBuildStarted -= StartProcess;
            SettingsProvider.OnSettingsChanged -= SetFilePathsAsync;
        }

        /// <summary>
        /// A <see cref="bool"/> representing whether or not the build process is currently active
        /// </summary>
        internal bool IsProcessActive => _process != null && _process.HasExited == false;

        /// <summary>
        /// Binds to <see cref="SettingsProvider.OnSettingsChanged"/> which updates the file paths and restarts the process if needed
        /// </summary>
        /// <param name="settings">The settings to pass on to <see cref="SetFilePathsAsync(TailwindSettings)"/></param>
        internal async Task SettingsChangedAsync(TailwindSettings settings)
        {
            var processActive = IsProcessActive;
            await SetFilePathsAsync(settings);

            // Restart process since SetFilePathsAsync stops the process
            if (processActive)
            {
                StartProcess();
            }
        }

        /// <summary>
        /// Starts the build process
        /// </summary>
        internal void StartProcess(Project project = null)
        {
            if (_cssFilePath == null)
            {
                return;
            }

            if (IsProcessActive == false && Scanner.HasConfigurationFile)
            {
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

                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("TailwindCSS: Build started..."));
                _process = Process.Start(processInfo);
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                _process.StandardInput.WriteLine($"npx tailwindcss -i \"{cssFile}\" -o \"{outputFile}\" --watch & exit");

                _process.OutputDataReceived += OutputDataReceived;
                _process.ErrorDataReceived += OutputDataReceived;
            }
        }

        /// <summary>
        /// Ends the build process
        /// </summary>
        internal void EndProcess()
        {
            if (IsProcessActive)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => WriteToBuildPaneAsync("TailwindCSS: Build stopped"));

                // Tailwind --watch keeps building even with kill; use \x3 to say Ctrl+c to stop
                // https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
                _process.StandardInput.WriteLine("\x3");
                _process.StandardInput.Close();
                _process.Kill();
                _process = null;
            }
        }

        /// <summary>
        /// Resets the file paths for the config file, output file, and input file 
        /// </summary>
        /// <param name="settings">The settings to consume</param>
        private async Task SetFilePathsAsync(TailwindSettings settings)
        {
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
            if (e.Data.Contains("Error"))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await LogErrorAsync("TailwindCSS: " + e.Data);
                });
            }
            else if (e.Data.Contains("Done in"))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await LogSuccessAsync();
                });
            }
        }

        private async Task LogErrorAsync(string error)
        {
            await VS.StatusBar.ShowMessageAsync("TailwindCSS: An error occurred while building. Check the build output window for more details.");
            await WriteToBuildPaneAsync(error);
        }

        private async Task LogSuccessAsync()
        {
            await VS.StatusBar.ShowMessageAsync("TailwindCSS build succeeded");

            await WriteToBuildPaneAsync("TailwindCSS: Build completed successfully.");
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
