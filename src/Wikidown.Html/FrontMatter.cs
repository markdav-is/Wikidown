namespace Wikidown.Html;

// YAML front matter on theme files (layouts, the root index.html). Wiki
// pages themselves never carry any.
public static class FrontMatter
{
    public static (Dictionary<string, object?> Data, string Body) Split(string text)
    {
        var empty = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!text.StartsWith("---", StringComparison.Ordinal)) return (empty, text);
        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0 || text[3..firstBreak].Trim().Length > 0) return (empty, text);

        var lineStart = firstBreak + 1;
        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            var line = (lineEnd < 0 ? text[lineStart..] : text[lineStart..lineEnd]).TrimEnd('\r', ' ', '\t');
            if (line is "---" or "...")
            {
                var yaml = text[(firstBreak + 1)..lineStart];
                var data = SiteData.FromYaml(yaml) as Dictionary<string, object?> ?? empty;
                return (data, lineEnd < 0 ? "" : text[(lineEnd + 1)..]);
            }
            if (lineEnd < 0) break;
            lineStart = lineEnd + 1;
        }
        return (empty, text);
    }
}
