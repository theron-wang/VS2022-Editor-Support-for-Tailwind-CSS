﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense;

[Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpTailwindGlobalCmdId)]
internal sealed class SetUpAndUseTailwindGlobal : BaseCommand<SetUpAndUseTailwindGlobal>
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
        Command.Enabled = !TailwindSetUpProcess.IsSettingUp;
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        if (!TailwindSetUpProcess.IsSettingUp)
        {
            var directory = Path.GetDirectoryName(SolutionExplorerSelection.CurrentSelectedItemFullPath);
            // Check again to see if there were any changes since the last settings cache
            // User may have manually run the setup command, for example
            SettingsProvider.RefreshSettings();
            var settings = await SettingsProvider.GetSettingsAsync();

            if (settings.ConfigurationFiles.Count > 0 && settings.ConfigurationFiles.Any(c =>
                !string.IsNullOrWhiteSpace(c.Path) && File.Exists(c.Path)))
            {
                return;
            }

            var configFile = await ThreadHelper.JoinableTaskFactory.RunAsync(() => TailwindSetUpProcess.RunAsync(directory, false));

            settings.ConfigurationFiles.Add(new() { Path = configFile, IsDefault = true, ApplicableLocations = [] });
            settings.BuildFiles.Add(new() { Input = configFile });
            await SettingsProvider.OverrideSettingsAsync(settings);

            var file = await PhysicalFile.FromFileAsync(SolutionExplorerSelection.CurrentSelectedItemFullPath);

            if (file is not null && file.ContainingProject is not null)
            {
                // tailwind.extension.json is placed in the same directory as tailwind.css
                var tailwindExtensionJson = await SettingsProvider.GetFilePathAsync();
                await file.ContainingProject.AddExistingFilesAsync(configFile, tailwindExtensionJson);
            }
        }
    }
}
