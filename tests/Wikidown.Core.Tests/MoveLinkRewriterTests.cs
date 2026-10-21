using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class MoveLinkRewriterTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public MoveLinkRewriterTests()
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
    public void MoveAndRewrite_RewritesInboundRelativeLink()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "see [Old](Old.md)\n"));

        var rewrites = MoveLinkRewriter.MoveAndRewrite(
            _repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        Assert.Single(rewrites);
        var other = _repo.Read(PagePath.Parse("/Other"));
        Assert.Contains("(New.md)", other.Markdown);
        Assert.True(_repo.Exists(PagePath.Parse("/New")));
    }

    [Fact]
    public void MoveAndRewrite_RewritesHrefsInRawHtmlAndSvg()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"),
            "<a href=\"Old.md\">html</a>\n" +
            "<svg><a href=\"Old.md#pin\"><circle/></a><a xlink:href='Old.md'><circle/></a>" +
            "<a href=\"https://x.y/Old.md\"><circle/></a></svg>\n"));

        var rewrites = MoveLinkRewriter.MoveAndRewrite(_repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        Assert.Equal(3, rewrites.Count);
        var other = _repo.Read(PagePath.Parse("/Other")).Markdown;
        Assert.Contains("<a href=\"New.md\">html</a>", other);
        Assert.Contains("<a href=\"New.md#pin\">", other);
        Assert.Contains("<a xlink:href='New.md'>", other);
        Assert.Contains("https://x.y/Old.md", other);
    }

    [Fact]
    public void MoveAndRewrite_RewritesInboundAbsoluteTitlePathLink()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "see [Old](/Old)\n"));

        MoveLinkRewriter.MoveAndRewrite(_repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        var other = _repo.Read(PagePath.Parse("/Other"));
        Assert.Contains("(/New)", other.Markdown);
    }

    [Fact]
    public void MoveAndRewrite_RewritesInboundLinkToSubpageOfMovedPage()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Old/Child"), "# Child\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "see [Child](Old/Child.md)\n"));

        MoveLinkRewriter.MoveAndRewrite(_repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        var other = _repo.Read(PagePath.Parse("/Other"));
        Assert.Contains("(New/Child.md)", other.Markdown);
        Assert.True(_repo.Exists(PagePath.Parse("/New/Child")));
    }

    [Fact]
    public void MoveAndRewrite_RecomputesMovedPagesOwnLinksForNewDepth()
    {
        // Root-level attachments; page moving from depth 1 to depth 2 needs
        // one more "../" to reach them.
        Directory.CreateDirectory(Path.Combine(_root, ".attachments"));
        File.WriteAllText(Path.Combine(_root, ".attachments", "map.png"), "png");

        _repo.Write(new WikiPage(PagePath.Parse("/Encounters"), "# Encounters\n"));
        _repo.Write(new WikiPage(
            PagePath.Parse("/Encounters/The-Sky-Hunters"),
            "![map](../.attachments/map.png)\n"));

        var rewrites = MoveLinkRewriter.MoveAndRewrite(
            _repo,
            PagePath.Parse("/Encounters/The-Sky-Hunters"),
            PagePath.Parse("/Adventures/Return-to-Frostwatch/The-Sky-Hunters"));

        Assert.Contains(rewrites, r => r.NewTarget == "../../.attachments/map.png");
        var moved = _repo.Read(PagePath.Parse("/Adventures/Return-to-Frostwatch/The-Sky-Hunters"));
        Assert.Contains("(../../.attachments/map.png)", moved.Markdown);
    }

    [Fact]
    public void MoveAndRewrite_LeavesUnrelatedLinksAlone()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Unrelated"), "b"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "[Unrelated](Unrelated.md)\n"));

        var rewrites = MoveLinkRewriter.MoveAndRewrite(
            _repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        Assert.Empty(rewrites);
        Assert.Contains("(Unrelated.md)", _repo.Read(PagePath.Parse("/Other")).Markdown);
    }

    [Fact]
    public void Plan_DoesNotMutateWiki_ForDryRun()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "[Old](Old.md)\n"));

        var plan = MoveLinkRewriter.Plan(_repo, PagePath.Parse("/Old"), PagePath.Parse("/New"));

        Assert.Single(plan.Rewrites);
        Assert.True(_repo.Exists(PagePath.Parse("/Old")));
        Assert.False(_repo.Exists(PagePath.Parse("/New")));
        Assert.Contains("(Old.md)", _repo.Read(PagePath.Parse("/Other")).Markdown);
    }
}
