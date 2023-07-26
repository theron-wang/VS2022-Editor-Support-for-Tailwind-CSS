using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Node
{
    /// <summary>
    /// Helper class which provides methods to update and check for Tailwind npm module updates
    /// </summary>
    [Export]
    internal sealed class CheckForUpdates
    {
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }
        [Import]
        internal ConfigFileScanner ConfigFileScanner { get; set; }

        private List<string> _configFilesChecked = new List<string>();

        /// <summary>
        /// Updates the Tailwind CSS module in the specified folder if needed
        /// </summary>
        /// <param name="project">The folder to update</param>
        public async Task UpdateIfNeededAsync(string folder)
        {
            // ConfigFileScanner.HasConfigurationFile is guaranteed to already have been updated by now
            if (ConfigFileScanner.HasConfigurationFile == false)
            {
                return;
            }

            var general = await General.GetLiveInstanceAsync();

            if (general.AutomaticallyUpdateLibrary == false)
            {
                return;
            }

            if (folder.EndsWith(Path.DirectorySeparatorChar.ToString()) == false)
            {
                folder += Path.DirectorySeparatorChar;
            }

            var settings = await SettingsProvider.GetSettingsAsync();

            var shouldCheck = settings.EnableTailwindCss && string.IsNullOrEmpty(settings.TailwindConfigurationFile) == false && File.Exists(settings.TailwindConfigurationFile);

            // Prevents multiple projects in same solution from re-checking the same file
            if (shouldCheck == false || _configFilesChecked.Contains(settings.TailwindConfigurationFile))
            {
                return;
            }

            try
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.Sync);
                await VS.StatusBar.ShowProgressAsync("Checking for TailwindCSS updates...", 1, 3);
                var processInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    FileName = "cmd",
                    WorkingDirectory = Path.GetDirectoryName(folder)
                };

                var process = Process.Start(processInfo);
                process.BeginOutputReadLine();

                string currentVersion = null;
                string newVersion = null;

                // Data is given one line at a time; look through each line and see if it is what we want
                process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    var line = e.Data?.Trim();

                    if (line != null && line.StartsWith("tailwindcss"))
                    {
                        var data = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        // Sample data:
                        // Package      Current  Wanted  Latest  Location                  Depended by
                        // tailwindcss    3.3.2   3.3.3   3.3.3  node_modules/tailwindcss  project_name

                        currentVersion = data[1];
                        newVersion = data[2];
                    }
                };

                await process.StandardInput.WriteLineAsync("npm outdated tailwindcss & exit");
                await process.WaitForExitAsync();

                if (currentVersion == newVersion && currentVersion == null)
                {
                    await VS.StatusBar.ShowProgressAsync("", 3, 3);
                    await VS.StatusBar.ShowMessageAsync("TailwindCSS is up to date");
                    return;
                }

                await VS.StatusBar.ShowProgressAsync($"Updating TailwindCSS ({currentVersion} -> {newVersion})", 2, 3);

                processInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = "cmd",
                    WorkingDirectory = Path.GetDirectoryName(folder)
                };

                process = Process.Start(processInfo);
                process.BeginErrorReadLine();

                process.ErrorDataReceived += ErrorDataReceived;

                await process.StandardInput.WriteLineAsync("npm update --save tailwindcss & exit");

                await process.WaitForExitAsync();

                await VS.StatusBar.ShowProgressAsync("", 3, 3);
                await VS.StatusBar.ShowMessageAsync($"TailwindCSS update successful (updated to version {newVersion}");

                _configFilesChecked.Add(settings.TailwindConfigurationFile);
            }
            catch (Exception ex)
            {
                await VS.StatusBar.ShowProgressAsync("", 3, 3);
                await VS.StatusBar.ShowMessageAsync("TailwindCSS update/check failed; check 'Extensions' output window for more details");
                await ex.LogAsync();
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.Sync);
            }
        }

        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                if (e.Data != null)
                {
                    var ex = new Exception(e.Data);
                    await LogErrorAsync(ex);
                }
            });
        }

        private async Task LogErrorAsync(Exception exception)
        {
            await exception.LogAsync();
            await VS.StatusBar.ShowMessageAsync("An error occurred while updating Tailwind CSS: check the 'Extensions' output window for more details");
        }
    }
}
