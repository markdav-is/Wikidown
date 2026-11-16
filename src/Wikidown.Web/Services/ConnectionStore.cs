using System.Text.Json;
using Microsoft.JSInterop;

namespace Wikidown.Web.Services;

// Persists the active wiki connection (provider, repo, token) in localStorage.
// Tokens never leave the browser — there is no backend.
public sealed class ConnectionStore(IJSRuntime js)
{
    private const string StorageKey = "wikidown.connection.v1";
    private const string RecentKey = "wikidown.recent.v1";
    private const int RecentLimit = 8;

    private WikiConnection? _cached;
    private Task<WikiConnection?>? _load;

    public WikiConnection? Current => _cached;

    public event Action? Changed;

    // Concurrent first callers (e.g. the Browse page and the drafts menu
    // both initializing on a fresh load) must share one read — a bool
    // "loaded" flag set before the await hands every racing caller a
    // still-null cache.
    public Task<WikiConnection?> LoadAsync() => _load ??= LoadCoreAsync();

    private async Task<WikiConnection?> LoadCoreAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            _cached = JsonSerializer.Deserialize<WikiConnection>(json);
        }
        catch (JsonException)
        {
            _cached = null;
        }
        return _cached;
    }

    public async Task SaveAsync(WikiConnection connection)
    {
        _cached = connection;
        _load = Task.FromResult<WikiConnection?>(connection);
        var json = JsonSerializer.Serialize(connection);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        var recent = (await RecentAsync()).Where(c => !SameWiki(c, connection)).Prepend(connection).Take(RecentLimit);
        await js.InvokeVoidAsync("localStorage.setItem", RecentKey, JsonSerializer.Serialize(recent));
        Changed?.Invoke();
    }

    /// <summary>Wikis opened on this device, latest first.</summary>
    public async Task<IReadOnlyList<WikiConnection>> RecentAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", RecentKey);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<WikiConnection>();
        try { return JsonSerializer.Deserialize<List<WikiConnection>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<WikiConnection>(); }
    }

    /// <summary>Drops a provider's wikis (and their tokens) — signing out must not leave them behind.</summary>
    public async Task ForgetAsync(WikiProvider provider)
    {
        var kept = (await RecentAsync()).Where(c => c.Provider != provider).ToList();
        await js.InvokeVoidAsync("localStorage.setItem", RecentKey, JsonSerializer.Serialize(kept));
        if (_cached?.Provider == provider) await ClearAsync();
    }

    private static bool SameWiki(WikiConnection a, WikiConnection b) =>
        a with { Token = "" } == b with { Token = "" };

    public async Task ClearAsync()
    {
        _cached = null;
        _load = Task.FromResult<WikiConnection?>(null);
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }
}
