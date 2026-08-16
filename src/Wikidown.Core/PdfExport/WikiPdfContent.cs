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
        var warnings = new List<PdfExportWarning>();

        var pages = paths.Select(path =>
        {
            var markdown = Breadcrumb.Strip(repo.Read(path).Markdown);
            var blocks = MarkdownIrBuilder.Build(markdown, path, repo, allowHtmlSkip, out var pageWarnings);
            warnings.AddRange(pageWarnings);
            return new PageIr(path, path.Name.Title, blocks);
        }).ToList();

        var nav = NavTree.Build(paths, folder => repo.ReadOrder(folder));
        return new PdfExportContent(pages, nav, warnings);
    }
}
