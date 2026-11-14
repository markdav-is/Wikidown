namespace Wikidown.Html;

// What HtmlExporter needs from _config.yml. The top-level scalars come
// from `key: value` lines at column zero, which still works when the file
// isn't valid YAML (an unquoted `{{TITLE}}` placeholder). The rest — the
// `wikidown:` publishing keys and everything exposed as site.* — needs a
// real YAML parse.
//
//   wikidown:
//     root_page: /Home          # rendered as the site's index.html
//     layouts:                  # page link path -> _layouts/<name>.html
//       /Home: landing
//       /Venues/*: venue        # every page under /Venues
public sealed record SiteConfig(
    string? Title,
    string? Description,
    string? RepositoryUrl,
    string? BaseUrl,
    string? Favicon,
    string? Url,
    string? Image,
    IReadOnlyList<string> ExcludeFromSite,
    IReadOnlyDictionary<string, string> Layouts,
    string? RootPage,
    IReadOnlyDictionary<string, object?> Values)
{
    public const string DefaultLayout = "wikidown";

    // The layout for a page: an exact entry wins, then the nearest
    // `/Folder/*` entry above it, then the default.
    public string LayoutFor(string linkPath)
    {
        if (Layouts.TryGetValue(linkPath, out var exact)) return exact;
        for (var slash = linkPath.LastIndexOf('/'); slash > 0; slash = linkPath.LastIndexOf('/', slash - 1))
            if (Layouts.TryGetValue(linkPath[..slash] + "/*", out var subtree)) return subtree;
        return DefaultLayout;
    }

    public static SiteConfig Parse(string yaml)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] is ' ' or '#' or '-') continue;
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0) continue;
            var key = line[..colon].Trim();
            var value = StripComment(line[(colon + 1)..]).Trim();
            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                value = Unquote(value);
            values[key] = value;
        }

        var all = ParseYaml(yaml);
        var wikidown = all.GetValueOrDefault("wikidown") as Dictionary<string, object?>;
        var layouts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (wikidown?.GetValueOrDefault("layouts") is Dictionary<string, object?> map)
            foreach (var (page, layout) in map)
                if (layout is not null) layouts[NormalizePagePath(page)] = layout.ToString()!;
        var rootPage = wikidown?.GetValueOrDefault("root_page") as string;

        return new SiteConfig(
            Get(values, "title"),
            Get(values, "description"),
            Get(values, "repository_url"),
            Get(values, "baseurl"),
            Get(values, "favicon"),
            Get(values, "url")?.TrimEnd('/'),
            Get(values, "image"),
            Core.PublishExclusions.Parse(yaml),
            layouts,
            rootPage is null ? null : NormalizePagePath(rootPage),
            all);
    }

    // Accepts the forms people write: /Home, Home, Home.md, /Venues/*.
    public static string NormalizePagePath(string path)
    {
        var p = path.Trim().Replace('\\', '/').Trim('/');
        if (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) p = p[..^3];
        else if (p.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) p = p[..^5];
        return "/" + p;
    }

    private static Dictionary<string, object?> ParseYaml(string yaml)
    {
        try
        {
            return SiteData.FromYaml(yaml) as Dictionary<string, object?> ?? new(StringComparer.Ordinal);
        }
        catch (YamlDotNet.Core.YamlException e)
        {
            // Only fatal when the keys that need it are in there.
            if (yaml.Contains("layouts:", StringComparison.Ordinal) || yaml.Contains("root_page:", StringComparison.Ordinal))
                throw new InvalidOperationException($"_config.yml: {e.Message}", e);
            return new(StringComparer.Ordinal);
        }
    }

    private static string? Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

    private static string StripComment(string value)
    {
        var inQuote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (inQuote != '\0') { if (c == inQuote) inQuote = '\0'; continue; }
            if (c is '"' or '\'') inQuote = c;
            else if (c == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1]))) return value[..i];
        }
        return value;
    }

    private static string Unquote(string quoted)
    {
        var inner = quoted[1..^1];
        return quoted[0] == '"' ? inner.Replace("\\\"", "\"").Replace("\\\\", "\\") : inner;
    }
}
