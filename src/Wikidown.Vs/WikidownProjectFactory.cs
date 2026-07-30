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
            File.Copy(templateFile, dest, overwrite: true);
            InitializeWikiRoot(dest);
            return dest;
        }

        /// <summary>
        /// Makes a freshly created project point at a real wiki. If the configured
        /// WikiRoot doesn't exist, probes up to two folders above the project file
        /// for an existing docs/ folder (covers "VS put the project in a Wiki/
        /// subfolder of the solution" and "solution folder is one below the repo
        /// root"). Falls back to creating the folder with a starter Home page.
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

            var probe = projectDir;
            var prefix = "";
            for (var depth = 0; depth < 3 && probe != null; depth++)
            {
                var candidate = Path.Combine(probe, "docs");
                if (Directory.Exists(candidate))
                {
                    RewriteWikiRoot(doc, wikiRootElement, projectFile, prefix + "docs");
                    return;
                }
                probe = Path.GetDirectoryName(probe);
                prefix += "../";
            }

            var target = Path.GetFullPath(Path.Combine(projectDir, configured));
            Directory.CreateDirectory(target);
            var home = Path.Combine(target, "Home.md");
            if (!File.Exists(home))
            {
                File.WriteAllText(home,
                    "# Home\n\nWelcome to your Wikidown wiki. Add pages with `wikidown new` or the `wiki_*` MCP tools.\n");
            }
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
