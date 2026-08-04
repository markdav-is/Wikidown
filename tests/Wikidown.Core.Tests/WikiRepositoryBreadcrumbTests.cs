using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryBreadcrumbTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public WikiRepositoryBreadcrumbTests()
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
    public void Write_InjectsBreadcrumb_ForSubpage()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "# Parent\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "# Child\n"));

        var markdown = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        Assert.StartsWith("[Parent](../Parent.md) / Child <!-- wikidown:breadcrumb -->", markdown);
    }

    [Fact]
    public void Write_OmitsBreadcrumb_ForTopLevelPage()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));

        var markdown = _repo.Read(PagePath.Parse("/Home")).Markdown;
        Assert.DoesNotContain(Breadcrumb.Marker, markdown);
    }

    [Fact]
    public void Write_Twice_DoesNotDuplicateBreadcrumb()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "# Parent\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "# Child\n"));
        var roundTripped = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), roundTripped));

        var markdown = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        Assert.Equal(1, markdown.Split(Breadcrumb.Marker).Length - 1);
    }

    [Fact]
    public void Move_RegeneratesBreadcrumb_ForNewDepth()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Encounters"), "# Encounters\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Encounters/Foo"), "# Foo\n"));

        _repo.Move(
            PagePath.Parse("/Encounters/Foo"),
            PagePath.Parse("/Adventures/Chapter-One/Foo"));

        var markdown = _repo.Read(PagePath.Parse("/Adventures/Chapter-One/Foo")).Markdown;
        Assert.StartsWith(
            "[Adventures](../../Adventures.md) / [Chapter One](../Chapter-One.md) / Foo",
            markdown);
        Assert.DoesNotContain("Encounters", markdown);
    }

    [Fact]
    public void Move_RegeneratesBreadcrumb_WhenDepthStaysTheSameButParentChanges()
    {
        // Regression case: old and new breadcrumb hrefs can compute to the
        // identical "../X.md" string even though the ancestor is different,
        // since the relative-link math only depends on hop count. The
        // breadcrumb text itself (not just the link target) must still be
        // regenerated, not left stale.
        _repo.Write(new WikiPage(PagePath.Parse("/Encounters"), "# Encounters\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Adventures"), "# Adventures\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Encounters/Foo"), "# Foo\n"));

        _repo.Move(PagePath.Parse("/Encounters/Foo"), PagePath.Parse("/Adventures/Foo"));

        var markdown = _repo.Read(PagePath.Parse("/Adventures/Foo")).Markdown;
        Assert.StartsWith("[Adventures](../Adventures.md) / Foo", markdown);
        Assert.DoesNotContain("Encounters", markdown);
    }

    [Fact]
    public void Move_RegeneratesBreadcrumb_ForMovedDescendants()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "# Old\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Old/Child"), "# Child\n"));

        _repo.Move(PagePath.Parse("/Old"), PagePath.Parse("/New"));

        var markdown = _repo.Read(PagePath.Parse("/New/Child")).Markdown;
        Assert.StartsWith("[New](../New.md) / Child", markdown);
    }

    [Fact]
    public void Move_ToTopLevel_StripsBreadcrumb()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "# Parent\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "# Child\n"));

        _repo.Move(PagePath.Parse("/Parent/Child"), PagePath.Parse("/Child"));

        var markdown = _repo.Read(PagePath.Parse("/Child")).Markdown;
        Assert.DoesNotContain(Breadcrumb.Marker, markdown);
    }

    [Fact]
    public void BreadcrumbLinks_ResolveCleanly_UnderCheckLinks()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/A/B"), "# B\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/A/B/C"), "# C\n"));

        Assert.Empty(LinkChecker.Check(_repo));
    }
}
