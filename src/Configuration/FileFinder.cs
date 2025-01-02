using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration;

/// <summary>
/// MEF Component class to provide methods to find files of a certain type in the open solution
/// </summary>
[Export]
internal sealed class FileFinder
{
    /// <summary>
    /// Finds all Javascript files (.js) within the solution
    /// </summary>
    /// <returns>The absolute paths to all Javascript files</returns>
    internal Task<List<string>> GetJavascriptFilesAsync() => FindAllFilesAsync(DefaultConfigurationFileNames.Extensions);
    /// <summary>
    /// Finds all CSS files (.css) within the solution
    /// </summary>
    /// <returns>The absolute paths to all CSS files</returns>
    internal Task<List<string>> GetCssFilesAsync() => FindAllFilesAsync(".css");

    /// <summary>
    /// Gets the path of the current miscellaneous project
    /// </summary>
    /// <returns>A <see cref="Task"/> which returns a <see cref="string"/> representing the folder path or <see langword="null"/> if the project does not exist</returns>
    internal async Task<string> GetCurrentMiscellaneousProjectPathAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var vsSolution = await VS.Services.GetSolutionAsync();

        if (vsSolution is null)
        {
            return null;
        }
        
        var solution = await vsSolution.ToSolutionItemAsync();

        if (solution is null)
        {
            return null;
        }

        var path = solution.FullPath;
        // Path can be given without the end trailing \, so we must add it on
        return path + Path.DirectorySeparatorChar;
    }

    private Task<List<string>> FindAllFilesAsync(params string[] extensions)
    {
        return TraverseAllProjectsAndFindFilesOfTypeAsync(extensions);
    }

    internal async Task<List<string>> TraverseAllProjectsAndFindFilesOfTypeAsync(IEnumerable<string> extensions)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var projects = await GetAllProjectsAsync();

        if (projects.Count == 0)
        {
            // If no projects, probably misc
            var miscPath = await GetCurrentMiscellaneousProjectPathAsync();

            if (string.IsNullOrEmpty(miscPath))
            {
                return [];
            }

            var files = Directory
                .EnumerateFiles(miscPath, "*.*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()) &&
                               !file.Split(Path.DirectorySeparatorChar).Contains("node_modules"));
            return files.ToList();
        }

        var projectItems = new List<SolutionItem>();

        foreach (var project in projects)
        {
            projectItems.AddRange(GetProjectItems(project.Children.ToList(), extensions));
        }

        return projectItems.Select(i => i.Name).ToList();
    }

    private List<SolutionItem> GetProjectItems(List<SolutionItem> projectItems, IEnumerable<string> extensions)
    {
        var list = new List<SolutionItem>();
        foreach (var item in projectItems)
        {
            if (item.Type == SolutionItemType.PhysicalFile && extensions.Contains(Path.GetExtension(item.Name)))
            {
                list.Add(item);
            }
            else if (item.Type == SolutionItemType.PhysicalFolder)
            {
                list.AddRange(GetProjectItems(item.Children.ToList(), extensions));
            }
        }

        return list;
    }

    /// <summary>
    /// Gets all projects, if not miscellaneous
    /// </summary>
    private async Task<List<SolutionItem>> GetAllProjectsAsync()
    {
        return (await VS.Solutions.GetAllProjectsAsync()).Cast<SolutionItem>().ToList();
    }
}