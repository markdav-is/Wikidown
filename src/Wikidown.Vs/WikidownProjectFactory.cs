using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Wikidown.Vs
{
    /// <summary>
    /// Creates <see cref="WikidownProject"/> instances for every .wikidownproj file
    /// found in a loaded solution.
    /// </summary>
    [Guid(PackageGuids.ProjectTypeGuidString)]
    internal sealed class WikidownProjectFactory : IVsProjectFactory
    {
        private readonly WikidownPackage _package;
        private IServiceProvider _serviceProvider;

        public WikidownProjectFactory(WikidownPackage package)
        {
            _package = package;
            _serviceProvider = package;
        }

        // ── IVsProjectFactory ────────────────────────────────────────────────

        public int CanCreateProject(string pszFilename, uint grfCreateFlags, out int pfCanCreate)
        {
            pfCanCreate = string.Equals(
                Path.GetExtension(pszFilename),
                ".wikidownproj",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int CreateProject(
            string pszFilename,
            string pszLocation,
            string pszName,
            uint grfCreateFlags,
            ref Guid iidProject,
            out IntPtr ppvProject,
            out int pfCanceled)
        {
            pfCanceled = 0;
            ppvProject = IntPtr.Zero;

            try
            {
                var project = new WikidownProject(_serviceProvider, pszFilename);
                ppvProject = Marshal.GetIUnknownForObject(project);
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                ppvProject = IntPtr.Zero;
                return Marshal.GetHRForException(ex);
            }
        }

        public int Close() => VSConstants.S_OK;

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            _serviceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }
    }
}
