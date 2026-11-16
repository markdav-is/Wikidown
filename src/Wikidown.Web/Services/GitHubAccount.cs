using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Wikidown.Web.Services;

public sealed record GitHubUser(string Login, string? Name, string? AvatarUrl);

public sealed record GitHubRepo(
    string Owner, string Name, string? Description, bool Private, string DefaultBranch,
    DateTimeOffset? PushedAt, bool CanPush)
{
    public string FullName => $"{Owner}/{Name}";
}

/// <summary>
/// The signed-in GitHub user: the token (kept in localStorage, like the
/// connection's) and what it can see — repositories and their branches.
/// </summary>
public sealed class GitHubAccount(HttpClient http, IJSRuntime js)
{
    private const string StorageKey = "wikidown.github.v1";
    private const string ApiBase = "https://api.github.com";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private Task? _load;

    public string? Token { get; private set; }
    public GitHubUser? User { get; private set; }
    public bool SignedIn => Token is not null && User is not null;

    public event Action? Changed;

    public Task LoadAsync() => _load ??= LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(json);
            if (stored is { Token.Length: > 0, User: not null }) (Token, User) = (stored.Token, stored.User);
        }
        catch (JsonException) { }
    }

    /// <summary>Checks the token against GitHub and remembers it. Throws with a readable message if it is refused.</summary>
    public async Task SignInAsync(string token, CancellationToken ct = default)
    {
        token = token.Trim();
        using var req = Request(HttpMethod.Get, $"{ApiBase}/user", token);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("GitHub did not accept that token.");
        res.EnsureSuccessStatusCode();
        var user = await res.Content.ReadFromJsonAsync<GhUser>(JsonOpts, ct)
                   ?? throw new InvalidOperationException("GitHub returned no user.");

        Token = token;
        User = new GitHubUser(user.Login, user.Name, user.AvatarUrl);
        _load = Task.CompletedTask;
        await js.InvokeVoidAsync("localStorage.setItem", ct, StorageKey, JsonSerializer.Serialize(new Stored(Token, User)));
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Token = null;
        User = null;
        _load = Task.CompletedTask;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }

    /// <summary>Repositories the user can reach, most recently pushed first.</summary>
    public async Task<IReadOnlyList<GitHubRepo>> ListReposAsync(CancellationToken ct = default)
    {
        var repos = new List<GitHubRepo>();
        for (var page = 1; page <= 10; page++)
        {
            var batch = await GetAsync<List<GhRepo>>($"{ApiBase}/user/repos?per_page=100&sort=pushed&page={page}", ct);
            repos.AddRange(batch.Select(ToRepo));
            if (batch.Count < 100) break;
        }
        return repos;
    }

    /// <summary>A repository by name — for ones the list does not show, such as someone else's public repo.</summary>
    public async Task<GitHubRepo> GetRepoAsync(string owner, string name, CancellationToken ct = default) =>
        ToRepo(await GetAsync<GhRepo>($"{ApiBase}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}", ct));

    public async Task<IReadOnlyList<string>> ListBranchesAsync(string owner, string repo, CancellationToken ct = default) =>
        (await GetAsync<List<GhBranch>>(
            $"{ApiBase}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/branches?per_page=100", ct))
        .Select(b => b.Name).ToList();

    public WikiConnection Connect(GitHubRepo repo, string? branch = null, string? docsPath = null) =>
        new(WikiProvider.GitHub, Token ?? "", repo.Owner, repo.Name,
            string.IsNullOrWhiteSpace(branch) ? repo.DefaultBranch : branch,
            docsPath ?? WikiConnection.DefaultDocsPath);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var req = Request(HttpMethod.Get, url, Token);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Your GitHub sign-in has expired. Sign in again.");
        if (res.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("GitHub can't find that, or this sign-in can't see it.");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<T>(JsonOpts, ct)
               ?? throw new InvalidOperationException("empty response");
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private static GitHubRepo ToRepo(GhRepo r) =>
        new(r.Owner.Login, r.Name, r.Description, r.Private, r.DefaultBranch ?? WikiConnection.DefaultBranch,
            r.PushedAt, r.Permissions?.Push ?? false);

    private sealed record Stored(string Token, GitHubUser User);
    private sealed record GhUser(string Login, string? Name, string? AvatarUrl);
    private sealed record GhOwner(string Login);
    private sealed record GhPermissions(bool Push);
    private sealed record GhBranch(string Name);
    private sealed record GhRepo(
        string Name, GhOwner Owner, string? Description, bool Private, string? DefaultBranch,
        DateTimeOffset? PushedAt, GhPermissions? Permissions);
}
