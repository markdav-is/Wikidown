using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Wikidown.Vs
{
    /// <summary>
    /// Package that registers the Wikidown project type with Visual Studio 2022+.
    /// Loads automatically when a solution containing a .wikidownproj is opened.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideProjectFactory(
        typeof(WikidownProjectFactory),
        "Wikidown Wiki",
        "Wikidown Project Files (*.wikidownproj);*.wikidownproj",
        "wikidownproj",
        "wikidownproj",
        @".\ProjectTemplates",
        LanguageVsTemplate = "Wikidown")]
    public sealed class WikidownPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RegisterProjectFactory(new WikidownProjectFactory(this));
        }
    }
}
