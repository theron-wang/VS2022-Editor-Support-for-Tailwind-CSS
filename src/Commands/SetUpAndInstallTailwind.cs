using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense
{
    [Command(PackageGuids.guidVSPackageCmdSetString, PackageIds.SetUpAndInstallTailwindCmdId)]
    internal sealed class SetUpAndInstallTailwind : BaseCommand<SetUpAndInstallTailwind>
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

                if (!string.IsNullOrEmpty(settings.TailwindConfigurationFile) && File.Exists(settings.TailwindConfigurationFile))
                {
                    return;
                }

                await ThreadHelper.JoinableTaskFactory.RunAsync(() => TailwindSetUpProcess.RunAsync(directory));

                var configFile = Path.Combine(directory, "tailwind.config.js");

                settings.TailwindConfigurationFile = configFile;
                await SettingsProvider.OverrideSettingsAsync(settings);

                var file = await PhysicalFile.FromFileAsync(configFile);

                if (file.ContainingProject is not null)
                {
                    // tailwind.extension.json is placed in the same directory as tailwind.config.js
                    var tailwindExtensionJson = await SettingsProvider.GetFilePathAsync();
                    await file.ContainingProject.AddExistingFilesAsync(configFile, tailwindExtensionJson);
                }
            }
        }
    }
}
