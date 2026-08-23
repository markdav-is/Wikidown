namespace Wikidown.Html;

// The starter theme, embedded once here and shared by `wikidown pages`
// (which copies it into a wiki) and HtmlExporter (which falls back to it
// when a wiki hasn't been scaffolded).
public static class ThemeResources
{
    public static readonly IReadOnlyList<string> Files = new[]
    {
        "_config.yml",
        "index.html",
        "_layouts/wikidown.html",
        "_includes/nav-tree.html",
        "assets/wikidown.css",
    };

    // Where the site root should send readers: /Home if the wiki has one,
    // otherwise its first top-level page.
    public static string HomeUrl(Core.WikiRepository repo)
    {
        var home = Core.PagePath.Parse("/Home");
        if (repo.Exists(home)) return home.ToLinkPath() + ".html";
        var first = repo.ListChildren(Core.PagePath.Root).FirstOrDefault();
        return first is null ? "/Home.html" : first.ToLinkPath() + ".html";
    }

    public static string Read(string relativePath)
    {
        using var stream = typeof(ThemeResources).Assembly.GetManifestResourceStream("theme/" + relativePath)
            ?? throw new InvalidOperationException($"missing embedded theme file: {relativePath}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
