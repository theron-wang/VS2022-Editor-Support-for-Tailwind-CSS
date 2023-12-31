﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetAsOutputCssFileCmdId)]
    internal sealed class SetAsOutputFile : BaseCommand<SetAsOutputFile>
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

            Command.Visible = settings.EnableTailwindCss && settings.TailwindCssFile != filePath && settings.TailwindOutputCssFile != filePath && Path.GetExtension(filePath) == ".css";
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var settings = await SettingsProvider.GetSettingsAsync();

            settings.TailwindOutputCssFile = SolutionExplorerSelection.CurrentSelectedItemFullPath;
            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
