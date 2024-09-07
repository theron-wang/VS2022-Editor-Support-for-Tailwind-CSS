using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Build;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.StartMinifyBuildProcessCmdId)]
    internal sealed class StartMinifyBuildProcess : BaseCommand<StartMinifyBuildProcess>
    {
        protected override async Task InitializeCompletedAsync()
        {
            BuildProcess = await VS.GetMefServiceAsync<TailwindBuildProcess>();
            ConfigFileScanner = await VS.GetMefServiceAsync<ConfigFileScanner>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        }

        internal TailwindBuildProcess BuildProcess { get; set; }
        internal ConfigFileScanner ConfigFileScanner { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);
            Command.Visible = settings.EnableTailwindCss && BuildProcess.AreProcessesActive() == false && ConfigFileScanner.HasConfigurationFile && settings.BuildType != BuildProcessOptions.None;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BuildProcess.InitializeAsync();

            BuildProcess.StartProcess(true);
        }
    }
}
