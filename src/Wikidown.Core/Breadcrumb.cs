namespace Wikidown.Core;

// A one-line ancestor trail auto-injected as a page's first line, e.g.
// "[Home](../Home.md) / [Encounters](../Encounters.md) / The Sky Hunters".
// Links are relative, matching the wiki's body-link convention.
public static class Breadcrumb
{
    public const string Marker = "<!-- wikidown:breadcrumb -->";
    private const string HomeFileBase = "Home";

    // Null when there's nothing meaningful to show: the page IS /Home, or
    // it's a top-level page and the wiki has no /Home to link back to.
    public static string? Render(WikiRepository repo, PagePath page)
    {
        if (page.IsRoot) return null;

        var segments = page.Segments;
        var topIsHome = string.Equals(segments[0].FileBase, HomeFileBase, StringComparison.OrdinalIgnoreCase);
        if (segments.Count == 1 && topIsHome) return null; // this page IS Home

        var hasHome = repo.Exists(PagePath.Parse("/" + HomeFileBase));
        if (segments.Count == 1 && !hasHome) return null; // nothing to link back to

        var crumbs = new List<string>();

        // Always lead with Home, unless the page's own ancestor chain
        // already starts with it (added below, so this would duplicate it).
        if (hasHome && !topIsHome)
        {
            var homeUps = segments.Count - 1;
            var homeLink = string.Concat(Enumerable.Repeat("../", homeUps)) + HomeFileBase + ".md";
            crumbs.Add($"[Home]({homeLink})");
        }

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
    public static string Inject(WikiRepository repo, PagePath page, string markdown)
    {
        var stripped = Strip(markdown);
        var crumb = Render(repo, page);
        return crumb is null ? stripped : crumb + "\n\n" + stripped.TrimStart('\n');
    }
}
