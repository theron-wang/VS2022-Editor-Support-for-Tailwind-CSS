using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Node;

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
    public async Task<string?> RunAsync(string directory, bool needInstall, string? cliPath = null)
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
            WorkingDirectory = directory,
            Arguments = "/C npm install tailwindcss @tailwindcss/cli",
        };

        try
        {
            await VS.StatusBar.ShowMessageAsync("Setting up Tailwind CSS");

            if (needInstall)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                using var process = Process.Start(processInfo);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.ErrorDataReceived += ErrorDataReceived;

                await process.WaitForExitAsync();
            }

            string fileName;

            if (File.Exists(Path.Combine(directory, "tailwind.css")))
            {
                fileName = $"tailwind-{Guid.NewGuid().ToString().Substring(0, 8)}.css";
            }
            else
            {
                fileName = "tailwind.css";
            }

            using (var fileStream = new FileStream(Path.Combine(directory, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
            {
                using var streamWriter = new StreamWriter(fileStream);
                await streamWriter.WriteLineAsync("@import \"tailwindcss\";");
            }

            return Path.Combine(directory, fileName);
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

        return null;
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
