using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration
{
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
        internal Task<List<string>> GetJavascriptFilesAsync() => TraverseAllProjectsAndFindFilesOfTypeAsync(".js");
        /// <summary>
        /// Finds all CSS files (.css) within the solution
        /// </summary>
        /// <returns>The absolute paths to all CSS files</returns>
        internal Task<List<string>> GetCssFilesAsync() => TraverseAllProjectsAndFindFilesOfTypeAsync(".css");

        /// <summary>
        /// Gets the current miscellaneous project (if applicable)
        /// </summary>
        /// <returns>A <see cref="Task"/> which returns a <see cref="SolutionItem"/> which represents the current miscellaneous project or <see langword="null"/> if the project does not exist</returns>
        internal async Task<SolutionItem> GetCurrentMiscellaneousProjectAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var hierarchies = await VS.Solutions.GetAllProjectHierarchiesAsync();
            var hierarchy = hierarchies.FirstOrDefault();

            if (hierarchy == null)
            {
                return null;
            }

            SolutionItem firstItem = null;
            // Gets the first non-null physical item in the project
            // We'll try and see if the first 10 have any non-null ones; simply checking
            // the first hierarchy item is not enough since that can sometimes be null
            for (uint i = 1; i <= 10; i++)
            {
                firstItem = await SolutionItem.FromHierarchyAsync(hierarchy, i);
                if (firstItem?.FullPath != null)
                {
                    break;
                }
            }
            return firstItem.FindParent(SolutionItemType.MiscProject);
        }

        /// <summary>
        /// Gets the path of the current miscellaneous project
        /// </summary>
        /// <returns>A <see cref="Task"/> which returns a <see cref="string"/> representing the folder path or <see langword="null"/> if the project does not exist</returns>
        internal async Task<string> GetCurrentMiscellaneousProjectPathAsync()
        {
            var miscProject = await GetCurrentMiscellaneousProjectAsync();
            if (miscProject == null)
            {
                return null;
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var solution = miscProject.FindParent(SolutionItemType.Solution);

            var path = solution.FullPath;
            // Path can be given without the end trailing \, so we must add it on
            return path + Path.DirectorySeparatorChar;
        }

        private async Task<List<string>> TraverseAllProjectsAndFindFilesOfTypeAsync(string extension)
        {
            var projects = await GetAllProjectsAsync();

            var projectItems = new List<SolutionItem>();

            foreach (var project in projects)
            {
                projectItems.AddRange(GetProjectItems(project.Children.ToList(), extension));
            }

            return projectItems.Select(i => i.Name).ToList();
        }

        private List<SolutionItem> GetProjectItems(List<SolutionItem> projectItems, string extension)
        {
            var list = new List<SolutionItem>();
            foreach (var item in projectItems)
            {
                if (item.Type == SolutionItemType.PhysicalFile && Path.GetExtension(item.Name) == extension)
                {
                    list.Add(item);
                }
                else if (item.Type == SolutionItemType.PhysicalFolder)
                {
                    list.AddRange(GetProjectItems(item.Children.ToList(), extension));
                }
            }

            return list;
        }

        private async Task<List<SolutionItem>> GetAllProjectsAsync()
        {
            var projects = (await VS.Solutions.GetAllProjectsAsync()).Cast<SolutionItem>().ToList();

            if (projects.Count == 0)
            {
                // Probably miscellaneous project
                var misc = await GetCurrentMiscellaneousProjectAsync();
                if (misc != null)
                {
                    projects.Add(misc);
                }
            }

            return projects;
        }
    }
}