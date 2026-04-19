using System.Text.Json;
using Microsoft.JSInterop;

namespace Wikidown.Web.Services;

// Persists the active wiki connection (provider, repo, token) in localStorage.
// Tokens never leave the browser — there is no backend.
public sealed class ConnectionStore(IJSRuntime js)
{
    private const string StorageKey = "wikidown.connection.v1";

    private WikiConnection? _cached;
    private bool _loaded;

    public WikiConnection? Current => _cached;

    public event Action? Changed;

    public async Task<WikiConnection?> LoadAsync()
    {
        if (_loaded) return _cached;
        _loaded = true;

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
        _loaded = true;
        var json = JsonSerializer.Serialize(connection);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        _cached = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }
}
