using System.Text.Json;
using Microsoft.JSInterop;

namespace Wikidown.Web.Services;

// Persists the active wiki connection (provider, repo, token) in localStorage.
// Tokens never leave the browser — there is no backend.
public sealed class ConnectionStore(IJSRuntime js)
{
    private const string StorageKey = "wikidown.connection.v1";

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
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        _cached = null;
        _load = Task.FromResult<WikiConnection?>(null);
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }
}
