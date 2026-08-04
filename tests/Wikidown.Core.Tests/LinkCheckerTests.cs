using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class LinkCheckerTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public LinkCheckerTests()
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
    public void Check_ReportsBrokenRelativeLink()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "[missing](Missing.md)\n"));

        var issues = LinkChecker.Check(_repo).ToList();

        var issue = Assert.Single(issues);
        Assert.Equal(LinkIssueKind.Broken, issue.Kind);
        Assert.Equal("Missing.md", issue.Target);
        Assert.Equal(1, issue.LineNumber);
    }

    [Fact]
    public void Check_AllowsResolvingRelativeLink()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "a"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "[A](A.md)\n"));

        Assert.Empty(LinkChecker.Check(_repo));
    }

    [Fact]
    public void Check_ResolvesRelativeLinkAcrossFolders()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "p"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "c"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "[Child](Parent/Child.md)\n"));

        Assert.Empty(LinkChecker.Check(_repo));
    }

    [Fact]
    public void Check_FlagsAbsoluteTitlePathByDefault()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "[B](/B)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "b"));

        var issue = Assert.Single(LinkChecker.Check(_repo));
        Assert.Equal(LinkIssueKind.AbsoluteTitlePath, issue.Kind);
    }

    [Fact]
    public void Check_CanDisableAbsolutePathFlag()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "[B](/B)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "b"));

        Assert.Empty(LinkChecker.Check(_repo, flagAbsolutePaths: false));
    }

    [Fact]
    public void Check_IgnoresExternalAndFragmentLinks()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"),
            "[ext](https://example.com) [mail](mailto:a@b.com) [frag](#section)\n"));

        Assert.Empty(LinkChecker.Check(_repo));
    }

    [Fact]
    public void Check_ReportsBrokenImage()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "![map](../.attachments/map.png)\n"));

        var issue = Assert.Single(LinkChecker.Check(_repo));
        Assert.Equal(LinkIssueKind.Broken, issue.Kind);
        Assert.Equal("../.attachments/map.png", issue.Target);
    }
}
