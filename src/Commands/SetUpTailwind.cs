using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpTailwindCmdId)]
    internal sealed class SetUpTailwind : BaseCommand<SetUpTailwind>
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

            Command.Visible = settings.EnableTailwindCss && (string.IsNullOrEmpty(settings.TailwindConfigurationFile) || File.Exists(settings.TailwindConfigurationFile) == false);
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var directory = Path.GetDirectoryName(SolutionExplorerSelection.CurrentSelectedItemFullPath);
            await TailwindSetUpProcess.RunAsync(directory);

            var configFile = Path.Combine(directory, "tailwind.config.js");

            var settings = await SettingsProvider.GetSettingsAsync();
            settings.TailwindConfigurationFile = configFile;
            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
