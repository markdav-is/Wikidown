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
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideProjectFactory(
        typeof(WikidownProjectFactory),
        "Wikidown Wiki",
        "Wikidown Project Files (*.wikidownproj);*.wikidownproj",
        "wikidownproj",
        "wikidownproj",
        // No legacy templates directory: VS's old-style scanner would list the
        // raw wiki.wikidownproj inside it as a second, bare "wiki" template in
        // the New Project dialog. Discovery happens via the VSIX .vstman only.
        null,
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
