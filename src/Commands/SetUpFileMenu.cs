﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense;

[Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpTailwindMenu)]
internal sealed class SetUpFileMenu : BaseCommand<SetUpFileMenu>
{
    protected override async Task InitializeCompletedAsync()
    {
        SolutionExplorerSelection = await VS.GetMefServiceAsync<SolutionExplorerSelectionService>();
        TailwindSetUpProcess = await VS.GetMefServiceAsync<TailwindSetUpProcess>();
        SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        DirectoryVersionFinder = await VS.GetMefServiceAsync<DirectoryVersionFinder>();
    }

    internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; } = null!;
    internal TailwindSetUpProcess TailwindSetUpProcess { get; set; } = null!;
    internal SettingsProvider SettingsProvider { get; set; } = null!;
    internal DirectoryVersionFinder DirectoryVersionFinder { get; set; } = null!;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

        if (!settings.EnableTailwindCss)
        {
            Command.Visible = false;
            return;
        }

        Command.Visible = settings.ConfigurationFiles.Count == 0 || settings.ConfigurationFiles.All(c =>
                string.IsNullOrWhiteSpace(c.Path) || File.Exists(c.Path) == false);

        if (Command.Visible)
        {
            return;
        }

        var directory = Path.GetDirectoryName(SolutionExplorerSelection.CurrentSelectedItemFullPath);

        var isInstalled = ThreadHelper.JoinableTaskFactory.Run(() => DirectoryVersionFinder.IsTailwindInstalledAsync(directory!));

        if (isInstalled)
        {
            return;
        }

        // If not installed (either globally or locally), then check if CLI is set up and used
        // If not, then show the command because we need to install stuff
        Command.Visible = !CliUsageValidator.IsCliUsedCorrectly(settings);
    }
}
