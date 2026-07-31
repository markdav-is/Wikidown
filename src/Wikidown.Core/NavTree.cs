namespace Wikidown.Core;

// A rendered navigation node: a page (optionally with subpages, when a
// same-named sibling folder holds children) or a bare folder with no paired
// page. Mirrors how Azure DevOps wikis and the VS extension present the tree.
public sealed record NavNode(
    PagePath Path,
    string Title,
    bool IsPage,
    IReadOnlyList<NavNode> Children);

public static class NavTree
{
    /// <summary>
    /// Builds the navigation tree for a wiki from the flat list of page
    /// paths. <paramref name="orderForFolder"/> supplies the .order entries
    /// for a folder (keyed by the folder's PagePath; return empty when there
    /// is none): listed entries sort first in listed order, everything else
    /// follows alphabetically.
    /// </summary>
    public static IReadOnlyList<NavNode> Build(
        IReadOnlyList<PagePath> pages,
        Func<PagePath, IReadOnlyList<string>> orderForFolder)
    {
        var pagesByParent = new Dictionary<string, List<PagePath>>(StringComparer.OrdinalIgnoreCase);
        var foldersByParent = new Dictionary<string, Dictionary<string, PagePath>>(StringComparer.OrdinalIgnoreCase);

        void RecordFolder(PagePath folder)
        {
            var parentLink = folder.Parent.ToLinkPath();
            if (!foldersByParent.TryGetValue(parentLink, out var set))
                foldersByParent[parentLink] = set = new Dictionary<string, PagePath>(StringComparer.OrdinalIgnoreCase);
            set[folder.ToLinkPath()] = folder;
        }

        foreach (var page in pages)
        {
            var parentLink = page.Parent.ToLinkPath();
            if (!pagesByParent.TryGetValue(parentLink, out var list))
                pagesByParent[parentLink] = list = new List<PagePath>();
            list.Add(page);

            for (var folder = page.Parent; !folder.IsRoot; folder = folder.Parent)
                RecordFolder(folder);
        }

        return BuildChildren(PagePath.Root, pagesByParent, foldersByParent, orderForFolder);
    }

    private static IReadOnlyList<NavNode> BuildChildren(
        PagePath folder,
        Dictionary<string, List<PagePath>> pagesByParent,
        Dictionary<string, Dictionary<string, PagePath>> foldersByParent,
        Func<PagePath, IReadOnlyList<string>> orderForFolder)
    {
        var link = folder.ToLinkPath();
        pagesByParent.TryGetValue(link, out var pages);
        foldersByParent.TryGetValue(link, out var subfolders);
        if (pages is null && subfolders is null) return Array.Empty<NavNode>();

        var remainingFolders = subfolders is null
            ? new Dictionary<string, PagePath>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, PagePath>(subfolders, StringComparer.OrdinalIgnoreCase);

        var nodes = new List<(NavNode Node, string FileBase)>();

        if (pages is not null)
        {
            foreach (var page in pages)
            {
                // A page with a same-named sibling folder absorbs it: one
                // node, expandable into the folder's children.
                var children = remainingFolders.Remove(page.ToLinkPath())
                    ? BuildChildren(page, pagesByParent, foldersByParent, orderForFolder)
                    : Array.Empty<NavNode>();
                nodes.Add((new NavNode(page, page.Name.Title, IsPage: true, children), page.Name.FileBase));
            }
        }

        foreach (var bare in remainingFolders.Values)
        {
            var children = BuildChildren(bare, pagesByParent, foldersByParent, orderForFolder);
            nodes.Add((new NavNode(bare, bare.Name.Title, IsPage: false, children), bare.Name.FileBase));
        }

        var order = orderForFolder(folder);
        int OrderKey(string fileBase)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], fileBase, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return int.MaxValue;
        }

        return nodes
            .OrderBy(n => OrderKey(n.FileBase))
            .ThenBy(n => n.FileBase, StringComparer.OrdinalIgnoreCase)
            .Select(n => n.Node)
            .ToList();
    }
}
