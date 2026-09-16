namespace Wikidown.Core.PdfExport;

public sealed record PageIr(PagePath Path, string Title, IReadOnlyList<IrBlock> Blocks);

public sealed record PdfExportWarning(PagePath Page, string Target);

public sealed record PdfExportContent(
    IReadOnlyList<PageIr> Pages, IReadOnlyList<NavNode> Nav, IReadOnlyList<PdfExportWarning> Warnings);

public static class WikiPdfContent
{
    public static PdfExportContent BuildAll(WikiRepository repo, PagePath? from = null, bool allowHtmlSkip = false)
    {
        // Walk(from) yields from's descendants only, not from itself — a
        // scoped export needs the page it's scoped to as well, so the
        // subtree's own root content isn't silently dropped.
        var paths = new List<PagePath>();
        if (from is { IsRoot: false } start && repo.Exists(start))
            paths.Add(start);
        paths.AddRange(repo.Walk(from));

        return Build(
            paths.Select(path => (path, repo.Read(path).Markdown)).ToList(),
            repo.ReadOrder,
            new RepositoryPdfPageSource(repo),
            allowHtmlSkip);
    }

    /// <summary>Builds export content from pages already in memory (the web editor's path).</summary>
    public static PdfExportContent Build(
        IReadOnlyList<(PagePath Path, string Markdown)> pages,
        Func<PagePath, IReadOnlyList<string>> orderFor,
        IPdfPageSource source,
        bool allowHtmlSkip = false)
    {
        var warnings = new List<PdfExportWarning>();

        var pageIrs = pages.Select(p =>
        {
            var markdown = Breadcrumb.Strip(p.Markdown);
            var blocks = MarkdownIrBuilder.Build(markdown, p.Path, source, allowHtmlSkip, out var pageWarnings);
            warnings.AddRange(pageWarnings);
            return new PageIr(p.Path, p.Path.Name.Title, blocks);
        }).ToList();

        var nav = NavTree.Build(pages.Select(p => p.Path).ToList(), orderFor);
        return new PdfExportContent(pageIrs, nav, warnings);
    }
}
