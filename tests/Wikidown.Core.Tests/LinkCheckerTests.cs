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
    public void Check_IgnoresLinksInsideCode()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"),
            "Write `[x](Missing.md)` or ``[y](/Abs) `tick` ``.\r\n\r\n" +
            "```markdown\r\n[z](Gone.md)\r\n~~~\r\n[still](Fenced.md)\r\n```\r\n\r\n" +
            "~~~\r\n![img](nope.png)\r\n~~~\r\n\r\n" +
            "After `code` a real [broken](Real.md) link.\r\n"));

        var issue = Assert.Single(LinkChecker.Check(_repo));
        Assert.Equal("Real.md", issue.Target);
        Assert.Equal(13, issue.LineNumber);
    }

    [Fact]
    public void Check_ReportsBrokenHrefsInRawHtmlAndSvg()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Wavir/Balic"), "# Balic\n"));
        Directory.CreateDirectory(Path.Combine(_root, ".attachments"));
        File.WriteAllText(Path.Combine(_root, ".attachments", "atlas.png"), "png");
        _repo.Write(new WikiPage(PagePath.Parse("/Wavir"),
            "<a href=\"Wavir/Balic.md\">ok</a> <a data-href=\"Nope.md\" href=\"https://x.y/z.md\">ext</a>\n" +
            "<svg viewBox=\"0 0 10 10\">\n" +
            "  <image href=\".attachments/atlas.png\"/><image href=\".attachments/gone.png\"/>\n" +
            "  <a href=\"Wavir/Balic.md#top\"><circle r=\"1\"/></a>\n" +
            "  <a href=\"Wavir/Renamed.md\"><circle r=\"1\"/></a>\n" +
            "  <a xlink:href='Old.md'><circle r=\"1\"/></a>\n" +
            "</svg>\n" +
            "`<a href=\"InCode.md\">`\n"));

        var issues = LinkChecker.Check(_repo).ToList();

        Assert.All(issues, i => Assert.Equal(LinkIssueKind.Broken, i.Kind));
        Assert.Equal(
            [(3, ".attachments/gone.png"), (5, "Wavir/Renamed.md"), (6, "Old.md")],
            issues.Select(i => (i.LineNumber, i.Target)));
    }

    [Fact]
    public void Check_AllowsResolvingRelativeLink()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "a"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "[A](A.md)\n"));

        Assert.Empty(LinkChecker.Check(_repo));
    }

    // Same verdict on every platform: Windows would resolve these links, the
    // Linux host the wiki is published to would not.
    [Fact]
    public void Check_FlagsLinkWhoseCaseDiffersFromDisk()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "p"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "c"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "[a](parent.md)\n[b](PARENT/Child.md)\n[ok](Parent/Child.md)\n"));

        var issues = LinkChecker.Check(_repo).Where(i => i.Page.ToLinkPath() == "/Other").ToList();

        Assert.Equal(new[] { "parent.md", "PARENT/Child.md" }, issues.Select(i => i.Target));
        Assert.All(issues, i => Assert.True(
            i.Kind is LinkIssueKind.CaseMismatch or LinkIssueKind.Broken));
        if (OperatingSystem.IsWindows())
            Assert.All(issues, i => Assert.Equal(LinkIssueKind.CaseMismatch, i.Kind));
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
        Assert.Equal(LinkIssueKind.AbsolutePath, issue.Kind);
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
