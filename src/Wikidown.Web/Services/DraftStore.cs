using Microsoft.JSInterop;
using Wikidown.Core;

namespace Wikidown.Web.Services;

// Persists in-progress page edits per (connection, page) so reloads and
// PWA relaunches don't lose work. Cleared on successful commit.
public sealed class DraftStore(IJSRuntime js)
{
    private const string Prefix = "wikidown.draft.v1.";

    public ValueTask<string?> LoadAsync(WikiConnection conn, PagePath page) =>
        js.InvokeAsync<string?>("localStorage.getItem", Key(conn, page));

    public ValueTask SaveAsync(WikiConnection conn, PagePath page, string markdown) =>
        js.InvokeVoidAsync("localStorage.setItem", Key(conn, page), markdown);

    public ValueTask ClearAsync(WikiConnection conn, PagePath page) =>
        js.InvokeVoidAsync("localStorage.removeItem", Key(conn, page));

    private static string Key(WikiConnection c, PagePath p) =>
        $"{Prefix}{c.Provider}/{c.Owner}/{c.Project}/{c.Repo}/{c.Branch}{p.ToLinkPath()}";
}
