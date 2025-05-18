using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Helpers;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class DirectoryVersionFinder : IDisposable
{
    public DirectoryVersionFinder()
    {
        VS.Events.SolutionEvents.OnAfterOpenFolder += InvalidateCache;
        VS.Events.SolutionEvents.OnAfterOpenProject += InvalidateCache;
    }

    private readonly Dictionary<string, TailwindVersion> _cache = [];

    /// <summary>
    /// Gets the tailwind version for the directory of the given file. Caches the result, until a new project is opened.
    /// </summary>
    public async Task<TailwindVersion> GetTailwindVersionAsync(string file)
    {
        var directory = Path.GetDirectoryName(file).ToLower();

        if (_cache.TryGetValue(directory, out var version))
        {
            return version;
        }

        var processInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            FileName = "cmd",
            Arguments = "/C npm list tailwindcss --depth=0",
            WorkingDirectory = directory
        };

        string output;

        using (var process = Process.Start(processInfo))
        {
            output = await process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();
        }

        // If not found locally, default to global
        if (string.IsNullOrWhiteSpace(output) || !output.Contains("tailwindcss"))
        {
            processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                FileName = "cmd",
                Arguments = "/C npm list tailwindcss --depth=0 -g",
                WorkingDirectory = directory
            };

            using var process = Process.Start(processInfo);

            output = await process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();
        }

        // Sample output: `-- tailwindcss@4.0.0
        if (!string.IsNullOrWhiteSpace(output))
        {
            if (output.Contains("@3"))
            {
                _cache[directory] = TailwindVersion.V3;
                return TailwindVersion.V3;
            }
            else if (output.Contains("@4.0"))
            {
                _cache[directory] = TailwindVersion.V4;
                return TailwindVersion.V4;
            }
        }

        _cache[directory] = TailwindVersion.V4_1;
        return TailwindVersion.V4_1;
    }

    private void InvalidateCache(string? _)
    {
        _cache.Clear();
    }

    private void InvalidateCache(Project? _)
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        VS.Events.SolutionEvents.OnAfterOpenFolder -= InvalidateCache;
        VS.Events.SolutionEvents.OnAfterOpenProject -= InvalidateCache;
    }
}
