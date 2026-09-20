using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryWriteSectionTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;
    private static readonly PagePath Page = PagePath.Parse("/Parent/Child");

    public WikiRepositoryWriteSectionTests()
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
    private string ReadRaw() => File.ReadAllText(FilePath);
    private string Crumb => ReadRaw().Split('\n')[0].TrimEnd('\r');

    // With the breadcrumb + blank line injected by Write: "# Child" is
    // line 3, "## History" 7, "## Voice" 11, "### Quirks" 15, "## Notes" 19.
    private const string Body =
        "# Child\n\nIntro.\n\n## History\n\nBorn.\n\n## Voice\n\nWry.\n\n### Quirks\n\nHums.\n\n## Notes\n\n- one\n";

    private void Seed(string body = Body, string ending = "\n")
    {
        _repo.Write(new WikiPage(Page, body));
        if (ending != "\n") File.WriteAllText(FilePath, ReadRaw().Replace("\n", ending));
    }

    private string Expect(string afterCrumb) => Crumb + "\n\n" + afterCrumb;

    [Fact]
    public void WriteSection_MiddleSection_ReplacesBodyOnly()
    {
        Seed();
        var result = _repo.WriteSection(Page, "History", "Born in 1820.\nRaised well.\n");

        Assert.Equal(Expect("# Child\n\nIntro.\n\n## History\n\nBorn in 1820.\nRaised well.\n\n## Voice\n\nWry.\n\n### Quirks\n\nHums.\n\n## Notes\n\n- one\n"), ReadRaw());
        Assert.Equal("wrote section 'History' in /Parent/Child (replaced lines 8–10 with 2 lines)", result.Summary);
    }

    [Fact]
    public void WriteSection_LastSection_RunsToEndOfFile_SingleTrailingNewline()
    {
        Seed();
        _repo.WriteSection(Page, "notes", "- uno\n- dos");

        Assert.EndsWith("## Notes\n\n- uno\n- dos\n", ReadRaw());
        Assert.False(ReadRaw().EndsWith("\n\n"));
    }

    [Fact]
    public void WriteSection_ReplacesChildHeadingsToo()
    {
        Seed();
        var result = _repo.WriteSection(Page, "## Voice", "Calm.");

        Assert.Contains("## Voice\n\nCalm.\n\n## Notes", ReadRaw());
        Assert.DoesNotContain("Quirks", ReadRaw());
        Assert.Equal("wrote section 'Voice' in /Parent/Child (replaced lines 12–18 with 1 lines)", result.Summary);
    }

    [Fact]
    public void WriteSection_KeepsHeadingLineVerbatim()
    {
        Seed("# Child\n\n##   Odd Spacing ##\n\nold\n");
        _repo.WriteSection(Page, "odd spacing", "new");
        Assert.Contains("##   Odd Spacing ##\n\nnew\n", ReadRaw());
    }

    [Fact]
    public void WriteSection_MissingSection_ThrowsListingHeadings()
    {
        Seed();
        var before = ReadRaw();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.WriteSection(Page, "Progress", "x"));
        Assert.Equal("no section 'Progress' in /Parent/Child; headings: Child · History · Voice · Quirks · Notes", ex.Message);
        Assert.Equal(before, ReadRaw());
    }

    [Fact]
    public void WriteSection_CreateIfMissing_AppendsH2AtEnd()
    {
        Seed();
        var result = _repo.WriteSection(Page, "### Progress", "Not yet.\n", createIfMissing: true);

        Assert.EndsWith("## Notes\n\n- one\n\n## Progress\n\nNot yet.\n", ReadRaw());
        Assert.True(result.Created);
        Assert.Equal("created section 'Progress' in /Parent/Child (lines 23–25)", result.Summary);
    }

    [Fact]
    public void WriteSection_DuplicateHeadings_Refused()
    {
        Seed("# Child\n\n## Notes\n\nfirst\n\n## Notes\n\nsecond\n");
        var before = ReadRaw();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.WriteSection(Page, "Notes", "x"));
        Assert.Equal("2 headings match 'Notes' in /Parent/Child; pass more of the heading text (or its #s) to pick one", ex.Message);
        Assert.Equal(before, ReadRaw());
    }

    [Fact]
    public void WriteSection_RepeatedWrites_DoNotStackBlankLines()
    {
        Seed();
        _repo.WriteSection(Page, "History", "\n\npass one\n\n\n");
        var afterFirst = ReadRaw();
        _repo.WriteSection(Page, "History", "\n\npass two\n\n\n");
        var afterSecond = ReadRaw();

        Assert.Contains("## History\n\npass one\n\n## Voice", afterFirst);
        Assert.Contains("## History\n\npass two\n\n## Voice", afterSecond);
        Assert.Equal(afterFirst.Replace("pass one", "pass two"), afterSecond);
    }

    [Fact]
    public void WriteSection_EmptyBody_LeavesOneBlankLineBeforeNextHeading()
    {
        Seed();
        _repo.WriteSection(Page, "History", "");
        Assert.Contains("## History\n\n## Voice", ReadRaw());
    }

    [Fact]
    public void WriteSection_CrlfFile_KeepsCrlf()
    {
        Seed(ending: "\r\n");
        _repo.WriteSection(Page, "History", "Line A\nLine B");

        var after = ReadRaw();
        Assert.Contains("## History\r\n\r\nLine A\r\nLine B\r\n\r\n## Voice", after);
        Assert.Equal(after.Count(c => c == '\n'), after.Count(c => c == '\r'));
    }

    [Fact]
    public void WriteSection_BreadcrumbAndOrderUntouched()
    {
        Seed();
        var crumb = Crumb;
        var orderPath = Path.Combine(_root, "Parent", ".order");
        File.WriteAllText(orderPath, "Zed\nChild\n");

        _repo.WriteSection(Page, "History", "x");

        Assert.StartsWith(crumb + "\n\n# Child", ReadRaw());
        Assert.Equal("Zed\nChild\n", File.ReadAllText(orderPath));
    }

    [Fact]
    public void WriteSection_MissingPage_Throws_EvenWithCreate()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            _repo.WriteSection(PagePath.Parse("/Nope"), "X", "y", createIfMissing: true));
        Assert.Contains("Page not found: /Nope", ex.Message);
        Assert.False(File.Exists(Path.Combine(_root, "Nope.md")));
    }

    private (int ExitCode, string Stdout, string Stderr) RunCli(params string[] extra)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = new[] { "write-section", "--root", _root, "--path", "/Parent/Child" }.Concat(extra).ToArray();
        return (CommandRunner.Run(args, stdout, stderr), stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Cli_WriteSection_FromFile()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "body.md");
        File.WriteAllText(bodyFile, "From file.\n");
        var (code, stdout, _) = RunCli("--section", "History", "--file", bodyFile);

        Assert.Equal(0, code);
        Assert.Contains("wrote section 'History' in /Parent/Child (replaced lines 8–10 with 1 lines)", stdout);
        Assert.Contains("## History\n\nFrom file.\n\n## Voice", ReadRaw());
    }

    [Fact]
    public void Cli_WriteSection_Create()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "body.md");
        File.WriteAllText(bodyFile, "New.\n");
        var (code, stdout, _) = RunCli("--section", "Extra", "--file", bodyFile, "--create");

        Assert.Equal(0, code);
        Assert.Contains("created section 'Extra'", stdout);
        Assert.EndsWith("## Extra\n\nNew.\n", ReadRaw());
    }

    [Fact]
    public void Cli_WriteSection_Miss_ListsHeadings()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "body.md");
        File.WriteAllText(bodyFile, "x");
        var (code, _, stderr) = RunCli("--section", "Nope", "--file", bodyFile);

        Assert.Equal(1, code);
        Assert.Contains("headings: Child · History · Voice · Quirks · Notes", stderr);
    }

    [Fact]
    public void Cli_WriteSection_MissingBody_IsUsageError()
    {
        Seed();
        var (code, _, stderr) = RunCli("--section", "History");
        Assert.Equal(2, code);
        Assert.Contains("--file", stderr);
    }

    [Fact]
    public void Cli_WriteSection_Help()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CommandRunner.Run(new[] { "write-section", "--help" }, stdout, TextWriter.Null));
        Assert.Contains("wikidown write-section --path", stdout.ToString());
    }
}
