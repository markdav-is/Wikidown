using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Wikidown.Core;

namespace Wikidown.Web.Services;

// GitLab REST API v4. The project is addressed as URL-encoded
// "namespace/project"; conflict detection rides on last_commit_id, which
// the files endpoint reports and the commits endpoint enforces per action.
// gitlab.com serves Access-Control-Allow-Origin: * on /api/v4, so the
// browser can call it directly — same no-backend model as GitHub/ADO.
public sealed class GitLabBackend(HttpClient http) : IWikiBackend
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WikiProvider Provider => WikiProvider.GitLab;

    public async Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, folderRelPath);
        var items = await GetTreeAsync(conn, path, recursive: false, ct);

        var entries = new List<RemoteEntry>();
        foreach (var item in items)
        {
            if (item.Type == "tree")
            {
                entries.Add(new RemoteEntry(item.Name, IsFolder: true));
            }
            else if (item.Type == "blob" &&
                     item.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new RemoteEntry(item.Name, IsFolder: false));
            }
        }
        return entries;
    }

    public async Task<RemotePage> ReadPageAsync(
        WikiConnection conn, PagePath page, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, page.ToFilePath().Replace('\\', '/'));
        var file = await GetFileAsync(conn, path, ct)
                   ?? throw new InvalidOperationException("page not found");
        return new RemotePage(page, DecodeContent(file), file.LastCommitId);
    }

    public async Task<WikiSnapshot> WalkAsync(
        WikiConnection conn, CancellationToken ct = default)
    {
        var docs = conn.DocsPath.Trim('/');
        var items = await GetTreeAsync(conn, docs, recursive: true, ct);

        var prefix = docs + "/";
        var pages = new List<PagePath>();
        var orderPaths = new List<(string FolderLink, string ItemPath)>();
        foreach (var item in items)
        {
            if (item.Type != "blob") continue;
            if (!item.Path.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var rel = item.Path[prefix.Length..];
            if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                pages.Add(PagePath.Parse("/" + rel));
            }
            else if (rel == ".order" || rel.EndsWith("/.order", StringComparison.Ordinal))
            {
                var folder = rel == ".order" ? "/" : "/" + rel[..^"/.order".Length];
                orderPaths.Add((folder, item.Path));
            }
        }
        pages.Sort((a, b) => string.CompareOrdinal(a.ToLinkPath(), b.ToLinkPath()));

        var orders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (folderLink, itemPath) in orderPaths)
        {
            var file = await GetFileAsync(conn, itemPath, ct);
            if (file is null) continue;
            orders[folderLink] = OrderFile.Parse(DecodeContent(file));
        }

        return new WikiSnapshot(pages, orders);
    }

    public async Task<CommitResult> WritePageAsync(
        WikiConnection conn, CommitRequest request, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, request.Page.ToFilePath().Replace('\\', '/'));
        var action = request.ExpectedSha is null
            ? new GlAction("create", path, request.Markdown, null)
            : new GlAction("update", path, request.Markdown, request.ExpectedSha);

        var body = new GlCommitRequest(
            Branch: conn.Branch,
            CommitMessage: request.CommitMessage,
            Actions: new[] { action });

        var url = $"{ApiBase(conn)}/projects/{ProjectId(conn)}/repository/commits";
        using var req = Authenticated(HttpMethod.Post, url, conn.Token);
        req.Content = JsonContent.Create(body, options: JsonOpts);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.BadRequest)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            // "…has changed since you started editing…" -> stale last_commit_id;
            // "A file with this name already exists" -> created remotely meanwhile.
            if (detail.Contains("changed since", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                throw new WikiConflictException(
                    "GitLab rejected the commit because the page changed on the server. Reload to merge.");
            }
            throw new InvalidOperationException($"GitLab commit failed (400): {Trim(detail)}");
        }
        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"GitLab commit failed ({(int)res.StatusCode}): {Trim(detail)}");
        }

        var payload = await res.Content.ReadFromJsonAsync<GlCommit>(JsonOpts, ct)
                      ?? throw new InvalidOperationException("empty commit response");
        // The commit just made is now the file's last commit — its id is the
        // next expected last_commit_id.
        return new CommitResult(payload.Id);
    }

    private async Task<List<GlTreeItem>> GetTreeAsync(
        WikiConnection conn, string path, bool recursive, CancellationToken ct)
    {
        var all = new List<GlTreeItem>();
        var page = 1;
        while (true)
        {
            var url = $"{ApiBase(conn)}/projects/{ProjectId(conn)}/repository/tree" +
                      $"?path={Uri.EscapeDataString(path)}" +
                      $"&ref={Uri.EscapeDataString(conn.Branch)}" +
                      $"&recursive={(recursive ? "true" : "false")}" +
                      $"&per_page=100&page={page}";
            using var req = Authenticated(HttpMethod.Get, url, conn.Token);
            using var res = await http.SendAsync(req, ct);

            if (res.StatusCode == HttpStatusCode.NotFound) return all;
            res.EnsureSuccessStatusCode();

            var items = await res.Content.ReadFromJsonAsync<List<GlTreeItem>>(JsonOpts, ct);
            if (items is null || items.Count == 0) return all;
            all.AddRange(items);

            var next = res.Headers.TryGetValues("x-next-page", out var v) ? v.FirstOrDefault() : null;
            if (string.IsNullOrEmpty(next) || !int.TryParse(next, out page)) return all;
        }
    }

    private async Task<GlFile?> GetFileAsync(WikiConnection conn, string path, CancellationToken ct)
    {
        var url = $"{ApiBase(conn)}/projects/{ProjectId(conn)}/repository/files/" +
                  $"{Uri.EscapeDataString(path)}?ref={Uri.EscapeDataString(conn.Branch)}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<GlFile>(JsonOpts, ct);
    }

    private static string DecodeContent(GlFile file) =>
        file.Content is null
            ? string.Empty
            : file.Encoding == "base64"
                ? Encoding.UTF8.GetString(Convert.FromBase64String(file.Content.Replace("\n", "")))
                : file.Content;

    private static string ApiBase(WikiConnection conn) =>
        (string.IsNullOrWhiteSpace(conn.Host) ? "https://gitlab.com" : conn.Host.TrimEnd('/')) + "/api/v4";

    private static string ProjectId(WikiConnection conn) =>
        Uri.EscapeDataString($"{conn.Owner.Trim('/')}/{conn.Repo.Trim('/')}");

    // Empty token -> anonymous (public projects are readable without auth).
    private static HttpRequestMessage Authenticated(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Add("PRIVATE-TOKEN", token.Trim());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private static string Combine(string basePath, string relPath)
    {
        var a = basePath.Trim('/');
        var b = relPath.Trim('/');
        if (string.IsNullOrEmpty(b)) return a;
        if (string.IsNullOrEmpty(a)) return b;
        return $"{a}/{b}";
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "…" : s;

    private sealed record GlTreeItem(string Id, string Name, string Type, string Path);
    private sealed record GlFile(string? Content, string? Encoding, string? BlobId, string? LastCommitId);
    private sealed record GlCommitRequest(string Branch, string CommitMessage, IReadOnlyList<GlAction> Actions);
    private sealed record GlAction(string Action, string FilePath, string Content, string? LastCommitId);
    private sealed record GlCommit(string Id);
}
