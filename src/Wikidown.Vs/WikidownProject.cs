using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
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
        IVsUIHierarchy,
        IPersistFileFormat,
        IVsPersistHierarchyItem
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
        private FileSystemWatcher _watcher;

        // ── construction ─────────────────────────────────────────────────────
        public WikidownProject(IServiceProvider serviceProvider, string projectFile)
        {
            _serviceProvider = serviceProvider;
            _projectFile = projectFile;
            _wikiRoot = ResolveWikiRoot(projectFile);

            BuildHierarchy();
            StartWatcher();
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

        // ── file-system watcher ──────────────────────────────────────────────

        private void StartWatcher()
        {
            if (!Directory.Exists(_wikiRoot)) return;
            _watcher = new FileSystemWatcher(_wikiRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnFsChanged;
            _watcher.Deleted += OnFsChanged;
            _watcher.Renamed += OnFsRenamed;
        }

        private void OnFsChanged(object sender, FileSystemEventArgs e)
        {
            if (IsWikiRelevant(e.FullPath)) InvalidateAsync();
        }

        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            if (IsWikiRelevant(e.FullPath) || IsWikiRelevant(e.OldFullPath)) InvalidateAsync();
        }

        private static bool IsWikiRelevant(string path)
        {
            var name = Path.GetFileName(path);
            var ext  = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".md" || name == ".order" || string.IsNullOrEmpty(ext);
        }

        private void InvalidateAsync()
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _nodes.Clear();
                _nextId = 1;
                BuildHierarchy();
                foreach (var sink in _sinks)
                    sink?.OnInvalidateItems(ItemIdRoot);
            });
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
                    pvar = unchecked((int)ItemIdNil);
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

                case __VSHPROPID.VSHPROPID_ProjectDir:
                    pvar = Path.GetDirectoryName(_projectFile);
                    return VSConstants.S_OK;

                case __VSHPROPID.VSHPROPID_TypeName:
                    pvar = "Wikidown Wiki";
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

        public int Close()
        {
            _watcher?.Dispose();
            _watcher = null;
            return VSConstants.S_OK;
        }

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

            if (itemid == ItemIdRoot || !_nodes.TryGetValue(itemid, out var node) || node.IsFolder)
                return VSConstants.E_NOTIMPL;

            // If the document is already open, just activate it (RDT lookup,
            // does not route through the project).
            if (VsShellUtilities.IsDocumentOpen(_serviceProvider, node.FullPath, Guid.Empty, out _, out _, out ppWindowFrame))
            {
                ppWindowFrame?.Show();
                return VSConstants.S_OK;
            }

            // OpenStandardEditor opens the editor directly. Anything that asks
            // "which project owns this file?" (OpenDocumentViaProject and the
            // VsShellUtilities.OpenDocument wrapper around it) finds this
            // hierarchy via IsDocumentInProject and re-enters OpenItem forever.
            var openDoc = _serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null) return VSConstants.E_FAIL;

            var oleSp = _serviceProvider.GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider))
                as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

            var logicalView = rguidLogicalView == Guid.Empty ? VSConstants.LOGVIEWID.Primary_guid : rguidLogicalView;
            var hr = openDoc.OpenStandardEditor(
                (uint)__VSOSEFLAGS.OSE_ChooseBestStdEditor,
                node.FullPath,
                ref logicalView,
                "%3",
                this,
                itemid,
                punkDocDataExisting,
                oleSp,
                out ppWindowFrame);

            if (ErrorHandler.Succeeded(hr))
                ppWindowFrame?.Show();
            return hr;
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

        // ── IVsHierarchy / IVsUIHierarchy shared stubs ──────────────────────

        public int QueryClose(out int pfCanClose) { pfCanClose = 1; return VSConstants.S_OK; }

        public int Unused4() => VSConstants.S_OK;

        // ── IVsUIHierarchy ───────────────────────────────────────────────────

        public int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

        public int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Double-click / Enter on a file in Solution Explorer opens it.
            // Folders fall through so the tree keeps its expand/collapse default.
            if (pguidCmdGroup == VSConstants.GUID_VsUIHierarchyWindowCmds)
            {
                switch (nCmdID)
                {
                    case (uint)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick:
                    case (uint)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey:
                        if (itemid != ItemIdRoot &&
                            _nodes.TryGetValue(itemid, out var node) && !node.IsFolder)
                        {
                            var view = VSConstants.LOGVIEWID.Primary_guid;
                            return OpenItem(itemid, ref view, IntPtr.Zero, out _);
                        }
                        break;
                }
            }
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        // ── IPersistFileFormat ───────────────────────────────────────────────
        // The .wikidownproj file is authored by the factory / user and never
        // modified by the hierarchy, so the project is never dirty and Save is
        // a no-op. Implementing this is still required so VS can add the
        // project to a solution and "Save All" without errors.

        public int GetClassID(out Guid pClassID)
        {
            pClassID = PackageGuids.ProjectType;
            return VSConstants.S_OK;
        }

        public int IsDirty(out int pfIsDirty)
        {
            pfIsDirty = 0;
            return VSConstants.S_OK;
        }

        public int InitNew(uint nFormatIndex) => VSConstants.S_OK;

        public int Load(string pszFilename, uint grfMode, int fReadOnly) => VSConstants.S_OK;

        public int Save(string pszFilename, int fRemember, uint nFormatIndex) => VSConstants.S_OK;

        public int SaveCompleted(string pszFilename) => VSConstants.S_OK;

        public int GetCurFile(out string ppszFilename, out uint pnFormatIndex)
        {
            ppszFilename = _projectFile;
            pnFormatIndex = 0;
            return VSConstants.S_OK;
        }

        public int GetFormatList(out string ppszFormatList)
        {
            ppszFormatList = "Wikidown Project Files (*.wikidownproj)\n*.wikidownproj\n";
            return VSConstants.S_OK;
        }

        // ── IVsPersistHierarchyItem ──────────────────────────────────────────
        // Delegates item saves to the document data VS already holds in the
        // running document table (e.g. an open .md editor buffer).

        public int IsItemDirty(uint itemid, IntPtr punkDocData, out int pfDirty)
        {
            pfDirty = 0;
            var docData = GetDocData(punkDocData);
            return docData != null ? docData.IsDocDataDirty(out pfDirty) : VSConstants.S_OK;
        }

        public int SaveItem(VSSAVEFLAGS dwSave, string pszSilentSaveAsName, uint itemid, IntPtr punkDocData, out int pfCanceled)
        {
            pfCanceled = 0;
            var docData = GetDocData(punkDocData);
            if (docData == null) return VSConstants.S_OK;
            return docData.SaveDocData(dwSave, out _, out pfCanceled);
        }

        private static IVsPersistDocData GetDocData(IntPtr punkDocData)
            => punkDocData != IntPtr.Zero
                ? Marshal.GetObjectForIUnknown(punkDocData) as IVsPersistDocData
                : null;
    }
}
