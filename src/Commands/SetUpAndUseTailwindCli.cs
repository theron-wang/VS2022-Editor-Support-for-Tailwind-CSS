using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpTailwindCliCmdId)]
    internal sealed class SetUpAndUseTailwindCli : BaseCommand<SetUpAndUseTailwindCli>
    {
        protected override async Task InitializeCompletedAsync()
        {
            SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
            TailwindSetUpProcess = await VS.GetMefServiceAsync<TailwindSetUpProcess>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        }

        internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; }
        internal TailwindSetUpProcess TailwindSetUpProcess { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            Command.Visible = settings.EnableTailwindCss && File.Exists(settings.TailwindCliPath) && (string.IsNullOrEmpty(settings.TailwindConfigurationFile) || File.Exists(settings.TailwindConfigurationFile) == false);
            Command.Enabled = !TailwindSetUpProcess.IsSettingUp;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            if (!TailwindSetUpProcess.IsSettingUp)
            {
                var directory = Path.GetDirectoryName(SolutionExplorerSelection.CurrentSelectedItemFullPath);
                var settings = await SettingsProvider.GetSettingsAsync();

                await ThreadHelper.JoinableTaskFactory.RunAsync(() => TailwindSetUpProcess.RunAsync(directory, settings.TailwindCliPath));
                
                var configFile = Path.Combine(directory, "tailwind.config.js");

                settings.TailwindConfigurationFile = configFile;
                settings.UseCli = true;
                await SettingsProvider.OverrideSettingsAsync(settings);
            }
        }
    }
}
