using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpTailwindWsCmdId)]
    internal sealed class SetUpTailwindWs : BaseCommand<SetUpTailwindWs>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            TailwindSetUpProcess = await VS.GetMefServiceAsync<TailwindSetUpProcess>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
            FileFinder = await VS.GetMefServiceAsync<FileFinder>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal TailwindSetUpProcess TailwindSetUpProcess { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }
        internal FileFinder FileFinder { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            var selected = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            if (Path.GetExtension(selected) == "")
            {
                var path = ThreadHelper.JoinableTaskFactory.Run(FileFinder.GetCurrentMiscellaneousProjectPathAsync);

                Command.Visible = settings.EnableTailwindCss && string.IsNullOrEmpty(path) == false &&
                    path.TrimEnd(Path.DirectorySeparatorChar).Equals(selected.TrimEnd(Path.DirectorySeparatorChar), StringComparison.InvariantCultureIgnoreCase) &&
                    (string.IsNullOrEmpty(settings.TailwindConfigurationFile) || File.Exists(settings.TailwindConfigurationFile) == false);
            }
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var directory = Path.GetDirectoryName(SolutionExplorerSelection.CurrentSelectedItemFullPath);
            await TailwindSetUpProcess.RunAsync(directory);

            var configFile = Path.Combine(directory, "tailwind.config.js");

            var settings = await SettingsProvider.GetSettingsAsync();
            settings.TailwindConfigurationFile = configFile;
            await SettingsProvider.OverrideSettingsAsync(settings);

            var fileNames = new string[]
            {
                Path.Combine(directory, "package.json"),
                Path.Combine(directory, "package-lock.json"),
                configFile
            };

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var projects = await VS.Solutions.GetAllProjectHierarchiesAsync();

                foreach (IVsHierarchy hierarchy in projects)
                {
                    // Get itemId of the hierarchy so we can use it to get the SolutionItem
                    hierarchy.ParseCanonicalName(directory, out var itemId);

                    var folder = await SolutionItem.FromHierarchyAsync(hierarchy, itemId);

                    // Include the created file if the current iterated folder/project is the same as the one that is selected
                    if (Path.GetDirectoryName(folder.FullPath) == directory)
                    {
                        var project = (Project)folder;
                        await project.AddExistingFilesAsync(fileNames);
                    }
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync("One or more TailwindCSS items could not be included in the project. Click the 'Show All Files' button to see them.");
            }
        }
    }
}
