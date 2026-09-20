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
        _repo = new WikiRepository(_root, LineEndings.Lf);
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

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void NewWiki_UsesTheGivenEnding_ForPagesAndOrder(string ending)
    {
        var repo = new WikiRepository(_root, ending);
        repo.Write(new WikiPage(PagePath.Parse("/One"), "# One\n\nBody.\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Two"), "# Two\n"));

        Assert.Equal($"# One{ending}{ending}Body.{ending}", File.ReadAllText(Path.Combine(_root, "One.md")));
        Assert.Equal($"One{ending}Two{ending}", File.ReadAllText(Path.Combine(_root, ".order")));
    }

    [Fact]
    public void CrlfWiki_StaysCrlf_WhateverThePlatformOrInputUses()
    {
        File.WriteAllText(Path.Combine(_root, "Home.md"), "# Home\r\n");
        File.WriteAllText(Path.Combine(_root, ".order"), "Home\r\n");

        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\nRewritten.\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guide"), "# Guide\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guide/Sub"), "# Sub\n"));
        _repo.Move(PagePath.Parse("/Guide/Sub"), PagePath.Parse("/Sub"));

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        }
        Assert.Equal("Home\r\nGuide\r\nSub\r\n", File.ReadAllText(Path.Combine(_root, ".order")));
    }

    [Fact]
    public void LfWiki_StaysLf_EvenWhenInputIsCrlf()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guide"), "# Guide\r\n\r\nBody.\r\n"));

        Assert.DoesNotContain("\r", File.ReadAllText(Path.Combine(_root, "Guide.md")));
        Assert.DoesNotContain("\r", File.ReadAllText(Path.Combine(_root, ".order")));
    }

    [Theory]
    [InlineData("/Report: Q1")]
    [InlineData("/What?")]
    [InlineData("/a*b")]
    [InlineData("/Parent/x|y")]
    [InlineData("/CON")]
    [InlineData("/nul")]
    [InlineData("/Parent/COM1")]
    [InlineData("/LPT9.notes")]
    [InlineData("/Trailing.")]
    public void Write_RefusesNamesThatCannotExistOnWindows_OnEveryPlatform(string path)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => _repo.Write(new WikiPage(PagePath.Parse(path), "# x\n")));
        Assert.Contains("Page name", ex.Message);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void Move_RefusesNonPortableDestination()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Notes"), "# Notes\n"));

        Assert.Throws<ArgumentException>(() => _repo.Move(PagePath.Parse("/Notes"), PagePath.Parse("/AUX")));
        Assert.True(_repo.Exists(PagePath.Parse("/Notes")));
    }

    [Fact]
    public void Move_CaseOnlyRename_Works_IncludingSubpageFolder()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/guide"), "# guide\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/guide/Sub"), "# Sub\n"));

        _repo.Move(PagePath.Parse("/guide"), PagePath.Parse("/Guide"));

        Assert.Contains("Guide.md", Directory.EnumerateFiles(_root).Select(Path.GetFileName));
        Assert.Contains("Guide", Directory.EnumerateDirectories(_root).Select(Path.GetFileName));
        Assert.DoesNotContain("guide.md", Directory.EnumerateFiles(_root).Select(Path.GetFileName));
        Assert.Equal(new[] { "Guide" }, _repo.ReadOrder(PagePath.Root));
        Assert.True(_repo.Exists(PagePath.Parse("/Guide/Sub")));
    }

    [Fact]
    public void Move_OntoADifferentExistingPage_StillRefused()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/B"), "# B\n"));

        Assert.Throws<InvalidOperationException>(() => _repo.Move(PagePath.Parse("/A"), PagePath.Parse("/B")));
    }

    [Fact]
    public void Writes_KeepAUtf8Bom_AndNeverAddOne()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var withBom = Path.Combine(_root, "WithBom.md");
        File.WriteAllBytes(withBom, bom.Concat("# WithBom\n\nold\n"u8.ToArray()).ToArray());
        _repo.Write(new WikiPage(PagePath.Parse("/Plain"), "# Plain\n\nold\n"));

        _repo.Edit(PagePath.Parse("/WithBom"), "old", "new");
        _repo.Write(new WikiPage(PagePath.Parse("/WithBom"), "# WithBom\n\nrewritten\n"));
        _repo.Edit(PagePath.Parse("/Plain"), "old", "new");

        Assert.Equal(bom, File.ReadAllBytes(withBom).Take(3));
        Assert.NotEqual(bom, File.ReadAllBytes(Path.Combine(_root, "Plain.md")).Take(3));
        Assert.Contains("rewritten", File.ReadAllText(withBom));
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
