using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetAsConfigFileCmdId)]
    internal sealed class SetAsConfigFile : BaseCommand<SetAsConfigFile>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
            TSNodeHandler = await VS.GetMefServiceAsync<TSNodeHandler>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }
        internal TSNodeHandler TSNodeHandler { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var filePath = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            Command.Visible = settings.EnableTailwindCss && settings.TailwindConfigurationFile != filePath && (Path.GetExtension(filePath) == ".js" || Path.GetExtension(filePath) == ".ts");
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var settings = await SettingsProvider.GetSettingsAsync();

            if (Path.GetExtension(SolutionExplorerSelection.CurrentSelectedItemFullPath) == ".ts" && !await TSNodeHandler.IsDownloadedAsync())
            {
                var confirm = await VS.MessageBox.ShowWarningAsync("TS Support", "Using a .ts file for your configuration file will download the ts-node npm module globally.");
                if (confirm == VSConstants.MessageBoxResult.IDOK)
                {
                    await TSNodeHandler.DownloadAsync();
                }
                else
                {
                    return;
                }
            }

            settings.TailwindConfigurationFile = SolutionExplorerSelection.CurrentSelectedItemFullPath;
            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
