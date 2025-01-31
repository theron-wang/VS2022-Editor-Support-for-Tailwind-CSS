using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Node
{
    /// <summary>
    /// Helper class to provide methods to install and set up Tailwind
    /// </summary>
    [Export]
    internal sealed class TailwindSetUpProcess
    {
        public bool IsSettingUp { get; private set; }

        /// <summary>
        /// Starts a process to install Tailwind in the specified directory (uses npm)
        /// </summary>
        /// <param name="directory">The directory to install in</param>
        public async Task RunAsync(string directory, bool needInstall, string cliPath = null)
        {
            IsSettingUp = true;
            var processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "cmd",
                WorkingDirectory = directory
            };

            try
            {
                await VS.StatusBar.ShowMessageAsync("Setting up Tailwind CSS");
                using var process = Process.Start(processInfo);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.ErrorDataReceived += ErrorDataReceived;

                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);

                if (needInstall)
                {
                    await process.StandardInput.WriteLineAsync("npm install -D tailwindcss && npx tailwindcss init & exit");
                }
                else if (string.IsNullOrWhiteSpace(cliPath))
                {
                    await process.StandardInput.WriteLineAsync($"{cliPath} init & exit");
                }
                else
                {
                    await process.StandardInput.WriteLineAsync("npx tailwindcss init & exit");
                }

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(ex);
            }
            finally
            {
                IsSettingUp = false;
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
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
            await VS.StatusBar.ShowMessageAsync("An error occurred while setting up Tailwind CSS: check the 'Extensions' output window for more details");
        }
    }
}
