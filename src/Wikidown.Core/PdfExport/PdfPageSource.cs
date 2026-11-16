namespace Wikidown.Core.PdfExport;

/// <summary>
/// Where the PDF IR builder looks up link targets and images. The CLI backs it
/// with the on-disk repository; the web editor backs it with pages and images
/// already fetched from a remote repo, so the same renderer runs in the browser.
/// </summary>
public interface IPdfPageSource
{
    bool Exists(PagePath page);

    /// <summary>A relative link target (fragment already stripped, no leading "/") to the page it names, or null.</summary>
    PagePath? ResolveRelativePage(PagePath from, string relativeTarget);

    /// <summary>An image target to something MigraDoc can load (a file path or "base64:…"), or null when missing.</summary>
    string? ResolveImage(PagePath from, string target);

    /// <summary>Where a resolved image can be viewed in the hosted repo (a browser URL), or null when unknown.</summary>
    string? ImageLinkUrl(PagePath from, string target);
}

public sealed class RepositoryPdfPageSource(WikiRepository repo) : IPdfPageSource
{
    private readonly TargetResolver _resolver = new(repo);
    private readonly Lazy<GitBlobLinks?> _links = new(() => GitBlobLinks.Find(repo.RootPath));

    public bool Exists(PagePath page) => repo.Exists(page);

    public PagePath? ResolveRelativePage(PagePath from, string relativeTarget)
    {
        var full = LinkChecker.ResolveFullPath(repo, from, relativeTarget);
        return File.Exists(full) ? FilePathToPagePath(full) : null;
    }

    public string? ResolveImage(PagePath from, string target) =>
        _resolver.Resolve(from, target) is { Exists: true } resolved ? resolved.FullPath : null;

    public string? ImageLinkUrl(PagePath from, string target) =>
        ResolveImage(from, target) is { } fullPath ? _links.Value?.For(fullPath) : null;

    private PagePath? FilePathToPagePath(string fullPath)
    {
        if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return null;
        var relative = Path.GetRelativePath(repo.RootPath, fullPath);
        if (relative.StartsWith("..")) return null;

        var segments = relative[..^3]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Select(PageName.FromFileBase)
            .ToList();
        return segments.Count == 0 ? null : new PagePath(segments);
    }
}

/// <summary>
/// Pages known by path only, with images supplied by a callback that returns a
/// MigraDoc-loadable name ("base64:…" for bytes already in memory) or null.
/// </summary>
public sealed class InMemoryPdfPageSource(
    IEnumerable<PagePath> pages,
    Func<PagePath, string, string?> imageResolver,
    Func<PagePath, string, string?>? imageLinkResolver = null) : IPdfPageSource
{
    private readonly HashSet<string> _pages = new(pages.Select(p => p.ToLinkPath()), StringComparer.OrdinalIgnoreCase);

    public bool Exists(PagePath page) => _pages.Contains(page.ToLinkPath());

    public PagePath? ResolveRelativePage(PagePath from, string relativeTarget)
    {
        var rel = LinkChecker.ResolveDocsRelativePath(from, relativeTarget);
        if (rel is null || !rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return null;

        var segments = rel[..^3]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(PageName.FromFileBase)
            .ToList();
        if (segments.Count == 0) return null;
        var page = new PagePath(segments);
        return Exists(page) ? page : null;
    }

    public string? ResolveImage(PagePath from, string target) => imageResolver(from, target);

    public string? ImageLinkUrl(PagePath from, string target) => imageLinkResolver?.Invoke(from, target);
}
