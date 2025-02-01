using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Node;

/// <summary>
/// Helper class which provides methods to update and check for Tailwind npm module updates
/// </summary>
[Export]
internal sealed class CheckForUpdates
{
    [Import]
    internal SettingsProvider SettingsProvider { get; set; }

    private readonly List<string> _configFilesChecked = [];

    /// <summary>
    /// Updates the Tailwind CSS module in the specified folder if needed
    /// </summary>
    /// <param name="project">The folder to update</param>
    public async Task UpdateIfNeededAsync(string folder)
    {
        var settings = await SettingsProvider.GetSettingsAsync();

        if (settings.ConfigurationFiles.Count == 0)
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

        var shouldCheck = settings.EnableTailwindCss &&
            !(settings.ConfigurationFiles.Count == 0 || settings.ConfigurationFiles.All(c =>
                string.IsNullOrEmpty(c.Path) || File.Exists(c.Path) == false));

        var configFilePaths = settings.ConfigurationFiles.Select(c => c.Path);
        // Prevents multiple projects in same solution from re-checking the same file
        if (shouldCheck == false || _configFilesChecked.Any(configFilePaths.Contains))
        {
            return;
        }

        try
        {
            await VS.StatusBar.ShowMessageAsync("Checking for Tailwind CSS updates");
            var processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                FileName = "cmd",
                Arguments = "/C npm outdated tailwindcss --json",
                WorkingDirectory = Path.GetDirectoryName(folder)
            };

            string output;

            using (var process = Process.Start(processInfo))
            {
                /*
                 * Sample output:
                 
                 {
                  "tailwindcss": [
                    {
                      "current": "3.4.1",
                      "wanted": "4.0.3",
                      "latest": "4.0.3",
                      "dependent": "@tailwindcss/container-queries",
                      "location": "path/to/folder"
                    },
                    {
                      "current": "3.4.1",
                      "wanted": "3.4.17",
                      "latest": "4.0.3",
                      "dependent": "Test",
                      "location": "path/to/folder"
                    }
                  ]
                }

                */
                output = await process.StandardOutput.ReadToEndAsync();

                await process.WaitForExitAsync();
            }

            var result = JsonSerializer.Deserialize<JsonObject>(output);

            if (result.ContainsKey("tailwindcss") == false)
            {
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS is up to date");
                return;
            }

            OutdatedPackage relevantPackage = null;

            if (result["tailwindcss"] is JsonArray array)
            {
                var packages = array.Select(obj => obj.Deserialize<OutdatedPackage>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }));

                relevantPackage = packages.FirstOrDefault(
                    p => p.Dependent.Equals(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)), StringComparison.InvariantCultureIgnoreCase));
            }
            else if (result["tailwindcss"] is JsonObject jsonObj)
            {
                relevantPackage = jsonObj.Deserialize<OutdatedPackage>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (!relevantPackage.Dependent.Equals(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)), StringComparison.InvariantCultureIgnoreCase))
                {
                    relevantPackage = null;
                }
            }

            if (relevantPackage is null)
            {
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS is up to date");
                return;
            }

            // Avoid updating major versions: 3.x --> 4.x, for example
            var currentMajor = relevantPackage.Current.Split('.')[0];
            var newMajor = relevantPackage.Latest.Split('.')[0];

            if (currentMajor != newMajor)
            {
                await VS.StatusBar.ShowMessageAsync($"A major Tailwind update is available: {relevantPackage.Current}. If you would like to update, please manually run npm install tailwindcss@latest.");
                return;
            }

            await VS.StatusBar.ShowMessageAsync($"Updating Tailwind CSS ({relevantPackage.Current} -> {relevantPackage.Latest})");

            processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "cmd",
                Arguments = "/C npm install tailwindcss@latest",
                WorkingDirectory = Path.GetDirectoryName(folder)
            };

            string error;

            using (var process = Process.Start(processInfo))
            {
                error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var ex = new Exception(error);
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync("An error occurred while updating Tailwind CSS: check the 'Extensions' output window for more details");
                return;
            }

            await VS.StatusBar.ShowMessageAsync($"Tailwind CSS update successful (updated to version {relevantPackage.Latest})");

            _configFilesChecked.AddRange(settings.ConfigurationFiles.Select(f => f.Path));
        }
        catch (Exception ex)
        {
            await VS.StatusBar.ShowMessageAsync("Tailwind CSS update/check failed; check 'Extensions' output window for more details");
            await ex.LogAsync();
        }
    }

    private class OutdatedPackage
    {
        public string Current { get; set; }
        public string Wanted { get; set; }
        public string Latest { get; set; }
        public string Dependent { get; set; }
        public string Location { get; set; }
    }
}
