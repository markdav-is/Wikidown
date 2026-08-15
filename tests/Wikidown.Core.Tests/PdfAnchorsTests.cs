using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class PdfAnchorsTests
{
    [Fact]
    public void PageAnchor_IsStableAcrossCalls()
    {
        var page = PagePath.Parse("/Getting-Started/Install");
        Assert.Equal(PdfAnchors.PageAnchor(page), PdfAnchors.PageAnchor(page));
        Assert.Equal("page:/Getting-Started/Install", PdfAnchors.PageAnchor(page));
    }

    [Fact]
    public void HeadingAnchor_IncludesPageAnchorAndSlug()
    {
        var page = PagePath.Parse("/A");
        Assert.Equal("page:/A#install-steps", PdfAnchors.HeadingAnchor(page, "install-steps"));
    }
}
