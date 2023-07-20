using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Build;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.StartBuildProcessCmdId)]
    internal sealed class StartBuildProcess : BaseCommand<StartBuildProcess>
    {
        protected override async Task InitializeCompletedAsync()
        {
            BuildProcess = await VS.GetMefServiceAsync<TailwindBuildProcess>();
        }

        internal TailwindBuildProcess BuildProcess { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Visible = BuildProcess.IsProcessActive == false;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BuildProcess.InitializeAsync();

            BuildProcess.StartProcess();
        }
    }
}
