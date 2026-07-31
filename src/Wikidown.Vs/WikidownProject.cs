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
        IVsPersistHierarchyItem,
        IOleCommandTarget
    {
        // Shell context menus (ShellCmdDef guidSHLMainMenu)
        private static readonly Guid ShlMainMenu = new Guid("d309f791-903f-11d0-9efc-00a0c911004f");
        private const int IDM_VS_CTXT_PROJNODE   = 0x0402;
        private const int IDM_VS_CTXT_ITEMNODE   = 0x0430;
        private const int IDM_VS_CTXT_FOLDERNODE = 0x0431;

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
            public string FullPath   = "";   // document identity: .md for pages, dir for bare folders
            public bool   IsFolder;          // folder icon / folder context menu
            public bool   IsPage;            // FullPath is an openable .md
            public string ChildDir;          // non-null when the node expands into a folder
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
        // Item ids stay stable for a given path across rebuilds — Solution
        // Explorer caches by id, and renumbering makes refreshes unreliable.
        private readonly Dictionary<string, uint> _idByPath = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
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
            // Sort children the way the wiki renders them: entries listed in
            // .order come first (in that order), everything else follows
            // alphabetically. A page file sorts immediately before its
            // same-named subfolder. The .order file itself is listed last.
            var order = ReadOrderEntries(dir);
            int OrderKey(string baseName)
            {
                for (var i = 0; i < order.Count; i++)
                {
                    if (string.Equals(order[i], baseName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return int.MaxValue;
            }

            // A page file with a same-named sibling folder (the spec's
            // subpage convention) becomes ONE node: folder icon, expandable,
            // opens the page on double-click.
            var dirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sub in Directory.GetDirectories(dir))
                dirs[Path.GetFileName(sub)] = sub;

            var entries = new List<(int Key, string Base, int Kind, Node Node)>();

            foreach (var file in Directory.GetFiles(dir))
            {
                var name = Path.GetFileName(file);
                if (name == ".order")
                {
                    entries.Add((int.MaxValue, name, 2, new Node { Name = name, FullPath = file }));
                    continue;
                }
                if (!string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase))
                    continue;

                var baseName = Path.GetFileNameWithoutExtension(file);
                if (dirs.TryGetValue(baseName, out var pairedDir))
                {
                    dirs.Remove(baseName);
                    entries.Add((OrderKey(baseName), baseName, 0, new Node
                    {
                        Name = baseName, FullPath = file, IsFolder = true, IsPage = true, ChildDir = pairedDir,
                    }));
                }
                else
                {
                    entries.Add((OrderKey(baseName), baseName, 0, new Node
                    {
                        Name = name, FullPath = file, IsPage = true,
                    }));
                }
            }

            // Folders with no paired page
            foreach (var kv in dirs)
            {
                entries.Add((OrderKey(kv.Key), kv.Key, 1, new Node
                {
                    Name = kv.Key, FullPath = kv.Value, IsFolder = true, ChildDir = kv.Value,
                }));
            }

            entries.Sort((a, b) =>
            {
                if (a.Kind == 2 || b.Kind == 2) return a.Kind.CompareTo(b.Kind);
                var byKey = a.Key.CompareTo(b.Key);
                if (byKey != 0) return byKey;
                var byName = string.Compare(a.Base, b.Base, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return a.Kind.CompareTo(b.Kind);
            });

            var children = new List<uint>();
            foreach (var entry in entries)
            {
                var node = entry.Node;
                if (!_idByPath.TryGetValue(node.FullPath, out var id))
                {
                    id = _nextId++;
                    _idByPath[node.FullPath] = id;
                }
                node.Id = id;
                node.Parent = parentId;
                _nodes[id] = node;
                children.Add(id);
                if (node.ChildDir != null)
                    PopulateFolder(node.ChildDir, id);
            }

            // Wire sibling chain and first-child pointer
            for (var i = 0; i < children.Count; i++)
            {
                _nodes[children[i]].NextSib = (i + 1 < children.Count) ? children[i + 1] : ItemIdNil;
            }
            if (children.Count > 0)
                _nodes[parentId].FirstChild = children[0];
        }

        // ── .order handling (mirrors Wikidown.Core.OrderFile semantics) ──────

        private static List<string> ReadOrderEntries(string dir)
        {
            var result = new List<string>();
            var path = Path.Combine(dir, ".order");
            if (!File.Exists(path)) return result;
            try
            {
                foreach (var raw in File.ReadAllText(path).Split('\n'))
                {
                    var line = raw.TrimEnd('\r', ' ', '\t');
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    result.Add(line);
                }
            }
            catch { /* unreadable .order — fall back to alphabetical */ }
            return result;
        }

        private static void AppendToOrder(string dir, string baseName)
        {
            // Never trust the read alone: merge with the pages actually on
            // disk so a failed/partial read can't clobber existing entries.
            // Result: existing listed order, then unlisted pages
            // alphabetically, then the new page last.
            var listed = ReadOrderEntries(dir);
            var present = new List<string>();
            foreach (var file in Directory.GetFiles(dir, "*.md"))
                present.Add(Path.GetFileNameWithoutExtension(file));

            bool Contains(List<string> list, string value)
            {
                foreach (var v in list)
                {
                    if (string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            var result = new List<string>();
            foreach (var e in listed)
            {
                if (Contains(present, e) && !Contains(result, e)) result.Add(e);
            }
            present.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var p in present)
            {
                if (!string.Equals(p, baseName, StringComparison.OrdinalIgnoreCase) && !Contains(result, p))
                    result.Add(p);
            }
            if (!Contains(result, baseName)) result.Add(baseName);

            File.WriteAllText(Path.Combine(dir, ".order"), string.Join("\n", result) + "\n");
        }

        // ── file-system watcher ──────────────────────────────────────────────

        private void StartWatcher()
        {
            if (!Directory.Exists(_wikiRoot)) return;
            _watcher = new FileSystemWatcher(_wikiRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnFsChanged;
            _watcher.Deleted += OnFsChanged;
            _watcher.Renamed += OnFsRenamed;
            _watcher.Changed += OnFsContentChanged;
        }

        private void OnFsContentChanged(object sender, FileSystemEventArgs e)
        {
            // Content changes only matter for .order (re-sort); md content
            // edits don't change the tree.
            if (Path.GetFileName(e.FullPath) == ".order") InvalidateAsync();
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
                RebuildAndNotify();
            });
        }

        private void RebuildAndNotify()
        {
            _nodes.Clear();
            BuildHierarchy();
            foreach (var sink in _sinks)
                sink?.OnInvalidateItems(ItemIdRoot);
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

                case __VSHPROPID.VSHPROPID_IconHandle:
                case __VSHPROPID.VSHPROPID_OpenFolderIconHandle:
                    if (itemid == ItemIdRoot && ProjectIconHandle != IntPtr.Zero)
                    {
                        pvar = unchecked((int)ProjectIconHandle.ToInt64());
                        return VSConstants.S_OK;
                    }
                    return VSConstants.DISP_E_MEMBERNOTFOUND;

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

            if (itemid == ItemIdRoot || !_nodes.TryGetValue(itemid, out var node) || !node.IsPage && node.IsFolder)
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
            => QueryStatusInternal(ref pguidCmdGroup, cCmds, prgCmds);

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
                            _nodes.TryGetValue(itemid, out var node) && (node.IsPage || !node.IsFolder))
                        {
                            var view = VSConstants.LOGVIEWID.Primary_guid;
                            return OpenItem(itemid, ref view, IntPtr.Zero, out _);
                        }
                        break;

                    case (uint)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick:
                        return ShowNodeContextMenu(itemid, pvaIn);
                }
            }

            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
                return ExecStd97(itemid, nCmdID);

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        // ── context menu ─────────────────────────────────────────────────────

        private uint _contextItemId = ItemIdRoot;

        private static System.Drawing.Icon _projectIcon;
        private static bool _projectIconLoadFailed;

        private static IntPtr ProjectIconHandle
        {
            get
            {
                if (_projectIcon == null && !_projectIconLoadFailed)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(typeof(WikidownProject).Assembly.Location) ?? "";
                        _projectIcon = new System.Drawing.Icon(Path.Combine(dir, "Assets", "wikidown.ico"), 16, 16);
                    }
                    catch { _projectIconLoadFailed = true; }
                }
                return _projectIcon?.Handle ?? IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Win32Point lpPoint);

        private int ShowNodeContextMenu(uint itemid, IntPtr pvaIn)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _contextItemId = itemid;

            if (!_nodes.TryGetValue(itemid, out var node))
                return VSConstants.E_FAIL;

            var menuId = itemid == ItemIdRoot ? IDM_VS_CTXT_PROJNODE
                : node.IsFolder ? IDM_VS_CTXT_FOLDERNODE
                : IDM_VS_CTXT_ITEMNODE;

            // pvaIn is a variant holding a packed DWORD: x in the low word,
            // y in the high word. The boxed type varies (UInt32 vs Int32), so
            // convert defensively and fall back to the cursor position.
            var pnts = new POINTS[1];
            var gotPoint = false;
            if (pvaIn != IntPtr.Zero)
            {
                try
                {
                    var packed = unchecked((uint)Convert.ToInt64(Marshal.GetObjectForNativeVariant(pvaIn)));
                    pnts[0].x = unchecked((short)(packed & 0xffff));
                    pnts[0].y = unchecked((short)((packed >> 16) & 0xffff));
                    gotPoint = true;
                }
                catch { /* fall through to cursor position */ }
            }
            if (!gotPoint && GetCursorPos(out var cursor))
            {
                pnts[0].x = (short)cursor.X;
                pnts[0].y = (short)cursor.Y;
            }

            var uiShell = _serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null) return VSConstants.E_FAIL;

            var menuGuid = ShlMainMenu;
            return uiShell.ShowContextMenu(0, ref menuGuid, menuId, pnts, this);
        }

        // ── IOleCommandTarget (routes context-menu commands to this project) ─

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => QueryStatusInternal(ref pguidCmdGroup, cCmds, prgCmds);

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
                return ExecStd97(_contextItemId, nCmdID);
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_UNKNOWNGROUP;
        }

        private int QueryStatusInternal(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && prgCmds != null)
            {
                var handledAny = false;
                for (var i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd97CmdID.AddNewItem:
                        case VSConstants.VSStd97CmdID.AddExistingItem:
                        case VSConstants.VSStd97CmdID.NewFolder:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            handledAny = true;
                            break;
                    }
                }
                if (handledAny) return VSConstants.S_OK;
            }
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private int ExecStd97(uint itemid, uint nCmdID)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.AddNewItem:
                        return AddNewPage(itemid);
                    case VSConstants.VSStd97CmdID.AddExistingItem:
                        return AddExistingPages(itemid);
                    case VSConstants.VSStd97CmdID.NewFolder:
                        return AddNewFolder(itemid);
                }
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    _serviceProvider,
                    ex.Message,
                    "Wikidown",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return VSConstants.E_FAIL;
            }
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        // ── add page commands ────────────────────────────────────────────────

        private string GetTargetDirectory(uint itemid)
        {
            if (itemid == ItemIdRoot || !_nodes.TryGetValue(itemid, out var node))
                return _wikiRoot;
            if (node.ChildDir != null) return node.ChildDir;
            return node.IsFolder ? node.FullPath : (Path.GetDirectoryName(node.FullPath) ?? _wikiRoot);
        }

        private int AddNewPage(uint itemid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dir = GetTargetDirectory(itemid);

            var name = SanitizePageName(PromptForName("Add Wiki Page", "Page name (dashes render as spaces):"));
            if (string.IsNullOrEmpty(name)) return VSConstants.S_OK;

            Directory.CreateDirectory(dir);
            var path = CreatePage(dir, name);

            RebuildAndNotify();
            OpenByPath(path);
            return VSConstants.S_OK;
        }

        private int AddNewFolder(uint itemid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dir = GetTargetDirectory(itemid);

            var name = SanitizePageName(PromptForName("Add Wiki Folder", "Folder name (a same-named page is created with it):"));
            if (string.IsNullOrEmpty(name)) return VSConstants.S_OK;

            // CLI convention: a folder of children is paired with a page of
            // the same name next to it, and the name is recorded in .order.
            Directory.CreateDirectory(Path.Combine(dir, name));
            var path = CreatePage(dir, name);

            RebuildAndNotify();
            OpenByPath(path);
            return VSConstants.S_OK;
        }

        private static string SanitizePageName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // Wiki convention: dashes in file names render as spaces in titles
            name = name.Trim().Replace(' ', '-');
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return name.Length == 0 ? null : name;
        }

        private static string CreatePage(string dir, string name)
        {
            var path = Path.Combine(dir, name + ".md");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "# " + name.Replace('-', ' ') + "\n");
                AppendToOrder(dir, name);
            }
            return path;
        }

        private int AddExistingPages(uint itemid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dir = GetTargetDirectory(itemid);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Add Existing Wiki Pages",
                Filter = "Markdown pages (*.md)|*.md|All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() != true) return VSConstants.S_OK;

            Directory.CreateDirectory(dir);
            foreach (var source in dlg.FileNames)
            {
                var dest = Path.Combine(dir, Path.GetFileName(source));
                if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase) &&
                    !File.Exists(dest))
                {
                    File.Copy(source, dest);
                }
                AppendToOrder(dir, Path.GetFileNameWithoutExtension(dest));
            }

            RebuildAndNotify();
            return VSConstants.S_OK;
        }

        private void OpenByPath(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var kv in _nodes)
            {
                if (string.Equals(kv.Value.FullPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    var view = VSConstants.LOGVIEWID.Primary_guid;
                    OpenItem(kv.Key, ref view, IntPtr.Zero, out _);
                    return;
                }
            }
        }

        private string PromptForName(string title, string label)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var input = new System.Windows.Controls.TextBox { Margin = new System.Windows.Thickness(0, 4, 0, 12), MinWidth = 320 };
            var ok = new System.Windows.Controls.Button { Content = "OK", IsDefault = true, MinWidth = 75, Margin = new System.Windows.Thickness(0, 0, 8, 0) };
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", IsCancel = true, MinWidth = 75 };

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(12) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = label });
            panel.Children.Add(input);
            panel.Children.Add(buttons);

            var dialog = new Microsoft.VisualStudio.PlatformUI.DialogWindow
            {
                Title = title,
                Content = panel,
                SizeToContent = System.Windows.SizeToContent.WidthAndHeight,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.NoResize,
            };
            ok.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            input.Loaded += (s, e) => input.Focus();

            return dialog.ShowModal() == true ? input.Text : null;
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
