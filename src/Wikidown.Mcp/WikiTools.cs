using System.ComponentModel;
using ModelContextProtocol.Server;
using Wikidown.Core;

namespace Wikidown.Mcp;

[McpServerToolType]
public sealed class WikiTools(WikiRepository repo)
{
    [McpServerTool(Name = "wiki_list")]
    [Description("List child pages of a wiki page (or root). Returns one entry per child as 'path<TAB>title'.")]
    public string List(
        [Description("Wiki link path (e.g. '/' or '/Getting-Started'). Defaults to root.")]
        string path = "/")
    {
        var parent = PagePath.Parse(path);
        var children = repo.ListChildren(parent);
        if (children.Count == 0) return "(no children)";
        return string.Join("\n",
            children.Select(c => $"{c.ToLinkPath()}\t{c.Name.Title}"));
    }

    [McpServerTool(Name = "wiki_read")]
    [Description("Read a wiki page's markdown content.")]
    public string Read(
        [Description("Wiki link path of the page (e.g. '/Getting-Started/Format').")]
        string path)
    {
        return repo.Read(PagePath.Parse(path)).Markdown;
    }

    [McpServerTool(Name = "wiki_write")]
    [Description("Create or overwrite a wiki page with the given markdown content. " +
                 "Updates the parent .order automatically.")]
    public string Write(
        [Description("Wiki link path of the page.")] string path,
        [Description("Full markdown body to write to the page.")] string markdown)
    {
        var p = PagePath.Parse(path);
        repo.Write(new WikiPage(p, markdown));
        return $"wrote {p.ToLinkPath()}";
    }

    [McpServerTool(Name = "wiki_new")]
    [Description("Create a new wiki page. Fails if it already exists. " +
                 "If markdown is empty, seeds the file with an H1 of the title.")]
    public string New(
        [Description("Wiki link path for the new page.")] string path,
        [Description("Optional title; defaults to the page name from the path.")] string? title = null,
        [Description("Optional initial markdown body.")] string? markdown = null)
    {
        var p = PagePath.Parse(path);
        if (repo.Exists(p))
            throw new InvalidOperationException($"page already exists: {p.ToLinkPath()}");
        var body = !string.IsNullOrEmpty(markdown)
            ? markdown
            : $"# {title ?? p.Name.Title}\n\n";
        repo.Write(new WikiPage(p, body));
        return $"created {p.ToLinkPath()}";
    }

    [McpServerTool(Name = "wiki_move")]
    [Description("Rename or move a wiki page (and its subpages folder if present).")]
    public string Move(
        [Description("Source wiki link path.")] string from,
        [Description("Destination wiki link path.")] string to)
    {
        var src = PagePath.Parse(from);
        var dst = PagePath.Parse(to);
        repo.Move(src, dst);
        return $"moved {src.ToLinkPath()} -> {dst.ToLinkPath()}";
    }

    [McpServerTool(Name = "wiki_delete")]
    [Description("Delete a wiki page. Pass recursive=true to also delete subpages.")]
    public string Delete(
        [Description("Wiki link path.")] string path,
        [Description("Delete subpages folder too. Defaults to false.")] bool recursive = false)
    {
        var p = PagePath.Parse(path);
        repo.Delete(p, deleteSubpages: recursive);
        return $"deleted {p.ToLinkPath()}";
    }

    [McpServerTool(Name = "wiki_reorder")]
    [Description("Rewrite the .order file of a folder with the given page base-names in order.")]
    public string Reorder(
        [Description("Folder wiki link path (use '/' for root).")] string folder,
        [Description("Page base-names (no .md), in the desired order.")] string[] names)
    {
        var f = PagePath.Parse(folder);
        repo.WriteOrder(f, names);
        return $"reordered {f.ToLinkPath()} ({names.Length} entries)";
    }

    [McpServerTool(Name = "wiki_search")]
    [Description("Search every page body for a literal substring. Returns 'path:line: text' per hit.")]
    public string Search(
        [Description("Substring to search for.")] string query,
        [Description("Match case exactly. Defaults to false.")] bool caseSensitive = false)
    {
        var hits = PageSearch.Search(repo, query, caseSensitive).ToList();
        if (hits.Count == 0) return "(no matches)";
        return string.Join("\n",
            hits.Select(h => $"{h.Path.ToLinkPath()}:{h.LineNumber}: {h.Line}"));
    }

    [McpServerTool(Name = "wiki_walk")]
    [Description("List every page in the wiki, depth-first, in display order. Returns one link path per line.")]
    public string Walk()
    {
        var paths = repo.Walk().Select(p => p.ToLinkPath()).ToList();
        return paths.Count == 0 ? "(empty wiki)" : string.Join("\n", paths);
    }
}
