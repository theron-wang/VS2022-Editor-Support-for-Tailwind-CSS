using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;
using TailwindCSSIntellisense.ClassSort;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense;

[Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SortEntireSolutionCmdId)]
internal sealed class SortClassesInSolution : BaseCommand<SortClassesInSolution>
{
    protected override async Task InitializeCompletedAsync()
    {
        ClassSorter = await VS.GetMefServiceAsync<ClassSorter>();
        SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
    }

    internal ClassSorter ClassSorter { get; set; } = null!;
    internal SettingsProvider SettingsProvider { get; set; } = null!;

    protected override void BeforeQueryStatus(EventArgs e)
    {
        var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

        Command.Visible = settings.EnableTailwindCss && settings.SortClassesType != Options.SortClassesOptions.None;
        Command.Enabled = !ClassSorter.Sorting;
    }

    protected override void Execute(object sender, EventArgs e)
    {
        if (!ClassSorter.Sorting)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(ClassSorter.SortAllAsync).FireAndForget();
        }
    }
}
