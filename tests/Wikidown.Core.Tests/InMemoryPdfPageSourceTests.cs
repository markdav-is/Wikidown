using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class InMemoryPdfPageSourceTests
{
    private static readonly PagePath[] Pages =
    [
        PagePath.Parse("/Home"),
        PagePath.Parse("/Guides/Getting-Started"),
        PagePath.Parse("/Guides/Getting-Started/Install"),
    ];

    private static InMemoryPdfPageSource Source(Func<PagePath, string, string?>? images = null) =>
        new(Pages, images ?? ((_, _) => null));

    [Fact]
    public void Relative_md_links_resolve_against_the_page_folder()
    {
        var from = PagePath.Parse("/Guides/Getting-Started/Install");
        Assert.Equal("/Guides/Getting-Started",
            Source().ResolveRelativePage(from, "../Getting-Started.md")?.ToLinkPath());
        Assert.Equal("/Home",
            Source().ResolveRelativePage(from, "../../Home.md")?.ToLinkPath());
        Assert.Null(Source().ResolveRelativePage(from, "../Missing.md"));
        Assert.Null(Source().ResolveRelativePage(from, "../Getting-Started"));
    }

    [Fact]
    public void Link_resolution_flows_through_the_ir_builder()
    {
        var from = PagePath.Parse("/Guides/Getting-Started/Install");
        var blocks = MarkdownIrBuilder.Build(
            "[up](../Getting-Started.md) ![pic](../.attachments/x.png) ![gone](nope.png)\n",
            from,
            Source((_, target) => target.Contains(".attachments") ? "base64:AAAA" : null),
            allowHtmlSkip: false,
            out var warnings);

        var runs = Assert.IsType<IrParagraph>(Assert.Single(blocks)).Runs;
        var link = Assert.Single(runs.OfType<IrLink>());
        Assert.Equal(PdfAnchors.PageAnchor(PagePath.Parse("/Guides/Getting-Started")), link.AnchorId);

        var images = runs.OfType<IrInlineImage>().ToList();
        Assert.Equal("base64:AAAA", images[0].ResolvedPath);
        Assert.Null(images[1].ResolvedPath);
        Assert.Equal("nope.png", Assert.Single(warnings).Target);
    }
}
