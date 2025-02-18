﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.RemoveAsCssFileCmdId)]
    internal sealed class RemoveAsInputFile : BaseCommand<RemoveAsInputFile>
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

            Command.Visible = settings.EnableTailwindCss && settings.BuildFiles.Any(f => f.Input.Equals(filePath, StringComparison.InvariantCultureIgnoreCase));

            if (!Command.Visible)
            {
                return;
            }

            var version = ThreadHelper.JoinableTaskFactory.Run(() => DirectoryVersionFinder.GetTailwindVersionAsync(filePath));

            if (version == TailwindVersion.V4)
            {
                Command.Visible = false;
            }
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var settings = await SettingsProvider.GetSettingsAsync();
            var filePath = SolutionExplorerSelection.CurrentSelectedItemFullPath;

            settings.BuildFiles.RemoveAll(f => f.Input.Equals(filePath, StringComparison.InvariantCultureIgnoreCase));

            await SettingsProvider.OverrideSettingsAsync(settings);
        }
    }
}
