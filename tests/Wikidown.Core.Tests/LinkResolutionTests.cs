using Xunit;
using Wikidown.Core;

namespace Wikidown.Core.Tests;

public class LinkResolutionTests
{
    private static readonly PagePath Nested = PagePath.Parse("/Adventures/Bar/Foo");
    private static readonly PagePath Root = PagePath.Parse("/Home");

    [Theory]
    [InlineData("../.attachments/pic.png", "Adventures/.attachments/pic.png")]
    [InlineData("../../.attachments/pic.png", ".attachments/pic.png")]
    [InlineData("./img/x.png", "Adventures/Bar/img/x.png")]
    [InlineData("x.png", "Adventures/Bar/x.png")]
    [InlineData("/.attachments/pic.png", ".attachments/pic.png")]
    [InlineData("/.attachments/a%20b.png#frag", ".attachments/a b.png")]
    public void Resolves_relative_to_page_folder_or_docs_root(string target, string expected)
    {
        Assert.Equal(expected, LinkChecker.ResolveDocsRelativePath(Nested, target));
    }

    [Theory]
    [InlineData("https://example.com/x.png")]
    [InlineData("http://example.com/x.png")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("#section")]
    [InlineData("")]
    [InlineData("../../x.png")]
    public void Returns_null_for_external_fragment_or_escaping_targets(string target)
    {
        Assert.Null(LinkChecker.ResolveDocsRelativePath(Root, target));
    }
}
