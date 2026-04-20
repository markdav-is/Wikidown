using Microsoft.JSInterop;

namespace Wikidown.Web.Services;

// Tracks browser online/offline status and exposes change notifications.
public sealed class NetworkStatus : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<NetworkStatus>? _selfRef;
    private bool _wired;

    public NetworkStatus(IJSRuntime js)
    {
        _js = js;
        IsOnline = true;
    }

    public bool IsOnline { get; private set; }

    public event Action? Changed;

    public async ValueTask InitializeAsync()
    {
        if (_wired) return;
        _wired = true;
        _selfRef = DotNetObjectReference.Create(this);
        IsOnline = await _js.InvokeAsync<bool>("eval", "navigator.onLine");
        await _js.InvokeVoidAsync("wikidownNet.register", _selfRef);
    }

    [JSInvokable]
    public void OnNetChanged(bool online)
    {
        if (IsOnline == online) return;
        IsOnline = online;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_wired)
        {
            try { await _js.InvokeVoidAsync("wikidownNet.unregister"); } catch { }
        }
        _selfRef?.Dispose();
    }
}
