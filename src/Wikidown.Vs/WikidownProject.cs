using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = System.IServiceProvider;

namespace Wikidown.Vs
{
    /// <summary>
    /// Lightweight, non-building VS hierarchy that exposes a wiki root folder
    /// (defaults to <c>docs/</c> relative to the .wikidownproj file) in
    /// Solution Explorer. The project never participates in Build, Rebuild, or
    /// Clean because it does not implement any build-related interfaces.
    /// </summary>
    internal sealed class WikidownProject :
        IVsHierarchy,
        IVsProject,
        IVsUIHierarchy
    {
        // ── constants ────────────────────────────────────────────────────────
        private const uint ItemIdRoot = VSConstants.VSITEMID_ROOT;
        private const uint ItemIdNil  = VSConstants.VSITEMID_NIL;

        // Stable type GUIDs used in GetGuidProperty(VSHPROPID_TypeGuid)
        private static readonly Guid PhysicalFile   = new Guid("{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}");
        private static readonly Guid PhysicalFolder = new Guid("{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}");

        // ── model ────────────────────────────────────────────────────────────
        private sealed class Node
        {
            public uint   Id;
            public string Name       = "";
            public string FullPath   = "";
            public bool   IsFolder;
            public uint   Parent     = ItemIdNil;
            public uint   FirstChild = ItemIdNil;
            public uint   NextSib    = ItemIdNil;
        }

        // ── state ────────────────────────────────────────────────────────────
        private readonly IServiceProvider        _serviceProvider;
        private readonly string                  _projectFile;
        private readonly string                  _wikiRoot;
        private readonly Dictionary<uint, Node>  _nodes   = new Dictionary<uint, Node>();
        private readonly List<IVsHierarchyEvents> _sinks   = new List<IVsHierarchyEvents>();
        private uint _nextId = 1;

        // ── construction ─────────────────────────────────────────────────────
        public WikidownProject(IServiceProvider serviceProvider, string projectFile)
        {
            _serviceProvider = serviceProvider;
            _projectFile = projectFile;
            _wikiRoot = ResolveWikiRoot(projectFile);

            BuildHierarchy();
        }

        private static string ResolveWikiRoot(string projectFile)
        {
            var dir = Path.GetDirectoryName(projectFile) ?? Directory.GetCurrentDirectory();
            try
            {
                var doc  = XDocument.Load(projectFile);
                var root = doc.Root?.Element("WikiRoot")?.Value?.Trim();
                if (!string.IsNullOrEmpty(root))
                    return Path.GetFullPath(Path.Combine(dir, root));
            }
            catch { /* fall through to default */ }
            return Path.GetFullPath(Path.Combine(dir, "docs"));
        }

        private void BuildHierarchy()
        {
            // Root node represents the project itself
            var rootNode = new Node
            {
                Id       = ItemIdRoot,
                Name     = Path.GetFileNameWithoutExtension(_projectFile),
                FullPath = _projectFile,
                IsFolder = false,
                Parent   = ItemIdNil,
            };
            _nodes[ItemIdRoot] = rootNode;

            if (Directory.Exists(_wikiRoot))
                PopulateFolder(_wikiRoot, ItemIdRoot);
        }

        private void PopulateFolder(string dir, uint parentId)
        {
            var children = new List<uint>();

            // Sub-directories first (folders in Solution Explorer appear before files)
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var id = _nextId++;
                _nodes[id] = new Node
                {
                    Id       = id,
                    Name     = Path.GetFileName(sub),
                    FullPath = sub,
                    IsFolder = true,
                    Parent   = parentId,
                };
                children.Add(id);
                PopulateFolder(sub, id);
            }

            // Then .md and .order files
            foreach (var file in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".md" && Path.GetFileName(file) != ".order") continue;

                var id = _nextId++;
                _nodes[id] = new Node
                {
                    Id       = id,
                    Name     = Path.GetFileName(file),
                    FullPath = file,
                    IsFolder = false,
                    Parent   = parentId,
                };
                children.Add(id);
            }

