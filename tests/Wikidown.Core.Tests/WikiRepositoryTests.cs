using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public WikiRepositoryTests()
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
    public void Write_CreatesFileAndUpdatesOrder()
    {
        var page = new WikiPage(PagePath.Parse("/Getting-Started"), "# Hello\n");
        _repo.Write(page);

        var file = Path.Combine(_root, "Getting-Started.md");
        Assert.True(File.Exists(file));
        Assert.Equal("# Hello\n", File.ReadAllText(file));

        var order = File.ReadAllText(Path.Combine(_root, ".order"));
        Assert.Contains("Getting-Started", order);
    }

    [Fact]
    public void Write_Nested_CreatesFolders()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A"));
        _repo.Write(new WikiPage(PagePath.Parse("/A/B"), "# B"));
        _repo.Write(new WikiPage(PagePath.Parse("/A/B/C"), "# C"));

        Assert.True(File.Exists(Path.Combine(_root, "A.md")));
        Assert.True(File.Exists(Path.Combine(_root, "A", "B.md")));
        Assert.True(File.Exists(Path.Combine(_root, "A", "B", "C.md")));
        Assert.True(File.Exists(Path.Combine(_root, "A", "B", ".order")));
    }

    [Fact]
    public void Read_ReturnsMarkdown()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Install"), "# Install\ncontent"));
        var page = _repo.Read(PagePath.Parse("/Install"));
        Assert.StartsWith("# Install", page.Markdown);
    }

    [Fact]
    public void ListChildren_RespectsOrderThenAlpha()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Banana"), "b"));
        _repo.Write(new WikiPage(PagePath.Parse("/Apple"), "a"));
        _repo.Write(new WikiPage(PagePath.Parse("/Cherry"), "c"));
        _repo.WriteOrder(PagePath.Root, new[] { "Cherry", "Banana" });

        var children = _repo.ListChildren(PagePath.Root);
        Assert.Equal(new[] { "Cherry", "Banana", "Apple" },
            children.Select(c => c.Name.FileBase));
    }

    [Fact]
    public void Move_RenamesFileAndSubpages()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Old"), "old"));
        _repo.Write(new WikiPage(PagePath.Parse("/Old/Child"), "child"));

        _repo.Move(PagePath.Parse("/Old"), PagePath.Parse("/New"));

        Assert.False(File.Exists(Path.Combine(_root, "Old.md")));
        Assert.True(File.Exists(Path.Combine(_root, "New.md")));
        Assert.True(File.Exists(Path.Combine(_root, "New", "Child.md")));
    }

    [Fact]
    public void Delete_RequiresRecursive_ForSubpages()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "p"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Kid"), "k"));

        Assert.Throws<InvalidOperationException>(() =>
            _repo.Delete(PagePath.Parse("/Parent")));

        _repo.Delete(PagePath.Parse("/Parent"), deleteSubpages: true);
        Assert.False(Directory.Exists(Path.Combine(_root, "Parent")));
        Assert.False(File.Exists(Path.Combine(_root, "Parent.md")));
    }

    [Fact]
    public void Search_FindsMatches()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "line1\nhello world\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "nothing here"));
        var hits = PageSearch.Search(_repo, "hello").ToList();
        Assert.Single(hits);
        Assert.Equal("/A", hits[0].Path.ToLinkPath());
        Assert.Equal(2, hits[0].LineNumber);
    }
}
