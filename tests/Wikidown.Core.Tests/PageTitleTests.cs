using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class PageTitleTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public PageTitleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Theory]
    [InlineData("# Attacks & Defense\n", "Attacks & Defense")]
    [InlineData("# Move-1\n", "Move-1")]
    [InlineData("# The `wikidown` *CLI*\n", "The wikidown CLI")]
    [InlineData("# [To Do](Kanban/To-Do.md)\n", "To Do")]
    [InlineData("[Home](../Home.md) / Cards <!-- wikidown:breadcrumb -->\r\n\r\n# Cards\r\n", "Cards")]
    [InlineData("## Only a subheading\n\n# Later H1\n", "Later H1")]
    public void FromMarkdown_UsesFirstH1AsPlainText(string markdown, string expected) =>
        Assert.Equal(expected, PageTitle.FromMarkdown(markdown));

    [Theory]
    [InlineData("no heading here\n")]
    [InlineData("```\n# not a heading\n```\n")]
    [InlineData("## Sub only\n")]
    [InlineData("")]
    public void FromMarkdown_NullWithoutAnH1(string markdown) =>
        Assert.Null(PageTitle.FromMarkdown(markdown));

    [Fact]
    public void For_FallsBackToTheFileName() =>
        Assert.Equal("Attacks Defense", PageTitle.For(PagePath.Parse("/Attacks-Defense"), "no heading\n"));

    [Fact]
    public void Nav_UsesHeadingsInEveryExport()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Cards"), "# Cards\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Cards/Attacks-Defense"), "# Attacks & Defense\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Cards/Move-1"), "no heading\n"));

        Assert.Contains("title: \"Attacks & Defense\"", JekyllNavigation.Render(_repo));
        Assert.Contains("title: \"Move 1\"", JekyllNavigation.Render(_repo));

        var pdf = WikiPdfContent.BuildAll(_repo);
        var cards = Assert.Single(pdf.Nav);
        Assert.Equal(["Attacks & Defense", "Move 1"], cards.Children.Select(c => c.Title));
        Assert.Contains(pdf.Pages, p => p.Title == "Attacks & Defense");
    }
}
