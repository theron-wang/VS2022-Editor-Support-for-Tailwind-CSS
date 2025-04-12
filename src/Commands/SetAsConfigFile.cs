using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense;

[Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetAsConfigFileCmdId)]
internal sealed class SetAsConfigFile : BaseCommand<SetAsConfigFile>
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

        Command.Visible = settings.EnableTailwindCss &&
                !settings.ConfigurationFiles.Any(c => c.Path.Equals(filePath, StringComparison.InvariantCultureIgnoreCase));

        if (!Command.Visible)
        {
            return;
        }

        var version = ThreadHelper.JoinableTaskFactory.Run(() => DirectoryVersionFinder.GetTailwindVersionAsync(filePath));

        if (version == TailwindVersion.V3)
        {
            Command.Visible = DefaultConfigurationFileNames.Extensions.Contains(Path.GetExtension(filePath));
        }
        else
        {
            Command.Visible = Path.GetExtension(filePath) == ".css";
        }
    }

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var settings = await SettingsProvider.GetSettingsAsync();

        var path = SolutionExplorerSelection.CurrentSelectedItemFullPath;

        settings.ConfigurationFiles.Add(new() { Path = path });

        if (Path.GetExtension(path) == ".css" && !settings.BuildFiles.Any(f => f.Input.Equals(path, StringComparison.InvariantCultureIgnoreCase)))
        {
            settings.BuildFiles.Add(new() { Input = path });
        }

        await SettingsProvider.OverrideSettingsAsync(settings);
    }
}
