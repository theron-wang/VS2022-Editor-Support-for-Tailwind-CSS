using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.RemoveAsOutputCssFileCmdId)]
    internal sealed class RemoveAsOutputFile : BaseCommand<RemoveAsOutputFile>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var filePath = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            Command.Visible = settings.EnableTailwindCss && settings.BuildFiles.Any(f => f.Output is not null && f.Output.Equals(filePath, StringComparison.InvariantCultureIgnoreCase));
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var settings = await SettingsProvider.GetSettingsAsync();
            var filePath = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            foreach (var file in settings.BuildFiles)
            {
                if (file.Output.Equals(filePath, StringComparison.InvariantCultureIgnoreCase))
                {
                    file.Output = null;
                }
            }

            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
