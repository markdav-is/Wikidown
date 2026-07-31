using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class NavTreeTests
{
    private static IReadOnlyList<NavNode> Build(
        string[] pages,
        Dictionary<string, string[]>? orders = null)
    {
        var parsed = pages.Select(PagePath.Parse).ToList();
        return NavTree.Build(parsed, folder =>
            orders is not null && orders.TryGetValue(folder.ToLinkPath(), out var o)
                ? o
                : Array.Empty<string>());
    }

    [Fact]
    public void FlatPages_SortAlphabetically()
    {
        var tree = Build(new[] { "/Zeta", "/Alpha", "/Mid" });
        Assert.Equal(new[] { "Alpha", "Mid", "Zeta" }, tree.Select(n => n.Title));
        Assert.All(tree, n => Assert.True(n.IsPage));
        Assert.All(tree, n => Assert.Empty(n.Children));
    }

    [Fact]
    public void OrderEntries_SortFirst_InListedOrder()
    {
        var tree = Build(
            new[] { "/Alpha", "/Beta", "/Home", "/Guides" },
            new() { ["/"] = new[] { "Home", "Guides" } });
        Assert.Equal(new[] { "Home", "Guides", "Alpha", "Beta" }, tree.Select(n => n.Title));
    }

    [Fact]
    public void PageWithSameNamedFolder_BecomesOneNodeWithChildren()
    {
        var tree = Build(new[] { "/Guides", "/Guides/Install", "/Guides/Usage" });
        var guides = Assert.Single(tree);
        Assert.True(guides.IsPage);
        Assert.Equal("/Guides", guides.Path.ToLinkPath());
        Assert.Equal(new[] { "Install", "Usage" }, guides.Children.Select(c => c.Title));
    }

    [Fact]
    public void FolderWithoutPairedPage_IsBareFolderNode()
    {
        var tree = Build(new[] { "/Guides/Install" });
        var guides = Assert.Single(tree);
        Assert.False(guides.IsPage);
        Assert.Equal("Guides", guides.Title);
        var install = Assert.Single(guides.Children);
        Assert.True(install.IsPage);
    }

    [Fact]
    public void ChildOrder_UsesTheChildFoldersOrderFile()
    {
        var tree = Build(
            new[] { "/Guides", "/Guides/Zeta", "/Guides/Alpha" },
            new() { ["/Guides"] = new[] { "Zeta" } });
        var guides = Assert.Single(tree);
        Assert.Equal(new[] { "Zeta", "Alpha" }, guides.Children.Select(c => c.Title));
    }

    [Fact]
    public void Titles_RenderDashesAsSpaces()
    {
        var tree = Build(new[] { "/Getting-Started" });
        Assert.Equal("Getting Started", Assert.Single(tree).Title);
    }

    [Fact]
    public void DeepNesting_Builds()
    {
        var tree = Build(new[] { "/A", "/A/B", "/A/B/C" });
        var a = Assert.Single(tree);
        var b = Assert.Single(a.Children);
        var c = Assert.Single(b.Children);
        Assert.Equal("/A/B/C", c.Path.ToLinkPath());
    }
}
