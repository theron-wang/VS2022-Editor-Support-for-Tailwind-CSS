using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.ClassSort;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SortInOpenFileCmdId)]
    internal sealed class SortClassesInOpenFile : BaseCommand<SortClassesInOpenFile>
    {
        protected override async Task InitializeCompletedAsync()
        {
            ClassSorter = await VS.GetMefServiceAsync<ClassSorter>();
            SettingsProvider = await VS.GetMefServiceAsync<SettingsProvider>();
        }

        internal ClassSorter ClassSorter { get; set; }
        internal SettingsProvider SettingsProvider { get; set; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var settings = ThreadHelper.JoinableTaskFactory.Run(SettingsProvider.GetSettingsAsync);

            Command.Visible = settings.EnableTailwindCss && settings.SortClassesType != Options.SortClassesOptions.None;
            Command.Enabled = !ClassSorter.Sorting;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            if (!ClassSorter.Sorting)
            {
                var file = await VS.Documents.GetActiveDocumentViewAsync();

                if (!string.IsNullOrWhiteSpace(file?.FilePath))
                {
                    await ClassSorter.SortAsync(file.FilePath, true);
                }
            }
        }
    }
}
