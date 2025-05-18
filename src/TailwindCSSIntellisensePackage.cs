﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using TailwindCSSIntellisense.Build;
using TailwindCSSIntellisense.ClassSort;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Options;
using Task = System.Threading.Tasks.Task;

namespace TailwindCSSIntellisense;

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
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Tailwind CSS IntelliSense", "General", 0, 0, true, SupportsProfiles = true)]
[ProvideOptionPage(typeof(OptionsProvider.LinterOptions), "Tailwind CSS IntelliSense", "Linter", 0, 0, true, SupportsProfiles = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
// Makes sure that the package is initialized early so the solution explorer commands are showing up
[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.DesignMode_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class TailwindCSSIntellisensePackage : AsyncPackage, IDisposable
{
    /// <summary>
    /// TailwindCSSIntellisensePackage GUID string.
    /// </summary>
    public const string PackageGuidString = "615fb6c4-7ae7-4ae8-b3ec-271ea26d9481";

    #region Package Members

    private TailwindBuildProcess _buildProcess = null!;
    private ProjectConfigurationManager _completionUtils = null!;
    private ClassSorter _classSorter = null!;

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

        _buildProcess = await VS.GetMefServiceAsync<TailwindBuildProcess>();
        _completionUtils = await VS.GetMefServiceAsync<ProjectConfigurationManager>();
        _classSorter = await VS.GetMefServiceAsync<ClassSorter>();

        // Reload Intellisense and build so everything starts clean in the new project/folder
        VS.Events.SolutionEvents.OnAfterOpenProject += ProjectLoaded;
        VS.Events.SolutionEvents.OnAfterOpenFolder += FolderOpened;

        // Just in case this extension loads after projects are loaded, initialize:

        if (await VS.Solutions.IsOpenAsync())
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await _buildProcess.InitializeAsync(true);
                await _completionUtils.InitializeAsync();
                await _completionUtils.Configuration.Reloader.InitializeAsync();
                _classSorter.Initialize();
            }).FireAndForget();
        }
    }

    /// <summary>
    /// On project open. project does not need to be supplied.
    /// </summary>
    /// <param name="project">Can be null or anything, does not affect output.</param>
    private void ProjectLoaded(Project? project = null)
    {
        FolderOpened();
    }

    /// <summary>
    /// On folder open. folderName does not need to be supplied.
    /// </summary>
    /// <param name="folderName">Can be null or anything, does not affect output.</param>
    private void FolderOpened(string? folderName = null)
    {
        try
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await _buildProcess.InitializeAsync(true);
                await _completionUtils.InitializeAsync();
                await _completionUtils.Configuration.Reloader.InitializeAsync();
                _classSorter.Initialize();
            }).FireAndForget();
        }
        catch (Exception ex)
        {
            JoinableTaskFactory.Run(() => VS.StatusBar.ShowMessageAsync("Tailwind CSS: An error occurred while loading in this project"));
            ex.Log();
        }
    }

    public void Dispose()
    {
        if (_classSorter != null)
        {
            VS.Events.SolutionEvents.OnAfterOpenProject -= ProjectLoaded;
            VS.Events.SolutionEvents.OnAfterOpenFolder -= FolderOpened;
        }
    }

    #endregion
}
