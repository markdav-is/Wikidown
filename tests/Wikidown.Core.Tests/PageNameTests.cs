using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class PageNameTests
{
    [Fact]
    public void TitleToFileBase_ReplacesSpaces()
    {
        var name = PageName.FromTitle("Getting Started");
        Assert.Equal("Getting-Started", name.FileBase);
        Assert.Equal("Getting-Started.md", name.FileName);
    }

    [Fact]
    public void FileBaseToTitle_ReplacesHyphens()
    {
        var name = PageName.FromFileBase("Release-Notes");
        Assert.Equal("Release Notes", name.Title);
    }

    [Fact]
    public void FileBase_StripsMdExtension()
    {
        var name = PageName.FromFileBase("Release-Notes.md");
        Assert.Equal("Release-Notes", name.FileBase);
    }

    [Theory]
    [InlineData("foo/bar")]
    [InlineData("foo:bar")]
    [InlineData("foo?")]
    [InlineData("#hash")]
    public void ForbiddenCharsRejected(string title)
    {
        Assert.Throws<ArgumentException>(() => PageName.FromTitle(title));
    }

    [Fact]
    public void EmptyTitleRejected()
    {
        Assert.Throws<ArgumentException>(() => PageName.FromTitle("   "));
    }
}
