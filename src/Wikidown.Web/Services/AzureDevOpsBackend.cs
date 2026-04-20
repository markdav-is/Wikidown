using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Wikidown.Core;

namespace Wikidown.Web.Services;

public sealed class AzureDevOpsBackend(HttpClient http) : IWikiBackend
{
    private const string ApiVersion = "7.1";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public WikiProvider Provider => WikiProvider.AzureDevOps;

    public async Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default)
    {
        var scope = "/" + Combine(conn.DocsPath, folderRelPath);
        var url = $"{ItemsBase(conn)}?scopePath={Uri.EscapeDataString(scope)}" +
                  $"&recursionLevel=OneLevel&includeContentMetadata=false" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(conn.Branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version={ApiVersion}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound) return Array.Empty<RemoteEntry>();
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<AdoItemList>(cancellationToken: ct);
        if (payload?.Value is null) return Array.Empty<RemoteEntry>();

        var entries = new List<RemoteEntry>();
        foreach (var item in payload.Value)
        {
            if (string.Equals(item.Path, scope, StringComparison.OrdinalIgnoreCase)) continue;
            var name = item.Path.Split('/').Last();
            if (string.Equals(item.GitObjectType, "tree", StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new RemoteEntry(name, IsFolder: true));
            }
            else if (string.Equals(item.GitObjectType, "blob", StringComparison.OrdinalIgnoreCase) &&
                     name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new RemoteEntry(name, IsFolder: false));
            }
        }
        return entries;
    }

    public async Task<RemotePage> ReadPageAsync(
        WikiConnection conn, PagePath page, CancellationToken ct = default)
    {
        var path = "/" + Combine(conn.DocsPath, page.ToFilePath().Replace('\\', '/'));
        var item = await GetItemAsync(conn, path, includeContent: true, ct)
                   ?? throw new InvalidOperationException("page not found");
        return new RemotePage(page, item.Content ?? string.Empty, item.ObjectId);
    }

    public async Task<IReadOnlyList<PagePath>> WalkAsync(
        WikiConnection conn, CancellationToken ct = default)
    {
        var scope = "/" + conn.DocsPath.Trim('/');
        var url = $"{ItemsBase(conn)}?scopePath={Uri.EscapeDataString(scope)}" +
                  $"&recursionLevel=Full" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(conn.Branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version={ApiVersion}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound) return Array.Empty<PagePath>();
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<AdoItemList>(cancellationToken: ct);
        if (payload?.Value is null) return Array.Empty<PagePath>();

        var prefix = scope.TrimEnd('/') + "/";
        var pages = new List<PagePath>();
        foreach (var item in payload.Value)
        {
            if (!string.Equals(item.GitObjectType, "blob", StringComparison.OrdinalIgnoreCase)) continue;
            if (!item.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
            if (!item.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var rel = item.Path[prefix.Length..];
            pages.Add(PagePath.Parse("/" + rel));
        }
        pages.Sort((a, b) => string.CompareOrdinal(a.ToLinkPath(), b.ToLinkPath()));
        return pages;
    }

    public async Task<CommitResult> WritePageAsync(
        WikiConnection conn, CommitRequest request, CancellationToken ct = default)
    {
        var path = "/" + Combine(conn.DocsPath, request.Page.ToFilePath().Replace('\\', '/'));

        var current = await GetItemAsync(conn, path, includeContent: false, ct);
        var changeType = current is null ? "add" : "edit";

        if (current is null && request.ExpectedSha is not null)
        {
            throw new WikiConflictException(
                "Page was deleted on the server since it was loaded.");
        }
        if (current is not null && !string.Equals(current.ObjectId, request.ExpectedSha, StringComparison.Ordinal))
        {
            throw new WikiConflictException(
                "Azure DevOps page changed on the server since it was loaded. Reload to merge.");
        }

        var headSha = await GetBranchHeadAsync(conn, ct);

        var pushBody = new AdoPush(
            RefUpdates: new[]
            {
                new AdoRefUpdate(
                    Name: $"refs/heads/{conn.Branch}",
                    OldObjectId: headSha)
            },
            Commits: new[]
            {
                new AdoCommit(
                    Comment: request.CommitMessage,
                    Changes: new[]
                    {
                        new AdoChange(
                            ChangeType: changeType,
                            Item: new AdoChangePath(path),
                            NewContent: new AdoNewContent(request.Markdown, "rawtext"))
                    })
            });

        var url = $"https://dev.azure.com/{Uri.EscapeDataString(conn.Owner)}" +
                  $"/{Uri.EscapeDataString(conn.Project)}" +
                  $"/_apis/git/repositories/{Uri.EscapeDataString(conn.Repo)}/pushes" +
                  $"?api-version={ApiVersion}";

        using var req = Authenticated(HttpMethod.Post, url, conn.Token);
        req.Content = JsonContent.Create(pushBody, options: JsonOpts);
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.Conflict)
        {
            throw new WikiConflictException(
                "Azure DevOps rejected the push (branch advanced). Reload to merge.");
        }
        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            // ADO returns 400 with TF401028 when the ref is stale.
            if (detail.Contains("TF401028", StringComparison.Ordinal))
            {
                throw new WikiConflictException(
                    "Azure DevOps rejected the push (branch advanced). Reload to merge.");
            }
            throw new InvalidOperationException(
                $"Azure DevOps commit failed ({(int)res.StatusCode}): {Trim(detail)}");
        }

        // Re-read to pick up the new file SHA.
        var updated = await GetItemAsync(conn, path, includeContent: false, ct)
                      ?? throw new InvalidOperationException("commit succeeded but item missing");
        return new CommitResult(updated.ObjectId ?? string.Empty);
    }

    private async Task<AdoItem?> GetItemAsync(
        WikiConnection conn, string path, bool includeContent, CancellationToken ct)
    {
        var url = $"{ItemsBase(conn)}?path={Uri.EscapeDataString(path)}" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(conn.Branch)}" +
                  $"&versionDescriptor.versionType=branch" +
                  $"&includeContent={(includeContent ? "true" : "false")}" +
                  $"&api-version={ApiVersion}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<AdoItem>(cancellationToken: ct);
    }

    private async Task<string> GetBranchHeadAsync(WikiConnection conn, CancellationToken ct)
    {
        var url = $"https://dev.azure.com/{Uri.EscapeDataString(conn.Owner)}" +
                  $"/{Uri.EscapeDataString(conn.Project)}" +
                  $"/_apis/git/repositories/{Uri.EscapeDataString(conn.Repo)}/refs" +
                  $"?filter=heads/{Uri.EscapeDataString(conn.Branch)}&api-version={ApiVersion}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadFromJsonAsync<AdoRefList>(cancellationToken: ct);
        var match = payload?.Value?.FirstOrDefault(r =>
            r.Name.Equals($"refs/heads/{conn.Branch}", StringComparison.Ordinal));
        if (match is null)
            throw new InvalidOperationException($"branch '{conn.Branch}' not found");
        return match.ObjectId;
    }

    private static string ItemsBase(WikiConnection conn) =>
        $"https://dev.azure.com/{Uri.EscapeDataString(conn.Owner)}" +
        $"/{Uri.EscapeDataString(conn.Project)}" +
        $"/_apis/git/repositories/{Uri.EscapeDataString(conn.Repo)}/items";

    private static HttpRequestMessage Authenticated(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + token));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
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

    private sealed record AdoItemList(List<AdoItem> Value);
    private sealed record AdoItem(string Path, string GitObjectType, string? ObjectId, string? Content);
    private sealed record AdoRefList(List<AdoRef> Value);
    private sealed record AdoRef(string Name, string ObjectId);

    private sealed record AdoPush(IReadOnlyList<AdoRefUpdate> RefUpdates, IReadOnlyList<AdoCommit> Commits);
    private sealed record AdoRefUpdate(string Name, string OldObjectId);
    private sealed record AdoCommit(string Comment, IReadOnlyList<AdoChange> Changes);
    private sealed record AdoChange(string ChangeType, AdoChangePath Item, AdoNewContent NewContent);
    private sealed record AdoChangePath(string Path);
    private sealed record AdoNewContent(string Content, string ContentType);
}
