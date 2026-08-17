using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class MarkdownIrBuilderTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public MarkdownIrBuilderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void Heading_ProducesHeadingBlockWithCorrectLevel()
    {
        var blocks = MarkdownIrBuilder.Build("## Title\n", PagePath.Parse("/A"), _repo);

        var heading = Assert.IsType<IrHeading>(Assert.Single(blocks));
        Assert.Equal(2, heading.Level);
        Assert.Equal("Title", Assert.IsType<IrText>(Assert.Single(heading.Runs)).Text);
    }

    [Fact]
    public void BoldItalic_ProduceStyledTextRuns()
    {
        var blocks = MarkdownIrBuilder.Build("**bold** *italic* plain\n", PagePath.Parse("/A"), _repo);

        var para = Assert.IsType<IrParagraph>(Assert.Single(blocks));
        var runs = para.Runs.Cast<IrText>().ToList();
        Assert.Contains(runs, r => r.Text == "bold" && r.Bold && !r.Italic);
        Assert.Contains(runs, r => r.Text == "italic" && r.Italic && !r.Bold);
    }

    [Fact]
    public void NestedBulletList_ProducesNestedListBlock()
    {
        var blocks = MarkdownIrBuilder.Build("- Top\n  - Nested\n", PagePath.Parse("/A"), _repo);

        var list = Assert.IsType<IrList>(Assert.Single(blocks));
        Assert.False(list.Ordered);
        var item = Assert.Single(list.Items);
        Assert.Equal("Top", Assert.IsType<IrText>(Assert.Single(item.Runs)).Text);
        var nestedItem = Assert.Single(item.Nested!.Items);
        Assert.Equal("Nested", Assert.IsType<IrText>(Assert.Single(nestedItem.Runs)).Text);
    }

    [Fact]
    public void FencedCodeBlock_PreservesLanguageAndRawText()
    {
        var blocks = MarkdownIrBuilder.Build("```csharp\nvar x = 1;\n```\n", PagePath.Parse("/A"), _repo);

        var code = Assert.IsType<IrCodeBlock>(Assert.Single(blocks));
        Assert.Equal("csharp", code.Language);
        Assert.Contains("var x = 1;", code.Code);
    }

    [Fact]
    public void PipeTable_ProducesTableBlockWithHeaderAndRows()
    {
        var md = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var blocks = MarkdownIrBuilder.Build(md, PagePath.Parse("/A"), _repo);

        var table = Assert.IsType<IrTable>(Assert.Single(blocks));
        Assert.Equal("A", Assert.IsType<IrText>(Assert.Single(table.HeaderCells[0])).Text);
        Assert.Equal("B", Assert.IsType<IrText>(Assert.Single(table.HeaderCells[1])).Text);
        var row = Assert.Single(table.Rows);
        Assert.Equal("1", Assert.IsType<IrText>(Assert.Single(row[0])).Text);
    }

    [Fact]
    public void RelativeLink_ResolvesToInternalAnchorId()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Target"), "# Target\n"));

        var blocks = MarkdownIrBuilder.Build("[go](Target.md)\n", PagePath.Parse("/A"), _repo);

        var para = Assert.IsType<IrParagraph>(Assert.Single(blocks));
        var link = Assert.IsType<IrLink>(Assert.Single(para.Runs));
        Assert.Equal(PdfAnchors.PageAnchor(PagePath.Parse("/Target")), link.AnchorId);
    }

    [Fact]
    public void AbsoluteTitlePathLink_StillResolves()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Target-Page"), "# Target Page\n"));

        var blocks = MarkdownIrBuilder.Build("[go](/Target-Page)\n", PagePath.Parse("/A"), _repo);

        var para = Assert.IsType<IrParagraph>(Assert.Single(blocks));
        var link = Assert.IsType<IrLink>(Assert.Single(para.Runs));
        Assert.Equal(PdfAnchors.PageAnchor(PagePath.Parse("/Target-Page")), link.AnchorId);
    }

    [Fact]
    public void ExternalLink_PassesThroughUnchanged()
    {
        var blocks = MarkdownIrBuilder.Build("[site](https://example.com)\n", PagePath.Parse("/A"), _repo);

        var para = Assert.IsType<IrParagraph>(Assert.Single(blocks));
        var link = Assert.IsType<IrExternalLink>(Assert.Single(para.Runs));
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void BrokenLink_DowngradesToPlainText()
    {
        var blocks = MarkdownIrBuilder.Build("[go](Missing.md)\n", PagePath.Parse("/A"), _repo);

        var para = Assert.IsType<IrParagraph>(Assert.Single(blocks));
        var text = Assert.IsType<IrText>(Assert.Single(para.Runs));
        Assert.Equal("go", text.Text);
    }

    [Fact]
    public void FragmentOnlyLink_ResolvesToHeadingAnchorOnSamePage()
    {
        var md = "## Install Steps\n\n[Jump](#install-steps)\n";
        var blocks = MarkdownIrBuilder.Build(md, PagePath.Parse("/A"), _repo);

        var heading = Assert.IsType<IrHeading>(blocks[0]);
        var para = Assert.IsType<IrParagraph>(blocks[1]);
        var link = Assert.IsType<IrLink>(Assert.Single(para.Runs));
        Assert.Equal(heading.AnchorId, link.AnchorId);
    }

    [Fact]
    public void MissingImage_ProducesPlaceholderBlockNotThrow()
    {
        var blocks = MarkdownIrBuilder.Build("![alt](missing.png)\n", PagePath.Parse("/A"), _repo);

        var img = Assert.IsType<IrImage>(Assert.Single(blocks));
        Assert.Null(img.ResolvedPath);
        Assert.Equal("alt", img.AltText);
    }

    [Fact]
    public void MissingImage_RecordsWarning()
    {
        MarkdownIrBuilder.Build(
            "![alt](missing.png)\n", PagePath.Parse("/A"), _repo, allowHtmlSkip: false, out var warnings);

        var warning = Assert.Single(warnings);
        Assert.Equal("/A", warning.Page.ToLinkPath());
        Assert.Equal("missing.png", warning.Target);
    }

    [Fact]
    public void ExternalImage_ProducesNoWarning()
    {
        MarkdownIrBuilder.Build(
            "![alt](https://example.com/pic.png)\n", PagePath.Parse("/A"), _repo, allowHtmlSkip: false, out var warnings);

        Assert.Empty(warnings);
    }

    [Fact]
    public void BlockQuote_ProducesBlockQuoteWithParagraph()
    {
        var blocks = MarkdownIrBuilder.Build("> A quoted line.\n", PagePath.Parse("/A"), _repo);

        var quote = Assert.IsType<IrBlockQuote>(Assert.Single(blocks));
        var para = Assert.IsType<IrParagraph>(Assert.Single(quote.Blocks));
        Assert.Equal("A quoted line.", Assert.IsType<IrText>(Assert.Single(para.Runs)).Text);
    }

    [Fact]
    public void NestedBlockQuote_ProducesNestedBlockQuote()
    {
        var blocks = MarkdownIrBuilder.Build("> Outer\n>\n> > Inner\n", PagePath.Parse("/A"), _repo);

        var quote = Assert.IsType<IrBlockQuote>(Assert.Single(blocks));
        var nested = Assert.IsType<IrBlockQuote>(quote.Blocks[1]);
        var para = Assert.IsType<IrParagraph>(Assert.Single(nested.Blocks));
        Assert.Equal("Inner", Assert.IsType<IrText>(Assert.Single(para.Runs)).Text);
    }

    [Fact]
    public void RawHtmlBlock_ThrowsByDefault()
    {
        Assert.Throws<NotSupportedException>(
            () => MarkdownIrBuilder.Build("<div>raw</div>\n", PagePath.Parse("/A"), _repo));
    }

    [Fact]
    public void RawHtmlBlock_WithAllowSkip_ProducesPlaceholder()
    {
        var blocks = MarkdownIrBuilder.Build(
            "<div>raw</div>\n", PagePath.Parse("/A"), _repo, allowHtmlSkip: true);

        Assert.IsType<IrHtmlPlaceholder>(Assert.Single(blocks));
    }
}
