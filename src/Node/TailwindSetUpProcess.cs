using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Node
{
    /// <summary>
    /// Helper class to provide methods to install and set up Tailwind
    /// </summary>
    [Export]
    internal sealed class TailwindSetUpProcess
    {
        /// <summary>
        /// Starts a process to install Tailwind in the specified directory (uses npm)
        /// </summary>
        /// <param name="directory">The directory to install in</param>
        public async Task RunAsync(string directory)
        {
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
                var process = Process.Start(processInfo);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.OutputDataReceived += OutputDataReceived;
                process.ErrorDataReceived += ErrorDataReceived;

                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await process.StandardInput.WriteLineAsync("npm install -D tailwindcss && npx tailwindcss init & exit");

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(ex);
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await VS.StatusBar.ShowMessageAsync(e.Data);
            });
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
            await VS.StatusBar.ShowMessageAsync("An error occurred while setting up TailwindCSS: check the 'Extensions' output window for more details");
        }
    }
}
