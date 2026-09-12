using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Wikidown.Core;

namespace Wikidown.Mcp;

[McpServerToolType]
public sealed class WikiTools(WikiRepository repo)
{
    // Hosts dispatch tool calls concurrently, and two patches of the same
    // page at once collide on the file. Every tool is fast and synchronous,
    // so serializing them costs nothing noticeable.
    private static readonly object Gate = new();

    // The SDK reports every exception except McpException as a bare
    // "An error occurred invoking '<tool>'", which hides the message that
    // tells the agent what to do next (list of headings, match count, ...).
    private static string Guarded(Func<string> tool)
    {
        try
        {
            lock (Gate) return tool();
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }

    [McpServerTool(Name = "wiki_list")]
    [Description("List child pages of a wiki page (or root). Returns one entry per child as 'path<TAB>title'.")]
    public string List(
        [Description("Wiki link path (e.g. '/' or '/Getting-Started'). Defaults to root.")]
        string path = "/") => Guarded(() =>
    {
        var parent = PagePath.Parse(path);
        var children = repo.ListChildren(parent);
        if (children.Count == 0) return "(no children)";
        return string.Join("\n",
            children.Select(c => $"{c.ToLinkPath()}\t{c.Name.Title}"));
    });

    [McpServerTool(Name = "wiki_read")]
    [Description("Read a wiki page's markdown content, or just one section of it. Pass section to get a single " +
                 "heading plus everything below it up to the next heading of the same or higher level (a ## " +
                 "section includes its ### children) — prefer this on long pages when you only need one part. " +
                 "A section miss fails with the page's headings listed so the next call can hit.")]
    public string Read(
        [Description("Wiki link path of the page (e.g. '/Getting-Started/Format').")]
        string path,
        [Description("Optional heading text, matched case-insensitively and ignoring leading #s and whitespace " +
                     "(e.g. 'Open concerns' or '## Open concerns'). Omit to read the whole page.")]
        string? section = null) => Guarded(() =>
    {
        var p = PagePath.Parse(path);
        if (string.IsNullOrWhiteSpace(section)) return repo.Read(p).Markdown;
        var result = repo.ReadSection(p, section);
        if (result.Note is null) return result.Markdown;
        var ending = result.Markdown.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return result.Markdown + result.Note + ending;
    });

    [McpServerTool(Name = "wiki_write")]
    [Description("Create or overwrite a wiki page with the given markdown content. " +
                 "Updates the parent .order automatically. Use only for new pages or full rewrites; " +
                 "for anything smaller, use wiki_edit.")]
    public string Write(
        [Description("Wiki link path of the page.")] string path,
        [Description("Full markdown body to write to the page.")] string markdown) => Guarded(() =>
    {
        var p = PagePath.Parse(path);
        repo.Write(new WikiPage(p, markdown));
        return $"wrote {p.ToLinkPath()}";
    });

    [McpServerTool(Name = "wiki_edit")]
    [Description("Replace an exact substring of a page's markdown in place. Prefer this over wiki_write for " +
                 "any change smaller than a full rewrite: it costs only the changed text and cannot drift the rest " +
                 "of the page. Matches the raw markdown exactly (line endings are normalized to the file's, so CRLF " +
                 "vs LF never matters); fails if old is not found or matches more than once (unless replaceAll). " +
                 "Never touches the breadcrumb line or .order, and never creates a page. Returns the changed line " +
                 "numbers with two lines of context so you need not re-read the page.")]
    public string Edit(
        [Description("Wiki link path of the page (e.g. '/Getting-Started/Format').")] string path,
        [Description("Exact text currently on the page. May span lines.")] string old,
        [Description("Replacement text. Empty deletes old.")] string @new,
        [Description("Replace every occurrence instead of failing when old matches more than once. Defaults to false.")]
        bool replaceAll = false) => Guarded(() =>
        repo.Edit(PagePath.Parse(path), old, @new, replaceAll).Summary);

    [McpServerTool(Name = "wiki_new")]
    [Description("Create a new wiki page. Fails if it already exists. " +
                 "If markdown is empty, seeds the file with an H1 of the title.")]
    public string New(
        [Description("Wiki link path for the new page.")] string path,
        [Description("Optional title; defaults to the page name from the path.")] string? title = null,
        [Description("Optional initial markdown body.")] string? markdown = null) => Guarded(() =>
    {
        var p = PagePath.Parse(path);
        if (repo.Exists(p))
            throw new InvalidOperationException($"page already exists: {p.ToLinkPath()}");
        var body = !string.IsNullOrEmpty(markdown)
            ? markdown
            : $"# {title ?? p.Name.Title}\n\n";
        repo.Write(new WikiPage(p, body));
        return $"created {p.ToLinkPath()}";
    });

    [McpServerTool(Name = "wiki_move")]
    [Description("Rename or move a wiki page (and its subpages folder if present). " +
                 "Rewrites inbound links across the wiki and the moved page's own " +
                 "relative links/images for their new depth.")]
    public string Move(
        [Description("Source wiki link path.")] string from,
        [Description("Destination wiki link path.")] string to) => Guarded(() =>
    {
        var src = PagePath.Parse(from);
        var dst = PagePath.Parse(to);
        var rewrites = MoveLinkRewriter.MoveAndRewrite(repo, src, dst);
        var summary = $"moved {src.ToLinkPath()} -> {dst.ToLinkPath()}; {rewrites.Count} link(s) rewritten";
        if (rewrites.Count == 0) return summary;
        var detail = string.Join("\n",
            rewrites.Select(r => $"  {r.Page.ToLinkPath()}:{r.LineNumber}: {r.OldTarget} -> {r.NewTarget}"));
        return $"{summary}\n{detail}";
    });

    [McpServerTool(Name = "wiki_delete")]
    [Description("Delete a wiki page. Pass recursive=true to also delete subpages.")]
    public string Delete(
        [Description("Wiki link path.")] string path,
        [Description("Delete subpages folder too. Defaults to false.")] bool recursive = false) => Guarded(() =>
    {
        var p = PagePath.Parse(path);
        repo.Delete(p, deleteSubpages: recursive);
        return $"deleted {p.ToLinkPath()}";
    });

    [McpServerTool(Name = "wiki_reorder")]
    [Description("Rewrite the .order file of a folder with the given page base-names in order.")]
    public string Reorder(
        [Description("Folder wiki link path (use '/' for root).")] string folder,
        [Description("Page base-names (no .md), in the desired order.")] string[] names) => Guarded(() =>
    {
        var f = PagePath.Parse(folder);
        repo.WriteOrder(f, names);
        return $"reordered {f.ToLinkPath()} ({names.Length} entries)";
    });

    [McpServerTool(Name = "wiki_search")]
    [Description("Search every page body for a literal substring. Returns 'path:line: text' per hit.")]
    public string Search(
        [Description("Substring to search for.")] string query,
        [Description("Match case exactly. Defaults to false.")] bool caseSensitive = false) => Guarded(() =>
    {
        var hits = PageSearch.Search(repo, query, caseSensitive).ToList();
        if (hits.Count == 0) return "(no matches)";
        return string.Join("\n",
            hits.Select(h => $"{h.Path.ToLinkPath()}:{h.LineNumber}: {h.Line}"));
    });

    [McpServerTool(Name = "wiki_walk")]
    [Description("List every page in the wiki, depth-first, in display order. Returns one link path per line.")]
    public string Walk() => Guarded(() =>
    {
        var paths = repo.Walk().Select(p => p.ToLinkPath()).ToList();
        return paths.Count == 0 ? "(empty wiki)" : string.Join("\n", paths);
    });
}
