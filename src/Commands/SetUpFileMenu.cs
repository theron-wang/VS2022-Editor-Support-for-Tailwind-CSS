using Community.VisualStudio.Toolkit;
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
    }

    internal SolutionExplorerSelectionService SolutionExplorerSelection { get; set; } = null!;
    internal TailwindSetUpProcess TailwindSetUpProcess { get; set; } = null!;
    internal SettingsProvider SettingsProvider { get; set; } = null!;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

        Command.Visible = settings.EnableTailwindCss &&
            (settings.ConfigurationFiles.Count == 0 || settings.ConfigurationFiles.All(c =>
                string.IsNullOrWhiteSpace(c.Path) || File.Exists(c.Path) == false));
    }
}
