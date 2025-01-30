using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense;

[Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetAsConfigFileCmdId)]
internal sealed class SetAsConfigFile : BaseCommand<SetAsConfigFile>
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

        Command.Visible = settings.EnableTailwindCss &&
            !settings.ConfigurationFiles.Any(c => c.Path.Equals(filePath, StringComparison.InvariantCultureIgnoreCase)) &&
            DefaultConfigurationFileNames.Extensions.Contains(Path.GetExtension(filePath));
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var settings = await SettingsProvider.GetSettingsAsync();

        settings.ConfigurationFiles.Add(new() { Path = SolutionExplorerSelection.CurrentSelectedItemFullPath, IsDefault = settings.ConfigurationFiles.Count == 0, ApplicableLocations = [] });

        await SettingsProvider.OverrideSettingsAsync(settings);
    }
}
