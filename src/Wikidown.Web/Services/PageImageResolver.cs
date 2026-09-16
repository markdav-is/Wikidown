using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.MudBlazor;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Wikidown.Core;

namespace Wikidown.Web.Services;

/// <summary>
/// Turns relative image links (../.attachments/x.png, /.attachments/x.png) into data URLs
/// so the browser never has to fetch from the repo host, which needs auth for private repos.
/// Fetched bytes are cached per connection + docs-relative path for the session.
/// </summary>
public sealed class PageImageResolver(BackendResolver backends)
{
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    private static readonly MarkdownPipeline ScanPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Fetches every not-yet-cached relative image on the page. Returns true if anything new was resolved.</summary>
    public async Task<bool> PrefetchAsync(
        WikiConnection conn, PagePath page, string markdown, CancellationToken ct = default)
    {
        var doc = Markdown.Parse(markdown, ScanPipeline);
        var wanted = new List<(string Key, string Rel)>();
        foreach (var link in doc.Descendants<LinkInline>())
        {
            if (!link.IsImage || link.Url is null) continue;
            var rel = LinkChecker.ResolveDocsRelativePath(page, link.Url);
            if (rel is null) continue;
            var key = CacheKey(conn, rel);
            if (_cache.ContainsKey(key) || wanted.Any(w => w.Key == key)) continue;
            wanted.Add((key, rel));
        }
        if (wanted.Count == 0) return false;

        var backend = backends.For(conn.Provider);
        await Task.WhenAll(wanted.Select(async w =>
        {
            string? dataUrl = null;
            try
            {
                var bytes = await backend.ReadBytesAsync(conn, w.Rel, ct);
                if (bytes is not null)
                    dataUrl = $"data:{MimeFor(w.Rel)};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (Exception)
            {
                // Missing or unreadable images render as a broken image, same as before.
            }
            _cache[w.Key] = dataUrl;
        }));
        return true;
    }

    /// <summary>The cached data URL for an image target on a page, or null if unknown or missing.</summary>
    public string? DataUrlFor(WikiConnection conn, PagePath page, string target)
    {
        var rel = LinkChecker.ResolveDocsRelativePath(page, target);
        return rel is not null && _cache.TryGetValue(CacheKey(conn, rel), out var data) ? data : null;
    }

    /// <summary>Replaces a cached image, e.g. after converting it to a format the PDF renderer can embed.</summary>
    public void Replace(WikiConnection conn, PagePath page, string target, string? dataUrl)
    {
        var rel = LinkChecker.ResolveDocsRelativePath(page, target);
        if (rel is not null) _cache[CacheKey(conn, rel)] = dataUrl;
    }

    /// <summary>A render pipeline whose image URLs are rewritten from the cache. Build a fresh one after each prefetch.</summary>
    public MarkdownPipeline BuildPipeline(WikiConnection conn, PagePath page) =>
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMudBlazor()
            .Use(new ImageRewriteExtension(url =>
            {
                var rel = LinkChecker.ResolveDocsRelativePath(page, url);
                return rel is not null && _cache.TryGetValue(CacheKey(conn, rel), out var data) ? data : null;
            }))
            .Build();

    private static string CacheKey(WikiConnection c, string rel) =>
        $"{c.Provider}|{c.Host}|{c.Owner}|{c.Project}|{c.Repo}|{c.Branch}|{c.DocsPath}|{rel}";

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        ".avif" => "image/avif",
        _ => "application/octet-stream",
    };

    private sealed class ImageRewriteExtension(Func<string, string?> resolve) : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) =>
            pipeline.DocumentProcessed += doc =>
            {
                foreach (var link in doc.Descendants<LinkInline>())
                {
                    if (!link.IsImage || link.Url is null) continue;
                    var resolved = resolve(link.Url);
                    if (resolved is not null) link.Url = resolved;
                }
            };

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer) { }
    }
}
