using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

// A Git work tree with the wiki in docs/ and art beside it in assets/, the
// layout that made check-links --fix necessary.
public class LinkFixerTests : IDisposable
{
    private readonly string _project;
    private readonly string _docs;
    private readonly WikiRepository _repo;

    public LinkFixerTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        _docs = Path.Combine(_project, "docs");
        Directory.CreateDirectory(Path.Combine(_project, ".git"));
        Directory.CreateDirectory(_docs);
        _repo = new WikiRepository(_docs);
        WriteAsset("assets/cards/duck.png", "duck");
        WriteAsset("assets/icons/duck.png", "icon");
        WriteAsset("assets/rules.pdf", "rules");
        WriteAsset("README.md", "# readme");
    }

    public void Dispose()
    {
        try { Directory.Delete(_project, recursive: true); } catch { /* best-effort */ }
    }

    private void WriteAsset(string relative, string content)
    {
        var full = Path.Combine(_project, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WritePage(string relative, string markdown) =>
        File.WriteAllText(Path.Combine(_docs, relative), markdown);

    private string ReadPage(string relative) => File.ReadAllText(Path.Combine(_docs, relative));

    private string Copy(string relative) => Path.Combine(_docs, ".attachments", "from-repo", relative);

    [Fact]
    public void Check_ReportsFilesOutsideTheWiki()
    {
        WritePage("A.md",
            "![a](../assets/cards/duck.png)\n" +
            "![b](/assets/icons/duck.png)\n" +
            "[rules](../assets/rules.pdf)\n" +
            "<img src=\"../assets/cards/duck.png\" alt=\"c\">\n" +
            "[readme](../README.md)\n" +
            "![web](https://example.com/x.png)\n" +
            "`![code](../assets/cards/duck.png)`\n");

        var issues = LinkChecker.Check(_repo).ToList();

        Assert.All(issues, i => Assert.Equal(LinkIssueKind.OutsideWiki, i.Kind));
        Assert.Equal([1, 2, 3, 4], issues.Select(i => i.LineNumber));
    }

    [Fact]
    public void Check_LeadingSlashPrefersTheWikiRoot()
    {
        Directory.CreateDirectory(Path.Combine(_docs, ".attachments"));
        File.WriteAllText(Path.Combine(_docs, ".attachments", "map.png"), "wiki map");
        WriteAsset(".attachments/map.png", "repo map");
        WritePage("A.md", "![map](/.attachments/map.png)\n");

        var issue = Assert.Single(LinkChecker.Check(_repo));
        Assert.Equal(LinkIssueKind.AbsoluteTitlePath, issue.Kind);
        Assert.Empty(LinkChecker.Check(_repo, flagAbsolutePaths: false));
    }

    [Fact]
    public void Fix_CopiesAndRelinks()
    {
        Directory.CreateDirectory(Path.Combine(_docs, "Cards"));
        WritePage("Cards.md", "# Cards\n");
        WritePage(Path.Combine("Cards", "Movement.md"),
            "| a | b |\n|---|---|\n| ![Duck](/assets/cards/duck.png) | [rules](../../assets/rules.pdf#page=2) |\n");

        var result = LinkFixer.Fix(_repo);

        var page = ReadPage(Path.Combine("Cards", "Movement.md"));
        Assert.Contains("| ![Duck](../.attachments/from-repo/assets/cards/duck.png) | [rules](../.attachments/from-repo/assets/rules.pdf#page=2) |", page);
        Assert.Equal("duck", File.ReadAllText(Copy("assets/cards/duck.png")));
        Assert.Equal("rules", File.ReadAllText(Copy("assets/rules.pdf")));
        Assert.Equal(2, result.Rewrites.Count);
        Assert.All(result.Copies, c => Assert.Equal(CopyActionKind.Created, c.Kind));
        Assert.Empty(LinkChecker.Check(_repo));
        Assert.Empty(LinkFixer.CheckCopies(_repo));
    }

    [Fact]
    public void Fix_RelinksAnImageAndItsClickThrough()
    {
        WritePage("A.md", "[![Duck](/assets/cards/duck.png)](/assets/cards/duck.png) **Duck**\n");

        LinkFixer.Fix(_repo);

        Assert.Equal(
            "[![Duck](.attachments/from-repo/assets/cards/duck.png)](.attachments/from-repo/assets/cards/duck.png) **Duck**\n",
            ReadPage("A.md"));
    }

    [Fact]
    public void Fix_KeepsLineEndingsAndSkipsCode()
    {
        WritePage("A.md",
            "# A\r\n\r\n![a](../assets/cards/duck.png) and `![b](../assets/cards/duck.png)`\r\n\r\n" +
            "```\r\n![c](../assets/cards/duck.png)\r\n```\r\n");

        LinkFixer.Fix(_repo);

        var page = ReadPage("A.md");
        Assert.Contains("![a](.attachments/from-repo/assets/cards/duck.png) and `![b](../assets/cards/duck.png)`\r\n", page);
        Assert.Contains("```\r\n![c](../assets/cards/duck.png)\r\n```", page);
        Assert.DoesNotContain("\n", page.Replace("\r\n", ""));
    }

    [Fact]
    public void Fix_SameNameInTwoFoldersStaysApart()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n![b](../assets/icons/duck.png)\n");

        LinkFixer.Fix(_repo);

        Assert.Equal("duck", File.ReadAllText(Copy("assets/cards/duck.png")));
        Assert.Equal("icon", File.ReadAllText(Copy("assets/icons/duck.png")));
    }

    [Fact]
    public void Fix_SecondRunChangesNothing()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n");
        LinkFixer.Fix(_repo);
        var before = ReadPage("A.md");

        var again = LinkFixer.Fix(_repo);

        Assert.Empty(again.Rewrites);
        Assert.Empty(again.Copies);
        Assert.Equal(before, ReadPage("A.md"));
    }

    [Fact]
    public void Fix_RefreshesStaleCopies()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n");
        LinkFixer.Fix(_repo);
        WriteAsset("assets/cards/duck.png", "new duck");

        var stale = Assert.Single(LinkFixer.CheckCopies(_repo));
        Assert.Equal(new CopyIssue(".attachments/from-repo/assets/cards/duck.png", CopyIssueKind.Stale), stale);

        var result = LinkFixer.Fix(_repo);

        Assert.Equal(CopyActionKind.Updated, Assert.Single(result.Copies).Kind);
        Assert.Equal("new duck", File.ReadAllText(Copy("assets/cards/duck.png")));
        Assert.Empty(LinkFixer.CheckCopies(_repo));
    }

    [Fact]
    public void Fix_DeletesUnusedCopies()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n");
        LinkFixer.Fix(_repo);
        WritePage("A.md", "no images now\n");

        Assert.Equal(CopyIssueKind.Unused, Assert.Single(LinkFixer.CheckCopies(_repo)).Kind);

        var result = LinkFixer.Fix(_repo);

        Assert.Equal(CopyActionKind.Deleted, Assert.Single(result.Copies).Kind);
        Assert.False(Directory.Exists(Path.Combine(_docs, ".attachments", "from-repo")));
    }

    [Fact]
    public void Fix_KeepsCopyWhenSourceIsGone()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n");
        LinkFixer.Fix(_repo);
        File.Delete(Path.Combine(_project, "assets", "cards", "duck.png"));

        var result = LinkFixer.Fix(_repo);

        Assert.True(File.Exists(Copy("assets/cards/duck.png")));
        Assert.Contains(result.Warnings, w => w.Contains("is gone", StringComparison.Ordinal));
    }

    [Fact]
    public void Fix_DryRunWritesNothing()
    {
        WritePage("A.md", "![a](../assets/cards/duck.png)\n");

        var result = LinkFixer.Fix(_repo, dryRun: true);

        Assert.Single(result.Rewrites);
        Assert.Single(result.Copies);
        Assert.Equal("![a](../assets/cards/duck.png)\n", ReadPage("A.md"));
        Assert.False(Directory.Exists(Path.Combine(_docs, ".attachments")));
    }

    [Fact]
    public void Fix_FileOutsideTheProjectGoesToExternal()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), "wikidown-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(elsewhere);
        try
        {
            var art = Path.Combine(elsewhere, "art.png");
            File.WriteAllText(art, "art");
            WritePage("A.md", $"![a]({new Uri(art).AbsoluteUri})\n");

            var result = LinkFixer.Fix(_repo);

            var copy = Assert.Single(result.Copies).Path;
            Assert.StartsWith(".attachments/from-external/", copy, StringComparison.Ordinal);
            Assert.EndsWith("/art.png", copy, StringComparison.Ordinal);
            Assert.Single(result.Warnings);
            Assert.Empty(LinkChecker.Check(_repo));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public void HtmlExport_ReportsOutsideReferences()
    {
        WritePage("A.md", "# A\n\n![a](../assets/cards/duck.png)\n");

        var result = Wikidown.Html.HtmlExporter.Export(_repo,
            new Wikidown.Html.HtmlExportOptions(Path.Combine(_project, "out")));

        var issue = Assert.Single(result.OutsideWiki);
        Assert.Equal("../assets/cards/duck.png", issue.Target);
    }

    [Fact]
    public void PdfSource_ResolvesLeadingSlashAgainstTheProjectRoot()
    {
        WritePage("A.md", "x\n");
        var source = new RepositoryPdfPageSource(_repo);

        Assert.Equal(
            Path.Combine(_project, "assets", "cards", "duck.png"),
            source.ResolveImage(PagePath.Parse("/A"), "/assets/cards/duck.png"));
    }
}
