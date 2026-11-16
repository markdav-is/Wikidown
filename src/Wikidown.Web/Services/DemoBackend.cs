using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Wikidown.Core;

namespace Wikidown.Web.Services;

/// <summary>
/// A sample wiki that lives only in this browser (localStorage), so the
/// editor can be tried — pages, folders, images, the kanban — without
/// signing in to anything.
/// </summary>
public sealed class DemoBackend(IJSRuntime js) : IWikiBackend
{
    private const string StorageKey = "wikidown.demo.v1";

    public static WikiConnection Connection { get; } =
        new(WikiProvider.Demo, "", "demo", "sample-wiki", "main", "docs");

    private Dictionary<string, byte[]>? _files;

    public WikiProvider Provider => WikiProvider.Demo;

    public async Task ResetAsync()
    {
        _files = Seed();
        await SaveAsync();
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        var prefix = folderRelPath.Trim('/');
        if (prefix.Length > 0) prefix += "/";
        return files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..].Split('/'))
            .Select(parts => new RemoteEntry(parts[0], IsFolder: parts.Length > 1))
            .Where(e => e.IsFolder || e.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    public async Task<RemotePage> ReadPageAsync(WikiConnection conn, PagePath page, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        return files.TryGetValue(Rel(page), out var bytes)
            ? new RemotePage(page, Encoding.UTF8.GetString(bytes), Hash(bytes))
            : throw new InvalidOperationException("page not found");
    }

    public async Task<WikiSnapshot> WalkAsync(WikiConnection conn, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        var pages = files.Keys
            .Where(k => k.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(k => PagePath.Parse("/" + k))
            .OrderBy(p => p.ToLinkPath(), StringComparer.Ordinal)
            .ToList();
        var orders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rel, bytes) in files)
        {
            if (rel != ".order" && !rel.EndsWith("/.order", StringComparison.Ordinal)) continue;
            var folder = rel == ".order" ? "/" : "/" + rel[..^"/.order".Length];
            orders[folder] = OrderFile.Parse(Encoding.UTF8.GetString(bytes));
        }
        return new WikiSnapshot(pages, orders);
    }

    public async Task<CommitResult> WritePageAsync(WikiConnection conn, CommitRequest request, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        var rel = Rel(request.Page);
        var current = files.TryGetValue(rel, out var existing) ? Hash(existing) : null;
        if (current != request.ExpectedSha)
            throw new WikiConflictException("The page changed since it was loaded. Reload to merge.");
        var bytes = Encoding.UTF8.GetBytes(request.Markdown);
        files[rel] = bytes;
        await SaveAsync();
        return new CommitResult(Hash(bytes));
    }

    public async Task<WikiFiles> ReadAllAsync(WikiConnection conn, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        return new WikiFiles(null, files
            .Where(f => f.Key.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f.Key) == ".order")
            .ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal));
    }

    public async Task CommitAsync(
        WikiConnection conn, IReadOnlyList<FileChange> changes, string message,
        string? expectedHead = null, CancellationToken ct = default)
    {
        var files = await FilesAsync();
        foreach (var change in changes)
        {
            if (change.Content is null) files.Remove(change.Path);
            else files[change.Path] = change.Content;
        }
        await SaveAsync();
    }

    public async Task<byte[]?> ReadBytesAsync(WikiConnection conn, string docsRelPath, CancellationToken ct = default) =>
        (await FilesAsync()).GetValueOrDefault(docsRelPath.Trim('/'));

    private async Task<Dictionary<string, byte[]>> FilesAsync()
    {
        if (_files is not null) return _files;
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (stored is { Count: > 0 })
                    return _files = stored.ToDictionary(f => f.Key, f => Convert.FromBase64String(f.Value), StringComparer.Ordinal);
            }
            catch (Exception e) when (e is JsonException or FormatException) { }
        }
        return _files = Seed();
    }

    private ValueTask SaveAsync() =>
        js.InvokeVoidAsync("localStorage.setItem", StorageKey,
            JsonSerializer.Serialize(_files!.ToDictionary(f => f.Key, f => Convert.ToBase64String(f.Value))));

    private static string Rel(PagePath page) => page.ToFilePath().Replace('\\', '/');

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA1.HashData(bytes));

    // Built with the real repository code so the sample has the same
    // breadcrumbs and .order files a CLI-made wiki has.
    private static Dictionary<string, byte[]> Seed()
    {
        var root = Path.Combine(Path.GetTempPath(), "wikidown-demo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repo = new WikiRepository(root, LineEndings.Lf);
            void Page(string path, string markdown) => repo.Write(new WikiPage(PagePath.Parse(path), markdown));

            Page("/Home",
                "# Sample wiki\n\nThis wiki lives only in your browser, so change anything you like.\n\n" +
                "- [Getting Started](Getting-Started.md)\n- [Kanban](Kanban.md)\n\n" +
                "## Things to try\n\n- Press **Edit** and type; the preview follows along.\n" +
                "- Paste a screenshot straight into the editor.\n" +
                "- Use the **⋮** menu next to a page in the tree to rename, move or delete it.\n" +
                "- Open the [Kanban](Kanban.md) page and drag a card to another column.\n");
            Page("/Getting-Started",
                "# Getting Started\n\nA wiki is a folder of Markdown files. A page with subpages is a file plus a folder of the same name.\n\n" +
                "- [Writing pages](Getting-Started/Writing-pages.md)\n");
            Page("/Getting-Started/Writing-pages",
                "# Writing pages\n\nPages are plain Markdown:\n\n| Syntax | Result |\n| --- | --- |\n| `**bold**` | **bold** |\n| `_italic_` | _italic_ |\n\n" +
                "- [x] Task lists work\n- [ ] So do tables and code blocks\n\n```powershell\nwikidown new --path Notes/Idea\n```\n");

            var board = PagePath.Parse("/Kanban");
            KanbanBoard.Create(repo, board);
            var todo = PagePath.Parse("/Kanban/To-Do");
            KanbanBoard.AddCard(repo, board, todo, "Write the release notes");
            KanbanBoard.AddCard(repo, board, todo, "Invite the team");
            KanbanBoard.AddCard(repo, board, PagePath.Parse("/Kanban/Doing"), "Try the editor");
            KanbanBoard.AddCard(repo, board, PagePath.Parse("/Kanban/Done"), "Open the sample wiki");

            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
                f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
