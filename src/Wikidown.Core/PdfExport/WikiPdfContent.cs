namespace Wikidown.Core.PdfExport;

public sealed record PageIr(PagePath Path, string Title, IReadOnlyList<IrBlock> Blocks);

public sealed record PdfExportContent(IReadOnlyList<PageIr> Pages, IReadOnlyList<NavNode> Nav);

public static class WikiPdfContent
{
    public static PdfExportContent BuildAll(WikiRepository repo, PagePath? from = null, bool allowHtmlSkip = false)
    {
        var paths = repo.Walk(from).ToList();

        var pages = paths.Select(path =>
        {
            var markdown = Breadcrumb.Strip(repo.Read(path).Markdown);
            var blocks = MarkdownIrBuilder.Build(markdown, path, repo, allowHtmlSkip);
            return new PageIr(path, path.Name.Title, blocks);
        }).ToList();

        var nav = NavTree.Build(paths, folder => repo.ReadOrder(folder));
        return new PdfExportContent(pages, nav);
    }
}
