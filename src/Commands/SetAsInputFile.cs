using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetAsCssFileCmdId)]
    internal sealed class SetAsInputFile : BaseCommand<SetAsInputFile>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
            DirectoryVersionFinder = await VS.GetMefServiceAsync<DirectoryVersionFinder>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }
        internal DirectoryVersionFinder DirectoryVersionFinder { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var filePath = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            Command.Visible = settings.EnableTailwindCss &&
                settings.BuildFiles is not null &&
                settings.BuildFiles.All(f =>
                    !f.Input.Equals(filePath, StringComparison.InvariantCultureIgnoreCase) &&
                    (f.Output is null ||
                    !f.Output.Equals(filePath, StringComparison.InvariantCultureIgnoreCase))
                ) &&
                Path.GetExtension(filePath) == ".css";

            if (!Command.Visible)
            {
                return;
            }

            var version = ThreadHelper.JoinableTaskFactory.Run(() => DirectoryVersionFinder.GetTailwindVersionAsync(filePath));

            if (version >= TailwindVersion.V4)
            {
                Command.Visible = false;
            }
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var settings = await SettingsProvider.GetSettingsAsync();

            var input = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            settings.BuildFiles.Add(new BuildPair() { Input = input });

            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
