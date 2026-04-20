using System.Text.Json;
using Microsoft.JSInterop;
using Wikidown.Core;

namespace Wikidown.Web.Services;

// Persists in-progress page edits per (connection, page) so reloads and
// PWA relaunches don't lose work. Cleared on successful commit.
// A small index of draft keys is kept alongside so the header menu can
// list every draft without scanning localStorage.
public sealed class DraftStore(IJSRuntime js)
{
    private const string Prefix = "wikidown.draft.v1.";
    private const string IndexKey = "wikidown.draftindex.v1";

    public event Action? Changed;

    public ValueTask<string?> LoadAsync(WikiConnection conn, PagePath page) =>
        js.InvokeAsync<string?>("localStorage.getItem", Key(conn, page));

    public async ValueTask SaveAsync(WikiConnection conn, PagePath page, string markdown)
    {
        var key = Key(conn, page);
        await js.InvokeVoidAsync("localStorage.setItem", key, markdown);
        if (await AddToIndexAsync(key)) Changed?.Invoke();
    }

    public async ValueTask ClearAsync(WikiConnection conn, PagePath page)
    {
        var key = Key(conn, page);
        await js.InvokeVoidAsync("localStorage.removeItem", key);
        if (await RemoveFromIndexAsync(key)) Changed?.Invoke();
    }

    public async ValueTask<IReadOnlyList<PagePath>> ListAsync(WikiConnection conn)
    {
        var all = await LoadIndexAsync();
        var connPrefix = ConnPrefix(conn);
        var results = new List<PagePath>();
        foreach (var key in all)
        {
            if (!key.StartsWith(connPrefix, StringComparison.Ordinal)) continue;
            var linkPath = key[connPrefix.Length..];
            if (linkPath.Length == 0 || linkPath[0] != '/') continue;
            results.Add(PagePath.Parse(linkPath));
        }
        return results;
    }

    private async ValueTask<List<string>> LoadIndexAsync()
    {
        var raw = await js.InvokeAsync<string?>("localStorage.getItem", IndexKey);
        if (string.IsNullOrEmpty(raw)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(raw) ?? new(); }
        catch { return new(); }
    }

    private ValueTask SaveIndexAsync(List<string> index) =>
        js.InvokeVoidAsync("localStorage.setItem", IndexKey, JsonSerializer.Serialize(index));

    private async ValueTask<bool> AddToIndexAsync(string key)
    {
        var idx = await LoadIndexAsync();
        if (idx.Contains(key)) return false;
        idx.Add(key);
        await SaveIndexAsync(idx);
        return true;
    }

    private async ValueTask<bool> RemoveFromIndexAsync(string key)
    {
        var idx = await LoadIndexAsync();
        if (!idx.Remove(key)) return false;
        await SaveIndexAsync(idx);
        return true;
    }

    private static string Key(WikiConnection c, PagePath p) =>
        $"{ConnPrefix(c)}{p.ToLinkPath()}";

    private static string ConnPrefix(WikiConnection c) =>
        $"{Prefix}{c.Provider}/{c.Owner}/{c.Project}/{c.Repo}/{c.Branch}";
}
