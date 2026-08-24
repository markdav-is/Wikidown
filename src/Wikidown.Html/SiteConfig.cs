namespace Wikidown.Html;

// The handful of top-level scalars HtmlExporter needs from _config.yml.
// Deliberately not a YAML parser: only `key: value` lines at column zero
// are read, which is exactly what the scaffolded config uses for these.
public sealed record SiteConfig(
    string? Title,
    string? Description,
    string? RepositoryUrl,
    string? BaseUrl,
    string? Favicon,
    IReadOnlyList<string> ExcludeFromSite)
{
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

        return new SiteConfig(
            Get(values, "title"),
            Get(values, "description"),
            Get(values, "repository_url"),
            Get(values, "baseurl"),
            Get(values, "favicon"),
            Core.PublishExclusions.Parse(yaml));
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
