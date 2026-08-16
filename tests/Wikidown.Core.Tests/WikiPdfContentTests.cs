using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiPdfContentTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public WikiPdfContentTests()
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
    public void BuildAll_WalksPagesInNavOrder()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Beta"), "# Beta\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Alpha"), "# Alpha\n"));
        _repo.WriteOrder(PagePath.Root, new[] { "Alpha", "Beta" });

        var content = WikiPdfContent.BuildAll(_repo);

        Assert.Equal(new[] { "Alpha", "Beta" }, content.Pages.Select(p => p.Title));
    }

    [Fact]
    public void BuildAll_FromSubtree_ScopesToNodeAndDescendants()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "# Other\n"));

        var content = WikiPdfContent.BuildAll(_repo, PagePath.Parse("/Guides"));

        Assert.Equal(new[] { "Guides", "Install" }, content.Pages.Select(p => p.Title));
    }

    [Fact]
    public void BuildAll_FromBareFolder_ScopesToDescendantsOnly()
    {
        // /Guides here is a folder with no paired Guides.md — from should
        // still work, just with nothing to include for the folder itself.
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "# Other\n"));

        var content = WikiPdfContent.BuildAll(_repo, PagePath.Parse("/Guides"));

        Assert.Equal(new[] { "Install" }, content.Pages.Select(p => p.Title));
    }

    [Fact]
    public void BuildAll_NavTreeMatchesPageHierarchy()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n"));

        var content = WikiPdfContent.BuildAll(_repo);

        var guides = Assert.Single(content.Nav);
        Assert.Equal("Guides", guides.Title);
        var install = Assert.Single(guides.Children);
        Assert.Equal("Install", install.Title);
    }

    [Fact]
    public void BuildAll_AggregatesWarningsAcrossPages()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A\n\n![x](missing-a.png)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "# B\n\n![y](missing-b.png)\n"));

        var content = WikiPdfContent.BuildAll(_repo);

        Assert.Equal(2, content.Warnings.Count);
        Assert.Contains(content.Warnings, w => w.Page.ToLinkPath() == "/A" && w.Target == "missing-a.png");
        Assert.Contains(content.Warnings, w => w.Page.ToLinkPath() == "/B" && w.Target == "missing-b.png");
    }

    [Fact]
    public void BuildAll_StripsBreadcrumbBeforeParsing()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Home/Child"), "# Child\ncontent\n"));

        var content = WikiPdfContent.BuildAll(_repo);
        var child = content.Pages.Single(p => p.Title == "Child");

        var heading = Assert.IsType<IrHeading>(child.Blocks[0]);
        Assert.Equal("Child", Assert.IsType<IrText>(Assert.Single(heading.Runs)).Text);
    }
}
