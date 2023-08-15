using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Build;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.StartBuildProcessWsCmdId)]
    internal sealed class StartBuildProcessWs : BaseCommand<StartBuildProcessWs>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            BuildProcess = await VS.GetMefServiceAsync<TailwindBuildProcess>();
            ConfigFileScanner = await VS.GetMefServiceAsync<ConfigFileScanner>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal TailwindBuildProcess BuildProcess { get; set; }
        internal ConfigFileScanner ConfigFileScanner { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);
            if (ConfigFileScanner.HasConfigurationFile == false)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => ConfigFileScanner.FindConfigurationFilePathAsync());
            }
            Command.Visible = settings.EnableTailwindCss && Path.GetExtension(SolutionExplorerSelection.CurrentSelectedItemFullPath) == "" && BuildProcess.AreProcessesActive() == false && ConfigFileScanner.HasConfigurationFile && settings.BuildType != BuildProcessOptions.None;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BuildProcess.InitializeAsync();

            BuildProcess.StartProcess();
        }
    }
}
