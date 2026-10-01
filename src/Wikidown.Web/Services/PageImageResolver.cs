using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text.RegularExpressions;
using Wikidown.Core;

namespace Wikidown.Web.Services;

/// <summary>
/// Turns relative image links (../.attachments/x.png, /.attachments/x.png) into data URLs
/// so the browser never has to fetch from the repo host, which needs auth for private repos.
/// Fetched bytes are cached per connection + docs-relative path for the session.
/// </summary>
public sealed partial class PageImageResolver(BackendResolver backends)
{
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    private static readonly MarkdownPipeline ScanPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Fetches every not-yet-cached relative image on the page. Returns true if anything new was resolved.</summary>
    public async Task<bool> PrefetchAsync(
        WikiConnection conn, PagePath page, string markdown, CancellationToken ct = default)
    {
        var doc = Markdown.Parse(markdown, ScanPipeline);
        var targets = doc.Descendants<LinkInline>()
            .Where(link => link.IsImage && link.Url is not null)
            .Select(link => link.Url!)
            .Concat(RawImgSrc().Matches(markdown).Select(m => m.Groups["src"].Value));
        var wanted = new List<(string Key, string Rel)>();
        foreach (var target in targets)
        {
            var rel = LinkChecker.ResolveDocsRelativePath(page, target);
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

    [GeneratedRegex(@"<img\b[^>]*?\bsrc\s*=\s*(?:""(?<src>[^""]*)""|'(?<src>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex RawImgSrc();
}
