using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryAppendTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;
    private static readonly PagePath Page = PagePath.Parse("/Parent/Child");

    public WikiRepositoryAppendTests()
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

    // With the breadcrumb + blank line injected by Write: "# Child" is
    // line 3, "## History" 7, "Born." 9, "## Voice" 11, "### Quirks" 15,
    // "Hums." 17, "## Notes" 19, "- one" 21 (last line).
    private const string Body =
        "# Child\n\nIntro.\n\n## History\n\nBorn.\n\n## Voice\n\nWry.\n\n### Quirks\n\nHums.\n\n## Notes\n\n- one\n";

    private void Seed(string body = Body, string ending = "\n")
    {
        _repo.Write(new WikiPage(Page, body));
        if (ending != "\n") File.WriteAllText(FilePath, ReadRaw().Replace("\n", ending));
    }

    [Fact]
    public void Append_ToEnd_SeparatedByOneBlankLine()
    {
        Seed();
        var result = _repo.Append(Page, "- two\n- three\n");

        Assert.EndsWith("## Notes\n\n- one\n\n- two\n- three\n", ReadRaw());
        Assert.Equal("appended 2 lines to /Parent/Child (now lines 23–24)", result.Summary);
        Assert.Equal(23, result.StartLine);
        Assert.Equal(24, result.EndLine);
    }

    [Fact]
    public void Append_ToEnd_TrimsExistingTrailingBlankLines_AndInputEdges()
    {
        Seed();
        File.WriteAllText(FilePath, ReadRaw() + "\n\n\n");
        _repo.Append(Page, "\n\nTail.\n\n\n");

        Assert.EndsWith("- one\n\nTail.\n", ReadRaw());
    }

    [Fact]
    public void Append_ToEnd_FileWithoutTrailingNewline()
    {
        Seed();
        File.WriteAllText(FilePath, ReadRaw().TrimEnd('\n'));
        _repo.Append(Page, "Tail.");

        Assert.EndsWith("- one\n\nTail.\n", ReadRaw());
    }

    [Fact]
    public void Append_AfterMiddleSection_InsertsBeforeNextHeading()
    {
        Seed();
        var result = _repo.Append(Page, "Died.", afterSection: "history");

        Assert.Contains("## History\n\nBorn.\n\nDied.\n\n## Voice", ReadRaw());
        Assert.Equal("appended 1 line to /Parent/Child after section 'History' (now line 11)", result.Summary);
    }

    [Fact]
    public void Append_AfterSectionWithChildren_GoesAfterTheChildren()
    {
        Seed();
        _repo.Append(Page, "Added.", afterSection: "## Voice");

        Assert.Contains("Hums.\n\nAdded.\n\n## Notes", ReadRaw());
    }

    [Fact]
    public void Append_AfterLastSection_RunsToEndOfFile()
    {
        Seed();
        var result = _repo.Append(Page, "- two", afterSection: "Notes");

        Assert.EndsWith("## Notes\n\n- one\n\n- two\n", ReadRaw());
        Assert.False(ReadRaw().EndsWith("\n\n"));
        Assert.Equal("appended 1 line to /Parent/Child after section 'Notes' (now line 23)", result.Summary);
    }

    [Fact]
    public void Append_AfterEmptySection_Works()
    {
        Seed("# Child\n\n## Empty\n\n## Next\n\ntext\n");
        _repo.Append(Page, "filled", afterSection: "Empty");

        Assert.Contains("## Empty\n\nfilled\n\n## Next", ReadRaw());
    }

    [Fact]
    public void Append_Repeated_DoesNotStackBlankLines()
    {
        Seed();
        _repo.Append(Page, "\n- a\n\n", afterSection: "History");
        _repo.Append(Page, "\n- b\n\n", afterSection: "History");
        _repo.Append(Page, "\n- c\n\n");
        _repo.Append(Page, "\n- d\n\n");

        Assert.Contains("## History\n\nBorn.\n\n- a\n\n- b\n\n## Voice", ReadRaw());
        Assert.EndsWith("- one\n\n- c\n\n- d\n", ReadRaw());
        Assert.DoesNotContain("\n\n\n", ReadRaw());
    }

    [Fact]
    public void Append_HeadingNotFound_ListsHeadings()
    {
        Seed();
        var before = ReadRaw();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Append(Page, "x", afterSection: "Progress"));
        Assert.Equal("no section 'Progress' in /Parent/Child; headings: Child · History · Voice · Quirks · Notes", ex.Message);
        Assert.Equal(before, ReadRaw());
    }

    [Fact]
    public void Append_DuplicateHeadings_Refused()
    {
        Seed("# Child\n\n## Notes\n\nfirst\n\n## Notes\n\nsecond\n");
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Append(Page, "x", afterSection: "Notes"));
        Assert.StartsWith("2 headings match 'Notes' in /Parent/Child", ex.Message);
    }

    [Fact]
    public void Append_EmptyMarkdown_Throws()
    {
        Seed();
        Assert.Throws<ArgumentException>(() => _repo.Append(Page, "\n\n"));
    }

    [Fact]
    public void Append_CrlfFile_KeepsCrlf()
    {
        Seed(ending: "\r\n");
        _repo.Append(Page, "Line A\nLine B", afterSection: "History");

        var after = ReadRaw();
        Assert.Contains("Born.\r\n\r\nLine A\r\nLine B\r\n\r\n## Voice", after);
        Assert.Equal(after.Count(c => c == '\n'), after.Count(c => c == '\r'));
    }

    [Fact]
    public void Append_BreadcrumbAndOrderUntouched()
    {
        Seed();
        var crumb = ReadRaw().Split('\n')[0];
        var orderPath = Path.Combine(_root, "Parent", ".order");
        File.WriteAllText(orderPath, "Zed\nChild\n");

        _repo.Append(Page, "x");

        Assert.StartsWith(crumb + "\n\n# Child", ReadRaw());
        Assert.Equal("Zed\nChild\n", File.ReadAllText(orderPath));
    }

    [Fact]
    public void Append_MissingPage_Throws_AndDoesNotCreate()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => _repo.Append(PagePath.Parse("/Nope"), "x"));
        Assert.Contains("Page not found: /Nope", ex.Message);
        Assert.Contains("wikidown new", ex.Message);
        Assert.False(File.Exists(Path.Combine(_root, "Nope.md")));
    }

    private (int ExitCode, string Stdout, string Stderr) RunCli(params string[] extra)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = new[] { "append", "--root", _root, "--path", "/Parent/Child" }.Concat(extra).ToArray();
        return (CommandRunner.Run(args, stdout, stderr), stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Cli_Append_ToEnd_FromFile()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "block.md");
        File.WriteAllText(bodyFile, "- two\n");
        var (code, stdout, _) = RunCli("--file", bodyFile);

        Assert.Equal(0, code);
        Assert.Contains("appended 1 line to /Parent/Child (now line 23)", stdout);
        Assert.EndsWith("- one\n\n- two\n", ReadRaw());
    }

    [Fact]
    public void Cli_Append_AfterSection()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "block.md");
        File.WriteAllText(bodyFile, "Died.\n");
        var (code, stdout, _) = RunCli("--after", "History", "--file", bodyFile);

        Assert.Equal(0, code);
        Assert.Contains("after section 'History' (now line 11)", stdout);
        Assert.Contains("Born.\n\nDied.\n\n## Voice", ReadRaw());
    }

    [Fact]
    public void Cli_Append_Miss_ListsHeadings()
    {
        Seed();
        var bodyFile = Path.Combine(_root, "block.md");
        File.WriteAllText(bodyFile, "x");
        var (code, _, stderr) = RunCli("--after", "Nope", "--file", bodyFile);

        Assert.Equal(1, code);
        Assert.Contains("headings: Child · History · Voice · Quirks · Notes", stderr);
    }

    [Fact]
    public void Cli_Append_MissingBody_IsUsageError()
    {
        Seed();
        var (code, _, stderr) = RunCli();
        Assert.Equal(2, code);
        Assert.Contains("--file", stderr);
    }

    [Fact]
    public void Cli_Append_Help()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CommandRunner.Run(new[] { "append", "--help" }, stdout, TextWriter.Null));
        Assert.Contains("wikidown append --path", stdout.ToString());
    }
}
