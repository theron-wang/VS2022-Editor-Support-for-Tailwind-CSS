using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using TailwindCSSIntellisense.Build;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Options;
using Task = System.Threading.Tasks.Task;

namespace TailwindCSSIntellisense
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "TailwindCSS IntelliSense", "General", 0, 0, true, SupportsProfiles = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    // Makes sure that the package is initialized early so the solution explorer commands are showing up
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.DesignMode_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class TailwindCSSIntellisensePackage : AsyncPackage, IDisposable
    {
        /// <summary>
        /// TailwindCSSIntellisensePackage GUID string.
        /// </summary>
        public const string PackageGuidString = "615fb6c4-7ae7-4ae8-b3ec-271ea26d9481";

        #region Package Members

        private CheckForUpdates _checkForUpdates;
        private TailwindBuildProcess _buildProcess;
        private CompletionUtilities _completionUtils;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _checkForUpdates = await VS.GetMefServiceAsync<CheckForUpdates>();
            _buildProcess = await VS.GetMefServiceAsync<TailwindBuildProcess>();
            _completionUtils = await VS.GetMefServiceAsync<CompletionUtilities>();

            // Reload Intellisense and build so everything starts clean in the new project/folder
            VS.Events.SolutionEvents.OnAfterOpenProject += ProjectLoaded;
            VS.Events.SolutionEvents.OnAfterOpenFolder += FolderOpened;

            // Just in case this extension loads after projects are loaded, initialize:

            if (await VS.Solutions.IsOpenAsync())
            {
                await _completionUtils.InitializeAsync();

                await _buildProcess.InitializeAsync();

                // Resets tailwind.config.js configuration styles
                await _completionUtils.Configuration.ReloadCustomAttributesAsync();
                await _completionUtils.Configuration.Reloader.InitializeAsync(_completionUtils.Configuration, true);

                // Check for updates again
                foreach (var project in await VS.Solutions.GetAllProjectsAsync())
                {
                    await _checkForUpdates.UpdateIfNeededAsync(Path.GetDirectoryName(project.FullPath));
                }
            }
        }

        private void ProjectLoaded(Project project)
        {
            FolderOpened(Path.GetDirectoryName(project.FullPath));
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void FolderOpened(string folderName)
        {
            try
            {
                // Reinitialize build process
                await _buildProcess.InitializeAsync(true);

                // Initialize completion - caches classes
                // Only fires once; after that, the method does not fire
                await _completionUtils.InitializeAsync();

                // Resets tailwind.config.js configuration styles
                await _completionUtils.Configuration.Reloader.InitializeAsync(_completionUtils.Configuration, true);
                await _completionUtils.Configuration.ReloadCustomAttributesAsync();

                // Check for updates again
                await _checkForUpdates.UpdateIfNeededAsync(folderName);
            }
            catch (Exception ex)
            {
                // Catch so process does not crash (as per VSTHRD100)
                await VS.StatusBar.ShowMessageAsync("TailwindCSS: An error occurred while loading in this project");
                await ex.LogAsync();
            }
        }
#pragma warning restore VSTHRD100 // Avoid async void methods

        public void Dispose()
        {
            if (_checkForUpdates != null)
            {
                VS.Events.SolutionEvents.OnAfterOpenProject -= ProjectLoaded;
            }
        }

        #endregion
    }
}
