using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class PagePathTests
{
    [Fact]
    public void Parse_Root()
    {
        Assert.True(PagePath.Parse("/").IsRoot);
        Assert.True(PagePath.Parse("").IsRoot);
    }

    [Fact]
    public void Parse_NestedLink()
    {
        var path = PagePath.Parse("/Getting-Started/Install");
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal("Getting Started", path.Segments[0].Title);
        Assert.Equal("Install", path.Segments[1].Title);
    }

    [Fact]
    public void ToLinkPath_RoundTrips()
    {
        var path = PagePath.Parse("/A/B-C/D");
        Assert.Equal("/A/B-C/D", path.ToLinkPath());
    }

    [Fact]
    public void ToFilePath_UsesPlatformSeparator()
    {
        var path = PagePath.Parse("/A/B");
        var expected = System.IO.Path.Combine("A", "B.md");
        Assert.Equal(expected, path.ToFilePath());
    }

    [Fact]
    public void RootHasNoName()
    {
        Assert.Throws<InvalidOperationException>(() => _ = PagePath.Root.Name);
    }
}
