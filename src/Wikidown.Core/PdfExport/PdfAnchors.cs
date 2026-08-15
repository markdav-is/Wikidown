namespace Wikidown.Core.PdfExport;

// Single source of truth for in-PDF anchor ids, shared by MarkdownIrBuilder
// (which resolves links against it) and the renderer (which registers
// bookmarks under these same ids). Must stay in lockstep with Markdig's
// UseAutoIdentifiers() heading-slug scheme.
public static class PdfAnchors
{
    public static string PageAnchor(PagePath page) => "page:" + page.ToLinkPath();

    public static string HeadingAnchor(PagePath page, string headingSlug) =>
        PageAnchor(page) + "#" + headingSlug;
}
