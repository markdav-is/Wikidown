using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class OrderFileTests
{
    [Fact]
    public void Parse_IgnoresBlankAndComments()
    {
        var content = "Alpha\n\n# comment\nBeta\r\nGamma\n";
        var entries = OrderFile.Parse(content);
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, entries);
    }

    [Fact]
    public void Render_NormalizesAndTrimsEmpty()
    {
        var out1 = OrderFile.Render(new[] { "A", " ", "B ", "" });
        Assert.Equal("A\nB\n", out1);
    }

    [Fact]
    public void Render_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, OrderFile.Render(Array.Empty<string>()));
    }
}
