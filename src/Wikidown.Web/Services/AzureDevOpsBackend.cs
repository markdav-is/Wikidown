using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Wikidown.Core;

namespace Wikidown.Web.Services;

// Read-only Azure DevOps Git Items API wrapper. Commit support lands in chunk 4e.
public sealed class AzureDevOpsBackend(HttpClient http) : IWikiBackend
{
    private const string ApiVersion = "7.1";

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
            // Skip the scope itself.
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
        var url = $"{ItemsBase(conn)}?path={Uri.EscapeDataString(path)}" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(conn.Branch)}" +
                  $"&versionDescriptor.versionType=branch" +
                  $"&includeContent=true&api-version={ApiVersion}";
        using var req = Authenticated(HttpMethod.Get, url, conn.Token);
        using var res = await http.SendAsync(req, ct);

        res.EnsureSuccessStatusCode();
        var item = await res.Content.ReadFromJsonAsync<AdoItem>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("empty response");

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

    private sealed record AdoItemList(List<AdoItem> Value);
    private sealed record AdoItem(string Path, string GitObjectType, string? ObjectId, string? Content);
}
