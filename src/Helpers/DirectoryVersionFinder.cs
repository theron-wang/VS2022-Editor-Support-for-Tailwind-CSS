using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private readonly Dictionary<string, bool> _installedCache = [];

    /// <summary>
    /// Gets the tailwind version for the directory of the given file. Caches the result, until a new project is opened.
    /// </summary>
    public async Task<bool> IsTailwindInstalledAsync(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        directory = directory!.ToLower();

        if (_installedCache.TryGetValue(directory, out var value))
        {
            return value;
        }

        var processInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            FileName = "cmd",
            Arguments = "/C npm list tailwindcss @tailwindcss/cli --depth=0",
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
                Arguments = "/C npm list tailwindcss @tailwindcss/cli --depth=0 -g",
                WorkingDirectory = directory
            };

            using var process = Process.Start(processInfo);

            output = await process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();
        }

        // Sample output: `-- tailwindcss@4.0.0
        if (!string.IsNullOrWhiteSpace(output))
        {
            if (output.Contains("@tailwindcss/cli"))
            {
                _installedCache[directory] = true;
                return true;
            }

            if (output.Contains("tailwindcss@4") && output.Contains("@tailwindcss/cli"))
            {
                _installedCache[directory] = true;
                return true;
            }

            if (output.Contains("tailwindcss") && !output.Contains("tailwindcss@4"))
            {
                _installedCache[directory] = true;
                return true;
            }
        }

        _installedCache[directory] = false;
        return false;
    }

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

    public void ClearCacheForDirectory(string directory, bool recursive = true)
    {
        directory = directory.ToLower();

        if (recursive)
        {
            foreach (var key in _cache.Keys.ToList())
            {
                if (key.StartsWith(directory))
                {
                    _cache.Remove(key);
                    _installedCache.Remove(key);
                }
            }
        }
        else
        {
            _cache.Remove(directory);
            _installedCache.Remove(directory);
        }
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
