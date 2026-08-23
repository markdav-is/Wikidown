using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class IndexCheckerTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public IndexCheckerTests()
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
    public void Check_FindsFolderWithMissingParentPage()
    {
        // Write a grandchild directly, without ever creating /Architecture —
        // WikiRepository.Write doesn't require the parent page to exist.
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));

        var issue = Assert.Single(IndexChecker.Check(_repo));
        Assert.Equal(IndexIssueKind.MissingParentPage, issue.Kind);
        Assert.Equal("/Architecture", issue.Folder.ToLinkPath());
        Assert.Null(issue.Child);
    }

    [Fact]
    public void Check_FindsOrphanedFolder_InvisibleToWalk()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));

        // Confirms the motivating gap: repo.Walk() can't see this page at
        // all, since it never descends into a page that was never discovered.
        Assert.DoesNotContain(
            PagePath.Parse("/Architecture/Data-Model").ToLinkPath(),
            _repo.Walk().Select(p => p.ToLinkPath()));

        Assert.NotEmpty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void Check_FindsChildNotLinkedFromParent()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture"), "# Architecture\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));

        var issue = Assert.Single(IndexChecker.Check(_repo));
        Assert.Equal(IndexIssueKind.ChildNotLinked, issue.Kind);
        Assert.Equal("/Architecture/Data-Model", issue.Child!.ToLinkPath());
    }

    [Fact]
    public void Check_PassesWhenParentLinksChild_RelativeForm()
    {
        _repo.Write(new WikiPage(
            PagePath.Parse("/Architecture"),
            "# Architecture\n\n[Data Model](Architecture/Data-Model.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));

        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void Check_PassesWhenParentLinksChild_AbsoluteForm()
    {
        _repo.Write(new WikiPage(
            PagePath.Parse("/Architecture"),
            "# Architecture\n\n[Data Model](/Architecture/Data-Model)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));

        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void Check_IgnoresDotFolders()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A\n"));
        Directory.CreateDirectory(Path.Combine(_root, ".attachments"));
        File.WriteAllText(Path.Combine(_root, ".attachments", "map.png"), "png");

        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void Check_MultipleChildren_AllMustBeLinked()
    {
        _repo.Write(new WikiPage(
            PagePath.Parse("/Architecture"),
            "# Architecture\n\n[Data Model](Architecture/Data-Model.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/Data-Model"), "# Data Model\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Architecture/API-Reference"), "# API Reference\n"));

        var issue = Assert.Single(IndexChecker.Check(_repo));
        Assert.Equal("/Architecture/API-Reference", issue.Child!.ToLinkPath());
    }

    [Fact]
    public void Check_IgnoresJekyllAndAssetFolders()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        Directory.CreateDirectory(Path.Combine(_root, "_layouts"));
        File.WriteAllText(Path.Combine(_root, "_layouts", "wikidown.html"), "<html></html>");
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "assets", "site.css"), "body{}");

        Assert.Empty(IndexChecker.Check(_repo));
    }
}
