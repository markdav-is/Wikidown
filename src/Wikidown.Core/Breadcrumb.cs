namespace Wikidown.Core;

// A one-line ancestor trail auto-injected as a page's first line, e.g.
// "[Encounters](../Encounters.md) / The Sky Hunters". Links are relative,
// matching the wiki's body-link convention, and computed purely from the
// page's path segments — no ancestor page needs to be read.
public static class Breadcrumb
{
    public const string Marker = "<!-- wikidown:breadcrumb -->";

    // Null for a top-level page (no ancestors to show).
    public static string? Render(PagePath page)
    {
        if (page.IsRoot || page.Segments.Count <= 1) return null;

        var segments = page.Segments;
        var crumbs = new List<string>();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var ups = segments.Count - 1 - i;
            var link = string.Concat(Enumerable.Repeat("../", ups)) + segments[i].FileName;
            crumbs.Add($"[{segments[i].Title}]({link})");
        }
        crumbs.Add(segments[^1].Title);
        return string.Join(" / ", crumbs) + " " + Marker;
    }

    // Removes a previously-injected breadcrumb line (and the blank line
    // after it, if present) so re-injection never accumulates duplicates.
    public static string Strip(string markdown)
    {
        var newlineIndex = markdown.IndexOf('\n');
        var firstLine = newlineIndex < 0 ? markdown : markdown[..newlineIndex];
        if (!firstLine.TrimEnd('\r').EndsWith(Marker, StringComparison.Ordinal))
            return markdown;

        var rest = newlineIndex < 0 ? "" : markdown[(newlineIndex + 1)..];
        if (rest.StartsWith('\n')) rest = rest[1..];
        else if (rest.StartsWith("\r\n", StringComparison.Ordinal)) rest = rest[2..];
        return rest;
    }

    // Strips any existing breadcrumb, then prepends a fresh one for `page`.
    public static string Inject(PagePath page, string markdown)
    {
        var stripped = Strip(markdown);
        var crumb = Render(page);
        return crumb is null ? stripped : crumb + "\n\n" + stripped.TrimStart('\n');
    }
}
