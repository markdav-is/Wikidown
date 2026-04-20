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

    public async Task<IReadOnlyList<PagePath>> WalkAsync(
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

            var docsPrefix = conn.DocsPath.TrimEnd('/') + "/";
            var pages = new List<PagePath>();
            foreach (var entry in tree.Tree)
            {
                if (entry.Type != "blob") continue;
                if (!entry.Path.StartsWith(docsPrefix, StringComparison.Ordinal)) continue;
                if (!entry.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;

                var rel = entry.Path[docsPrefix.Length..];
                pages.Add(PagePath.Parse("/" + rel));
            }
            pages.Sort((a, b) => string.CompareOrdinal(a.ToLinkPath(), b.ToLinkPath()));
            return pages;
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

    private static HttpRequestMessage Authenticated(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
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
    private sealed record GhPutRequest(string Message, string Content, string Branch, string? Sha);
    private sealed record GhPutResponse(GhContent Content);
}
