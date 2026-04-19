namespace Wikidown.Web.Services;

public sealed class BackendResolver(IEnumerable<IWikiBackend> backends)
{
    private readonly Dictionary<WikiProvider, IWikiBackend> _map =
        backends.ToDictionary(b => b.Provider);

    public IWikiBackend For(WikiProvider provider) =>
        _map.TryGetValue(provider, out var backend)
            ? backend
            : throw new InvalidOperationException($"No backend for {provider}");
}
