using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Wikidown.Core;

namespace Wikidown.Web.Services;

public sealed class GitHubBackend(HttpClient http) : IWikiBackend
{
    private const string ApiBase = "https://api.github.com";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public WikiProvider Provider => WikiProvider.GitHub;

    public async Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, folderRelPath);
        var url = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(conn.Branch)}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound) return Array.Empty<RemoteEntry>();
        res.EnsureSuccessStatusCode();

        var items = await res.Content.ReadFromJsonAsync<List<GhContent>>(cancellationToken: ct)
                    ?? new List<GhContent>();

        var entries = new List<RemoteEntry>();
        foreach (var item in items)
        {
            if (item.Type == "dir")
            {
                entries.Add(new RemoteEntry(item.Name, IsFolder: true));
            }
            else if (item.Type == "file" &&
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
        var url = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(conn.Branch)}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);

        res.EnsureSuccessStatusCode();
        var item = await res.Content.ReadFromJsonAsync<GhContent>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("empty response");

        var markdown = item.Content is null
            ? string.Empty
            : Encoding.UTF8.GetString(Convert.FromBase64String(item.Content.Replace("\n", "")));
        return new RemotePage(page, markdown, item.Sha);
    }

    public async Task<WikiSnapshot> WalkAsync(
        WikiConnection conn, CancellationToken ct = default)
    {
        var branchUrl = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/branches/{Uri.EscapeDataString(conn.Branch)}";
        using (var req = Authenticated(HttpMethod.Get, branchUrl, conn.Token))
        using (var res = await http.SendAsync(req, ct))
        {
            res.EnsureSuccessStatusCode();
            var branch = await res.Content.ReadFromJsonAsync<GhBranch>(cancellationToken: ct)
                         ?? throw new InvalidOperationException("branch missing");

            var treeUrl = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/git/trees/{branch.Commit.Sha}?recursive=1";
            using var treeReq = Authenticated(HttpMethod.Get, treeUrl, conn.Token);
            using var treeRes = await http.SendAsync(treeReq, ct);
            treeRes.EnsureSuccessStatusCode();
            var tree = await treeRes.Content.ReadFromJsonAsync<GhTree>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("tree missing");

            var docsPrefix = DocsPrefix(conn);
            var pages = new List<PagePath>();
            var orderBlobs = new List<(string FolderLink, string Sha)>();
            foreach (var entry in tree.Tree)
            {
                if (entry.Type != "blob") continue;
                if (!entry.Path.StartsWith(docsPrefix, StringComparison.Ordinal)) continue;

                var rel = entry.Path[docsPrefix.Length..];
                if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    pages.Add(PagePath.Parse("/" + rel));
                }
                else if (rel == ".order" || rel.EndsWith("/.order", StringComparison.Ordinal))
                {
                    var folder = rel == ".order" ? "/" : "/" + rel[..^"/.order".Length];
                    orderBlobs.Add((folder, entry.Sha));
                }
            }
            pages.Sort((a, b) => string.CompareOrdinal(a.ToLinkPath(), b.ToLinkPath()));

            var orders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (folderLink, sha) in orderBlobs)
            {
                var blobUrl = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/git/blobs/{sha}";
                using var blobReq = Authenticated(HttpMethod.Get, blobUrl, conn.Token);
                using var blobRes = await http.SendAsync(blobReq, ct);
                if (!blobRes.IsSuccessStatusCode) continue;
                var blob = await blobRes.Content.ReadFromJsonAsync<GhContent>(cancellationToken: ct);
                if (blob?.Content is null) continue;
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content.Replace("\n", "")));
                orders[folderLink] = OrderFile.Parse(text);
            }

            return new WikiSnapshot(pages, orders);
        }
    }

    public async Task<CommitResult> WritePageAsync(
        WikiConnection conn, CommitRequest request, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, request.Page.ToFilePath().Replace('\\', '/'));
        var url = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/contents/{EscapePath(path)}";

        var body = new GhPutRequest(
            Message: request.CommitMessage,
            Content: Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Markdown)),
            Branch: conn.Branch,
            Sha: request.ExpectedSha);

        using var req = Authenticated(HttpMethod.Put, url, conn.Token);
        req.Content = JsonContent.Create(body, options: JsonOpts);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.Conflict ||
            (res.StatusCode == HttpStatusCode.UnprocessableEntity && request.ExpectedSha is not null))
        {
            throw new WikiConflictException(
                "GitHub rejected the commit because the page changed on the server. Reload to merge.");
        }

        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"GitHub commit failed ({(int)res.StatusCode}): {Trim(detail)}");
        }

        var payload = await res.Content.ReadFromJsonAsync<GhPutResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("empty commit response");
        return new CommitResult(payload.Content.Sha ?? string.Empty);
    }

    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public async Task<WikiFiles> ReadAllAsync(WikiConnection conn, CancellationToken ct = default)
    {
        var repo = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}";
        var branch = await GetAsync<GhBranch>(conn, $"{repo}/branches/{Uri.EscapeDataString(conn.Branch)}", ct);
        var tree = await GetAsync<GhTree>(conn, $"{repo}/git/trees/{branch.Commit.Sha}?recursive=1", ct);

        var prefix = DocsPrefix(conn);
        var wanted = tree.Tree
            .Where(e => e.Type == "blob" && e.Path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => (Rel: e.Path[prefix.Length..], e.Sha))
            .Where(e => e.Rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                        || e.Rel == ".order" || e.Rel.EndsWith("/.order", StringComparison.Ordinal))
            .ToList();

        // Blobs are immutable, so only what changed since the last read is fetched.
        using var gate = new SemaphoreSlim(8);
        await Task.WhenAll(wanted.Where(w => !_blobs.ContainsKey(w.Sha)).Select(w => w.Sha).Distinct().Select(async sha =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var blob = await GetAsync<GhContent>(conn, $"{repo}/git/blobs/{sha}", ct);
                _blobs[sha] = Convert.FromBase64String((blob.Content ?? "").Replace("\n", ""));
            }
            finally { gate.Release(); }
        }));

        return new WikiFiles(branch.Commit.Sha, wanted.ToDictionary(w => w.Rel, w => _blobs[w.Sha], StringComparer.Ordinal));
    }

    // The Git Data API rather than the Contents API: one commit for any
    // number of files, so a move never leaves the wiki half-renamed.
    public async Task CommitAsync(
        WikiConnection conn, IReadOnlyList<FileChange> changes, string message,
        string? expectedHead = null, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;
        var repo = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}";
        var refPath = "heads/" + EscapePath(conn.Branch);

        var head = expectedHead
                   ?? (await GetAsync<GhRef>(conn, $"{repo}/git/ref/{refPath}", ct)).Object.Sha;
        var parent = await GetAsync<GhGitCommit>(conn, $"{repo}/git/commits/{head}", ct);

        var entries = new List<Dictionary<string, object?>>();
        foreach (var change in changes)
        {
            string? sha = null;
            if (change.Content is not null)
            {
                var blob = await SendAsync<GhSha>(conn, HttpMethod.Post, $"{repo}/git/blobs",
                    new { content = Convert.ToBase64String(change.Content), encoding = "base64" }, ct);
                sha = blob.Sha;
                _blobs[sha] = change.Content;
            }
            entries.Add(new()
            {
                ["path"] = Combine(conn.DocsPath, change.Path),
                ["mode"] = "100644",
                ["type"] = "blob",
                ["sha"] = sha,
            });
        }

        var tree = await SendAsync<GhSha>(conn, HttpMethod.Post, $"{repo}/git/trees",
            new Dictionary<string, object?> { ["base_tree"] = parent.Tree.Sha, ["tree"] = entries }, ct);
        var commit = await SendAsync<GhSha>(conn, HttpMethod.Post, $"{repo}/git/commits",
            new { message, tree = tree.Sha, parents = new[] { head } }, ct);

        using var req = Authenticated(HttpMethod.Patch, $"{repo}/git/refs/{refPath}", conn.Token);
        req.Content = JsonContent.Create(new { sha = commit.Sha, force = false });
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
            throw new WikiConflictException("The wiki changed on GitHub while you were working. Try again.");
        await EnsureOkAsync(res, ct);
    }

    private async Task<T> GetAsync<T>(WikiConnection conn, string url, CancellationToken ct)
    {
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);
        await EnsureOkAsync(res, ct);
        return await res.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
               ?? throw new InvalidOperationException("empty response");
    }

    private async Task<T> SendAsync<T>(WikiConnection conn, HttpMethod method, string url, object body, CancellationToken ct)
    {
        using var req = Authenticated(method, url, conn.Token);
        req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req, ct);
        await EnsureOkAsync(res, ct);
        return await res.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
               ?? throw new InvalidOperationException("empty response");
    }

    private static async Task EnsureOkAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var detail = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(res.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "GitHub needs you to sign in (again) before this repository can be changed.",
            HttpStatusCode.Forbidden or HttpStatusCode.NotFound =>
                $"GitHub refused ({(int)res.StatusCode}). Check that you can write to this repository: {Trim(detail)}",
            _ => $"GitHub request failed ({(int)res.StatusCode}): {Trim(detail)}",
        });
    }

    private static string DocsPrefix(WikiConnection conn)
    {
        var docs = conn.DocsPath.Trim('/');
        return docs.Length == 0 ? "" : docs + "/";
    }

    public async Task<byte[]?> ReadBytesAsync(
        WikiConnection conn, string docsRelPath, CancellationToken ct = default)
    {
        var path = Combine(conn.DocsPath, docsRelPath);
        var url = $"{ApiBase}/repos/{conn.Owner}/{conn.Repo}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(conn.Branch)}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    private static HttpRequestMessage Authenticated(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        // Empty token -> anonymous (public repos are readable without auth).
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private static string EscapePath(string path) =>
        Uri.EscapeDataString(path).Replace("%2F", "/");

    private static string Combine(string basePath, string relPath)
    {
        var a = basePath.Trim('/');
        var b = relPath.Trim('/');
        if (string.IsNullOrEmpty(b)) return a;
        if (string.IsNullOrEmpty(a)) return b;
        return $"{a}/{b}";
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "…" : s;

    private sealed record GhContent(string Name, string Path, string Type, string? Sha, string? Content, string? Encoding);
    private sealed record GhBranch(GhCommit Commit);
    private sealed record GhCommit(string Sha);
    private sealed record GhTree(string Sha, List<GhTreeEntry> Tree, bool Truncated);
    private sealed record GhTreeEntry(string Path, string Type, string Sha);
    private sealed record GhRef(GhSha Object);
    private sealed record GhSha(string Sha);
    private sealed record GhGitCommit(string Sha, GhSha Tree);
    private sealed record GhPutRequest(string Message, string Content, string Branch, string? Sha);
    private sealed record GhPutResponse(GhContent Content);
}
