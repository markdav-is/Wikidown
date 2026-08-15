namespace Wikidown.Core.PdfExport;

public sealed record PageIr(PagePath Path, string Title, IReadOnlyList<IrBlock> Blocks);

public sealed record PdfExportWarning(PagePath Page, string Target);

public sealed record PdfExportContent(
    IReadOnlyList<PageIr> Pages, IReadOnlyList<NavNode> Nav, IReadOnlyList<PdfExportWarning> Warnings);

public static class WikiPdfContent
{
    public static PdfExportContent BuildAll(WikiRepository repo, PagePath? from = null, bool allowHtmlSkip = false)
    {
        var paths = repo.Walk(from).ToList();
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
