using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Wikidown.Vs
{
    /// <summary>
    /// Creates <see cref="WikidownProject"/> instances for every .wikidownproj file
    /// found in a loaded solution, and clones the template file into the target
    /// location when a project is created from the "Wikidown Wiki" template.
    /// </summary>
    [Guid(PackageGuids.ProjectTypeGuidString)]
    internal sealed class WikidownProjectFactory : IVsProjectFactory
    {
        private const string ProjectExtension = ".wikidownproj";

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
                ProjectExtension,
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
                // CPF_CLONEFILE: pszFilename points at the file inside the unzipped
                // template cache; the factory owns copying it to pszLocation\pszName.
                // CPF_OPENFILE: pszFilename is the project file already on disk.
                var projectFile =
                    (grfCreateFlags & (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE) != 0
                        ? CloneTemplate(pszFilename, pszLocation, pszName)
                        : pszFilename;

                var project = new WikidownProject(_serviceProvider, projectFile);
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

        // ── template cloning ─────────────────────────────────────────────────

        private static string CloneTemplate(string templateFile, string location, string name)
        {
            if (string.IsNullOrEmpty(location))
                location = Path.GetDirectoryName(templateFile) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(location);

            var fileName = string.IsNullOrEmpty(name) ? "wiki" + ProjectExtension
                : name.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase) ? name
                : name + ProjectExtension;

            var dest = Path.Combine(location, fileName);
            // With CreateInPlace templates VS materializes the file at the
            // destination itself (and may still hold a handle on it) before
            // calling the factory — only copy when it isn't already there.
            var samePath = string.Equals(
                Path.GetFullPath(templateFile), Path.GetFullPath(dest),
                StringComparison.OrdinalIgnoreCase);
            if (!samePath && !File.Exists(dest))
                File.Copy(templateFile, dest);

            try { InitializeWikiRoot(dest); }
            catch { /* wiki discovery/seeding is best-effort; never block creation */ }
            return dest;
        }

        /// <summary>
        /// Makes a freshly created project point at a real wiki. If the configured
        /// WikiRoot doesn't exist, the wiki lives at <c>docs/</c> under the
        /// repository root (the nearest ancestor containing <c>.git</c>) so every
        /// project in the repo shares one wiki regardless of where VS put the
        /// .wikidownproj. Outside a repo it falls back to the project folder.
        /// The folder is created with a starter Home page when missing.
        /// </summary>
        private static void InitializeWikiRoot(string projectFile)
        {
            var projectDir = Path.GetDirectoryName(projectFile);
            if (string.IsNullOrEmpty(projectDir)) return;

            XDocument doc;
            try { doc = XDocument.Load(projectFile); }
            catch { return; }

            var wikiRootElement = doc.Root?.Element("WikiRoot");
            var configured = wikiRootElement?.Value?.Trim();
            if (string.IsNullOrEmpty(configured)) configured = "docs";

            if (Directory.Exists(Path.GetFullPath(Path.Combine(projectDir, configured))))
                return;

            var repoRoot = FindRepoRoot(projectDir);
            var wikiDir = Path.Combine(repoRoot ?? projectDir, "docs");
            var relative = RelativeDocsPath(projectDir, repoRoot);
            if (!string.Equals(relative, configured, StringComparison.OrdinalIgnoreCase))
                RewriteWikiRoot(doc, wikiRootElement, projectFile, relative);

            if (Directory.Exists(wikiDir)) return;
            Directory.CreateDirectory(wikiDir);
            var home = Path.Combine(wikiDir, "Home.md");
            if (!File.Exists(home))
            {
                File.WriteAllText(home,
                    "# Home\n\nWelcome to your Wikidown wiki. Add pages with `wikidown new`.\n");
            }
        }

        // .git is a directory in a normal clone and a file in a worktree or submodule.
        private static string FindRepoRoot(string startDir)
        {
            for (var dir = Path.GetFullPath(startDir); dir != null; dir = Path.GetDirectoryName(dir))
            {
                var git = Path.Combine(dir, ".git");
                if (Directory.Exists(git) || File.Exists(git)) return dir;
            }
            return null;
        }

        private static string RelativeDocsPath(string projectDir, string repoRoot)
        {
            if (repoRoot == null) return "docs";
            var prefix = "";
            for (var dir = Path.GetFullPath(projectDir);
                 dir != null && !string.Equals(dir, repoRoot, StringComparison.OrdinalIgnoreCase);
                 dir = Path.GetDirectoryName(dir))
                prefix += "../";
            return prefix + "docs";
        }

        private static void RewriteWikiRoot(XDocument doc, XElement wikiRootElement, string projectFile, string value)
        {
            if (wikiRootElement == null)
            {
                wikiRootElement = new XElement("WikiRoot");
                doc.Root?.Add(wikiRootElement);
            }
            wikiRootElement.Value = value;
            doc.Save(projectFile);
        }
    }
}