            // Wire sibling chain and first-child pointer
            for (var i = 0; i < children.Count; i++)
            {
                _nodes[children[i]].NextSib = (i + 1 < children.Count) ? children[i + 1] : ItemIdNil;
            }
            if (children.Count > 0)
                _nodes[parentId].FirstChild = children[0];
        }

        // ── IVsHierarchy ─────────────────────────────────────────────────────

        public int GetProperty(uint itemid, int propid, out object pvar)
        {
            pvar = null;
            if (!_nodes.TryGetValue(itemid, out var node)) return VSConstants.E_FAIL;

            switch ((__VSHPROPID)propid)
            {
                case __VSHPROPID.VSHPROPID_Name:
                case __VSHPROPID.VSHPROPID_Caption:
                    pvar = node.Name;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_SaveName:
                    pvar = node.FullPath;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_ParentHierarchy:
                    pvar = null;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_ParentHierarchyItemid:
                    pvar = (int)ItemIdNil;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_FirstChild:
                    pvar = (int)node.FirstChild;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_NextSibling:
                    pvar = (int)node.NextSib;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_Parent:
                    pvar = (int)node.Parent;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_Expandable:
                    pvar = node.IsFolder || node.FirstChild != ItemIdNil;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_ExpandByDefault:
                    pvar = itemid == ItemIdRoot;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_IsHiddenItem:
                case __VSHPROPID.VSHPROPID_IsNonMemberItem:
                    pvar = false;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_ItemDocCookie:
                    pvar = (uint)0;
                    return VSConstants.S_OK;

                // Build-related: report no build support
                case __VSHPROPID.VSHPROPID_HasEnumerationSideEffects:
                    pvar = false;
                    return VSConstants.S_OK;

                default:
                    return VSConstants.DISP_E_MEMBERNOTFOUND;
            }
        }

        public int SetProperty(uint itemid, int propid, object var) => VSConstants.E_NOTIMPL;

        public int GetGuidProperty(uint itemid, int propid, out Guid pguid)
        {
            pguid = Guid.Empty;
            if (!_nodes.TryGetValue(itemid, out var node)) return VSConstants.E_FAIL;

            switch ((__VSHPROPID)propid)
            {
                case __VSHPROPID.VSHPROPID_TypeGuid:
                    pguid = node.IsFolder ? PhysicalFolder : PhysicalFile;
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_ProjectIDGuid:
                    // Derive a stable GUID from the full project file path
                    var pathBytes = System.Text.Encoding.UTF8.GetBytes(_projectFile.ToLowerInvariant());
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        var hash = md5.ComputeHash(pathBytes);
                        pguid = new Guid(hash);
                    }
                    return VSConstants.S_OK;

                default:
                    return VSConstants.DISP_E_MEMBERNOTFOUND;
            }
        }

        public int SetGuidProperty(uint itemid, int propid, ref Guid rguid) => VSConstants.E_NOTIMPL;

        public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested)
        {
            ppHierarchyNested = IntPtr.Zero;
            pitemidNested = ItemIdNil;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCanonicalName(uint itemid, out string pbstrName)
        {
            pbstrName = null;
            if (!_nodes.TryGetValue(itemid, out var node)) return VSConstants.E_FAIL;
            pbstrName = node.FullPath;
            return VSConstants.S_OK;
        }

        public int ParseCanonicalName(string pszName, out uint pitemid)
        {
            pitemid = ItemIdNil;
            foreach (var kv in _nodes)
            {
                if (string.Equals(kv.Value.FullPath, pszName, StringComparison.OrdinalIgnoreCase))
                {
                    pitemid = kv.Key;
                    return VSConstants.S_OK;
                }
            }
            return VSConstants.E_FAIL;
        }

        public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie)
        {
            _sinks.Add(pEventSink);
            pdwCookie = (uint)_sinks.Count;
            return VSConstants.S_OK;
        }

        public int UnadviseHierarchyEvents(uint dwCookie)
        {
            var idx = (int)dwCookie - 1;
            if (idx >= 0 && idx < _sinks.Count) _sinks[idx] = null;
            return VSConstants.S_OK;
        }

        public int Unused0() => VSConstants.E_NOTIMPL;
        public int Unused1() => VSConstants.E_NOTIMPL;
        public int Unused2() => VSConstants.E_NOTIMPL;
        public int Unused3() => VSConstants.E_NOTIMPL;

        public int Close() => VSConstants.S_OK;

        public int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
        {
            ppSP = null;
            return VSConstants.E_NOTIMPL;
        }

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp) => VSConstants.S_OK;

        // ── IVsProject ───────────────────────────────────────────────────────

        public int IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid)
        {
            pfFound = 0;
            pitemid = ItemIdNil;
            if (pdwPriority != null && pdwPriority.Length > 0)
                pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Unsupported;

            foreach (var kv in _nodes)
            {
                if (string.Equals(kv.Value.FullPath, pszMkDocument, StringComparison.OrdinalIgnoreCase))
                {
                    pfFound = 1;
                    pitemid = kv.Key;
                    if (pdwPriority != null && pdwPriority.Length > 0)
                        pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Standard;
                    return VSConstants.S_OK;
                }
            }
            return VSConstants.S_OK;
        }

        public int OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
        {
            ppWindowFrame = null;
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_nodes.TryGetValue(itemid, out var node) || node.IsFolder)
                return VSConstants.E_NOTIMPL;

            var openDoc = _serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null) return VSConstants.E_NOTIMPL;

            var logicalView = rguidLogicalView == Guid.Empty ? VSConstants.LOGVIEWID.Primary_guid : rguidLogicalView;
            return openDoc.OpenDocumentViaProject(
                node.FullPath,
                ref logicalView,
                out _,
                out _,
                out _,
                out ppWindowFrame);
        }

        public int GetMkDocument(uint itemid, out string pbstrMkDocument)
        {
            pbstrMkDocument = null;
            if (!_nodes.TryGetValue(itemid, out var node)) return VSConstants.E_FAIL;
            pbstrMkDocument = node.FullPath;
            return VSConstants.S_OK;
        }

        public int AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult)
        {
            if (pResult != null && pResult.Length > 0)
                pResult[0] = VSADDRESULT.ADDRESULT_Cancel;
            return VSConstants.E_NOTIMPL;
        }

        public int GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot, out string pbstrItemName)
        {
            pbstrItemName = pszSuggestedRoot + pszExt;
            return VSConstants.S_OK;
        }

        public int GetItemContext(uint itemid, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
        {
            ppSP = null;
            return VSConstants.E_NOTIMPL;
        }

        // ── IVsUIHierarchy ───────────────────────────────────────────────────

        public int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

        public int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
            => (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
    }
}
