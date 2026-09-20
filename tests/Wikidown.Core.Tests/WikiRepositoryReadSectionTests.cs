using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryReadSectionTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;
    private static readonly PagePath Page = PagePath.Parse("/Parent/Child");

    public WikiRepositoryReadSectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root, LineEndings.Lf);
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "# Parent\n"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private string FilePath => Path.Combine(_root, "Parent", "Child.md");

    // With the breadcrumb + blank line injected by Write: "# Child" is
    // line 3, "## History" 7, "## Voice" 11, "### Quirks" 15, "## Notes" 19.
    private const string Body =
        "# Child\n\nIntro.\n\n## History\n\nBorn.\n\n## Voice\n\nWry.\n\n### Quirks\n\nHums.\n\n## Notes\n\n- one\n";

    private void Seed(string body = Body, string ending = "\n")
    {
        _repo.Write(new WikiPage(Page, body));
        if (ending != "\n") File.WriteAllText(FilePath, File.ReadAllText(FilePath).Replace("\n", ending));
    }

    [Fact]
    public void ReadSection_ExactHeading_ReturnsHeadingAndBody()
    {
        Seed();
        var result = _repo.ReadSection(Page, "History");

        Assert.Equal("## History\n\nBorn.\n", result.Markdown);
        Assert.Equal(7, result.StartLine);
        Assert.Equal(9, result.EndLine);
        Assert.Equal(1, result.MatchCount);
        Assert.Null(result.Note);
    }

    [Theory]
    [InlineData("voice")]
    [InlineData("## Voice")]
    [InlineData("  VOICE  ")]
    [InlineData("# voice")]
    public void ReadSection_MatchesCaseInsensitivelyIgnoringHashes(string section)
    {
        Seed();
        Assert.StartsWith("## Voice\n", _repo.ReadSection(Page, section).Markdown);
    }

    [Fact]
    public void ReadSection_IncludesChildHeadings()
    {
        Seed();
        var result = _repo.ReadSection(Page, "Voice");

        Assert.Equal("## Voice\n\nWry.\n\n### Quirks\n\nHums.\n", result.Markdown);
        Assert.Equal(11, result.StartLine);
        Assert.Equal(17, result.EndLine);
    }

    [Fact]
    public void ReadSection_ChildHeading_StopsAtNextHigherLevel()
    {
        Seed();
        Assert.Equal("### Quirks\n\nHums.\n", _repo.ReadSection(Page, "Quirks").Markdown);
    }

    [Fact]
    public void ReadSection_LastSection_RunsToEndOfFile()
    {
        Seed();
        var result = _repo.ReadSection(Page, "Notes");

        Assert.Equal("## Notes\n\n- one\n", result.Markdown);
        Assert.Equal(19, result.StartLine);
        Assert.Equal(21, result.EndLine);
    }

    [Fact]
    public void ReadSection_TopLevelHeading_ReturnsWholeBody()
    {
        Seed();
        Assert.StartsWith("# Child\n\nIntro.\n\n## History", _repo.ReadSection(Page, "Child").Markdown);
        Assert.EndsWith("- one\n", _repo.ReadSection(Page, "Child").Markdown);
    }

    [Fact]
    public void ReadSection_NoMatch_ListsHeadings()
    {
        Seed();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.ReadSection(Page, "Progress"));
        Assert.Equal("no section 'Progress' in /Parent/Child; headings: Child · History · Voice · Quirks · Notes", ex.Message);
    }

    [Fact]
    public void ReadSection_NoHeadingsAtAll_SaysSo()
    {
        Seed("just text\n");
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.ReadSection(Page, "X"));
        Assert.Contains("page has no headings", ex.Message);
    }

    [Fact]
    public void ReadSection_DuplicateHeadings_ReturnsFirstWithNote()
    {
        Seed("# Child\n\n## Notes\n\nfirst\n\n## Notes\n\nsecond\n");
        var result = _repo.ReadSection(Page, "Notes");

        Assert.Equal("## Notes\n\nfirst\n", result.Markdown);
        Assert.Equal(2, result.MatchCount);
        Assert.Equal("note: 2 headings matched; returned the first", result.Note);
    }

    [Fact]
    public void ReadSection_IgnoresHeadingsInsideCodeFences()
    {
        Seed("# Child\n\n## Real\n\n```sh\n## Fake\n```\n\ntail\n");
        Assert.Equal("## Real\n\n```sh\n## Fake\n```\n\ntail\n", _repo.ReadSection(Page, "Real").Markdown);
        Assert.Throws<InvalidOperationException>(() => _repo.ReadSection(Page, "Fake"));
    }

    [Fact]
    public void ReadSection_CrlfFile_ReturnsCrlf()
    {
        Seed(ending: "\r\n");
        var result = _repo.ReadSection(Page, "History");
        Assert.Equal("## History\r\n\r\nBorn.\r\n", result.Markdown);
    }

    [Fact]
    public void ReadSection_MissingPage_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => _repo.ReadSection(PagePath.Parse("/Nope"), "X"));
        Assert.Contains("Page not found: /Nope", ex.Message);
    }

    private (int ExitCode, string Stdout, string Stderr) RunCli(params string[] extra)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = new[] { "read", "--root", _root, "--path", "/Parent/Child" }.Concat(extra).ToArray();
        return (CommandRunner.Run(args, stdout, stderr), stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Cli_Read_Section_PrintsOnlyThatSection()
    {
        Seed();
        var (code, stdout, _) = RunCli("--section", "history");
        Assert.Equal(0, code);
        Assert.Equal("## History\n\nBorn.\n", stdout);
    }

    [Fact]
    public void Cli_Read_WithoutSection_PrintsWholePage()
    {
        Seed();
        var (code, stdout, _) = RunCli();
        Assert.Equal(0, code);
        Assert.Contains("## History", stdout);
        Assert.Contains("## Notes", stdout);
    }

    [Fact]
    public void Cli_Read_SectionMiss_ListsHeadingsOnStderr()
    {
        Seed();
        var (code, stdout, stderr) = RunCli("--section", "Progress");
        Assert.Equal(1, code);
        Assert.Equal("", stdout);
        Assert.Contains("headings: Child · History · Voice · Quirks · Notes", stderr);
    }

    [Fact]
    public void Cli_Read_DuplicateSection_AppendsNote()
    {
        Seed("# Child\n\n## Notes\n\nfirst\n\n## Notes\n\nsecond\n");
        var (_, stdout, _) = RunCli("--section", "Notes");
        Assert.Equal("## Notes\n\nfirst\nnote: 2 headings matched; returned the first",
            stdout.Replace("\r\n", "\n").TrimEnd('\n'));
    }
}
