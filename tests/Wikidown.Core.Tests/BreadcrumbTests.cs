using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class BreadcrumbTests
{
    [Fact]
    public void Render_ReturnsNull_ForTopLevelPage()
    {
        Assert.Null(Breadcrumb.Render(PagePath.Parse("/CLI")));
    }

    [Fact]
    public void Render_OneAncestor()
    {
        var crumb = Breadcrumb.Render(PagePath.Parse("/A/B"));
        Assert.Equal("[A](../A.md) / B <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Render_TwoAncestors()
    {
        var crumb = Breadcrumb.Render(PagePath.Parse("/A/B/C"));
        Assert.Equal(
            "[A](../../A.md) / [B](../B.md) / C <!-- wikidown:breadcrumb -->", crumb);
    }

    [Fact]
    public void Inject_PrependsBreadcrumbBeforeExistingContent()
    {
        var result = Breadcrumb.Inject(PagePath.Parse("/A/B"), "# B\n\nbody\n");
        Assert.Equal(
            "[A](../A.md) / B <!-- wikidown:breadcrumb -->\n\n# B\n\nbody\n", result);
    }

    [Fact]
    public void Inject_IsIdempotent_NoDuplicateOnReinject()
    {
        var first = Breadcrumb.Inject(PagePath.Parse("/A/B"), "# B\n\nbody\n");
        var second = Breadcrumb.Inject(PagePath.Parse("/A/B"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, second.Split(Breadcrumb.Marker).Length - 1);
    }

    [Fact]
    public void Inject_OmitsBreadcrumb_ForTopLevelPage()
    {
        var result = Breadcrumb.Inject(PagePath.Parse("/CLI"), "# CLI\n\nbody\n");
        Assert.Equal("# CLI\n\nbody\n", result);
    }

    [Fact]
    public void Inject_StripsStaleBreadcrumb_WhenPageBecomesTopLevel()
    {
        var withCrumb = Breadcrumb.Inject(PagePath.Parse("/A/B"), "# B\n\nbody\n");
        var result = Breadcrumb.Inject(PagePath.Parse("/B"), withCrumb);
        Assert.Equal("# B\n\nbody\n", result);
    }
}
