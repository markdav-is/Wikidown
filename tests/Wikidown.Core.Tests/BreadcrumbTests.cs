using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class BreadcrumbTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public BreadcrumbTests()
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
    public void Render_ReturnsNull_ForTopLevelPage_WithoutHome()
    {
        Assert.Null(Breadcrumb.Render(_repo, PagePath.Parse("/CLI")));
    }

    [Fact]
    public void Render_OneAncestor_WithoutHome()
    {
        var crumb = Breadcrumb.Render(_repo, PagePath.Parse("/A/B"));
        Assert.Equal("[A](../A.md) / B <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Render_TwoAncestors_WithoutHome()
    {
        var crumb = Breadcrumb.Render(_repo, PagePath.Parse("/A/B/C"));
        Assert.Equal(
            "[A](../../A.md) / [B](../B.md) / C <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Render_TopLevelPage_LeadsWithHome_WhenHomeExists()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));

        var crumb = Breadcrumb.Render(_repo, PagePath.Parse("/CLI"));
        Assert.Equal("[Home](Home.md) / CLI <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Render_NestedPage_LeadsWithHome_WhenHomeExists()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));

        var crumb = Breadcrumb.Render(_repo, PagePath.Parse("/A/B"));
        Assert.Equal(
            "[Home](../Home.md) / [A](../A.md) / B <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Render_ReturnsNull_ForHomeItself()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));

        Assert.Null(Breadcrumb.Render(_repo, PagePath.Parse("/Home")));
    }

    [Fact]
    public void Render_ChildOfHome_DoesNotDuplicateHomeSegment()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));

        var crumb = Breadcrumb.Render(_repo, PagePath.Parse("/Home/Something"));
        Assert.Equal("[Home](../Home.md) / Something <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Inject_PrependsBreadcrumbBeforeExistingContent()
    {
        var result = Breadcrumb.Inject(_repo, PagePath.Parse("/A/B"), "# B\n\nbody\n");
        Assert.Equal(
            "[A](../A.md) / B <!-- wikidown:breadcrumb -->\n\n# B\n\nbody\n", result);
    }

    [Fact]
    public void Inject_IsIdempotent_NoDuplicateOnReinject()
    {
        var first = Breadcrumb.Inject(_repo, PagePath.Parse("/A/B"), "# B\n\nbody\n");
        var second = Breadcrumb.Inject(_repo, PagePath.Parse("/A/B"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, second.Split(Breadcrumb.Marker).Length - 1);
    }

    [Fact]
    public void Inject_OmitsBreadcrumb_ForTopLevelPage_WithoutHome()
    {
        var result = Breadcrumb.Inject(_repo, PagePath.Parse("/CLI"), "# CLI\n\nbody\n");
        Assert.Equal("# CLI\n\nbody\n", result);
    }

    [Fact]
    public void Inject_StripsStaleBreadcrumb_WhenPageBecomesTopLevel()
    {
        var withCrumb = Breadcrumb.Inject(_repo, PagePath.Parse("/A/B"), "# B\n\nbody\n");
        var result = Breadcrumb.Inject(_repo, PagePath.Parse("/B"), withCrumb);
        Assert.Equal("# B\n\nbody\n", result);
    }
}
