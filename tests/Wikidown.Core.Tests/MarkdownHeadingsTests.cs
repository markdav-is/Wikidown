using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class MarkdownHeadingsTests
{
    [Theory]
    [InlineData("Voice", "Voice")]
    [InlineData("## Voice", "Voice")]
    [InlineData("  ###   Voice  ", "Voice")]
    [InlineData("## Voice ##", "Voice")]
    [InlineData("The Pivot — she's going", "The Pivot — she's going")]
    [InlineData("#", "")]
    public void Normalize_StripsHashesAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, MarkdownHeadings.Normalize(input));
    }

    [Fact]
    public void Parse_FindsAtxHeadingsWithLevelsAndLines()
    {
        var lines = new[] { "# Title", "", "## Two", "text", "### Three ###", "#not a heading", "####### seven" };
        var headings = MarkdownHeadings.Parse(lines);

        Assert.Equal(3, headings.Count);
        Assert.Equal(new HeadingInfo(1, 1, "Title"), headings[0]);
        Assert.Equal(new HeadingInfo(3, 2, "Two"), headings[1]);
        Assert.Equal(new HeadingInfo(5, 3, "Three"), headings[2]);
    }

    [Fact]
    public void Parse_IgnoresHeadingsInsideFencedCode()
    {
        var lines = new[] { "## Real", "```sh", "# comment", "## also not", "```", "~~~", "# nope", "~~~", "## Real 2" };
        var headings = MarkdownHeadings.Parse(lines);

        Assert.Equal(new[] { "Real", "Real 2" }, headings.Select(h => h.Text));
        Assert.Equal(9, headings[1].Line);
    }

    [Fact]
    public void Find_IsCaseInsensitiveAndIgnoresHashes()
    {
        var headings = MarkdownHeadings.Parse(new[] { "## Open Concerns", "## Notes", "## notes" });

        Assert.Single(MarkdownHeadings.Find(headings, "open concerns"));
        Assert.Single(MarkdownHeadings.Find(headings, "### OPEN CONCERNS"));
        Assert.Equal(2, MarkdownHeadings.Find(headings, "Notes").Count);
        Assert.Empty(MarkdownHeadings.Find(headings, "Open"));
    }

    [Fact]
    public void SectionEnd_StopsAtSameOrHigherLevel_OrEndOfFile()
    {
        var lines = new[] { "# T", "## A", "### A1", "## B", "text" };
        var headings = MarkdownHeadings.Parse(lines);

        Assert.Equal(4, MarkdownHeadings.SectionEnd(headings, headings[1], lines.Length)); // ## A ends at ## B
        Assert.Equal(4, MarkdownHeadings.SectionEnd(headings, headings[2], lines.Length)); // ### A1 ends at ## B
        Assert.Equal(6, MarkdownHeadings.SectionEnd(headings, headings[3], lines.Length)); // ## B runs to EOF
        Assert.Equal(6, MarkdownHeadings.SectionEnd(headings, headings[0], lines.Length)); // # T runs to EOF
    }

    [Fact]
    public void Describe_ListsHeadingTexts()
    {
        var headings = MarkdownHeadings.Parse(new[] { "# T", "## A", "### A1" });
        Assert.Equal("headings: T · A · A1", MarkdownHeadings.Describe(headings));
        Assert.Equal("page has no headings", MarkdownHeadings.Describe(Array.Empty<HeadingInfo>()));
    }
}
