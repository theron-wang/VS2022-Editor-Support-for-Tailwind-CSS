using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Node;

[Export]
internal sealed class TSNodeHandler
{
    [Import]
    private FileFinder _fileFinder;

    public async Task<bool> IsDownloadedAsync()
    {
        var processInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            FileName = "cmd"
        };

        try
        {
            var process = Process.Start(processInfo);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.ErrorDataReceived += ErrorDataReceived;

            var output = new StringBuilder();

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (string.IsNullOrEmpty(e.Data) == false)
                {
                    output.AppendLine(e.Data);
                }
            };

            await process.StandardInput.WriteLineAsync("npm list -g ts-node & exit");

            await process.WaitForExitAsync();

            return output.ToString().Contains("`-- ts-node");
        }
        catch (Exception ex)
        {
            await LogErrorAsync(ex);
        }
        return false;
    }

    public async Task DownloadAsync()
    {
        var processInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            FileName = "cmd"
        };

        try
        {
            await VS.StatusBar.ShowMessageAsync("Tailwind CSS: Installing ts-node");
            var process = Process.Start(processInfo);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.ErrorDataReceived += ErrorDataReceived;

            await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
            await process.StandardInput.WriteLineAsync("npm install -g ts-node & exit");

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
        await VS.StatusBar.ShowMessageAsync("An error occurred while installing ts-node: check the 'Extensions' output window for more details");
    }
}
