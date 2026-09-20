using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class WikiRepositoryEditTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;
    private static readonly PagePath Page = PagePath.Parse("/Parent/Child");

    public WikiRepositoryEditTests()
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

    private void WriteRaw(string content) => File.WriteAllText(FilePath, content);
    private string ReadRaw() => File.ReadAllText(FilePath);

    // With the breadcrumb + blank line injected by Write, "# Child" is
    // line 3, "## Voice" line 5, "Wry, exhausted." line 7, "## Notes"
    // line 9, "* one" line 11, "* two" line 12. List items use "*" so a
    // "* " needle can't collide with the breadcrumb's "<!-- ".
    private const string Body =
        "# Child\n\n## Voice\n\nWry, exhausted.\n\n## Notes\n\n* one\n* two\n";

    private void Seed(string ending = "\n")
    {
        _repo.Write(new WikiPage(Page, Body));
        if (ending != "\n") WriteRaw(ReadRaw().Replace("\n", ending));
    }

    [Fact]
    public void Edit_ReplacesSingleMatch_AndReportsLineWithContext()
    {
        Seed();
        var result = _repo.Edit(Page, "Wry, exhausted.", "Wry, exhausted, unstoppable.");

        Assert.Equal(1, result.Replacements);
        Assert.Contains("Wry, exhausted, unstoppable.", ReadRaw());
        Assert.StartsWith("edited /Parent/Child (1 replacement, line 7)", result.Summary);
        Assert.Contains("> 7 | Wry, exhausted, unstoppable.", result.Summary);
        Assert.Contains("  5 | ## Voice", result.Summary);
        Assert.Contains("  9 | ## Notes", result.Summary);
    }

    [Fact]
    public void Edit_NoMatch_ThrowsAndLeavesFileUntouched()
    {
        Seed();
        var before = ReadRaw();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Edit(Page, "nope", "x"));
        Assert.Contains("no match for old text in /Parent/Child", ex.Message);
        Assert.Equal(before, ReadRaw());
    }

    [Fact]
    public void Edit_MultipleMatches_ThrowsWithCount()
    {
        Seed();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Edit(Page, "* ", "- "));
        Assert.Contains("matches 2 times", ex.Message);
        Assert.Contains("replaceAll", ex.Message);
        Assert.Contains("* one", ReadRaw());
    }

    [Fact]
    public void Edit_ReplaceAll_ReplacesEveryOccurrence()
    {
        Seed();
        var result = _repo.Edit(Page, "* ", "- ", replaceAll: true);

        Assert.Equal(2, result.Replacements);
        Assert.Contains("- one\n- two\n", ReadRaw());
        Assert.StartsWith("edited /Parent/Child (2 replacements, lines 11, 12)", result.Summary);
        Assert.Contains("> 11 | - one", result.Summary);
        Assert.Contains("> 12 | - two", result.Summary);
    }

    [Fact]
    public void Edit_OldEqualsNew_Throws()
    {
        Seed();
        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Edit(Page, "* one", "* one"));
        Assert.Contains("identical", ex.Message);
    }

    [Fact]
    public void Edit_EmptyOld_Throws()
    {
        Seed();
        Assert.Throws<ArgumentException>(() => _repo.Edit(Page, "", "x"));
    }

    [Fact]
    public void Edit_CrlfFile_MatchesLfInput_AndKeepsCrlf()
    {
        Seed("\r\n");
        var result = _repo.Edit(Page, "* one\n* two", "* one\n* two\n* three");

        var after = ReadRaw();
        Assert.Contains("* one\r\n* two\r\n* three\r\n", after);
        Assert.Equal(after.Count(c => c == '\n'), after.Count(c => c == '\r'));
        Assert.Equal(1, result.Replacements);
        Assert.StartsWith("edited /Parent/Child (1 replacement, lines 11–13)", result.Summary);
    }

    [Fact]
    public void Edit_LfFile_MatchesCrlfInput_AndKeepsLf()
    {
        Seed();
        _repo.Edit(Page, "* one\r\n* two", "* uno\r\n* dos");

        var after = ReadRaw();
        Assert.DoesNotContain("\r", after);
        Assert.Contains("* uno\n* dos\n", after);
    }

    [Fact]
    public void Edit_OldSpanningLines_Works()
    {
        Seed();
        _repo.Edit(Page, "## Voice\n\nWry, exhausted.", "## Voice\n\nCalm.");
        Assert.Contains("## Voice\n\nCalm.\n\n## Notes", ReadRaw());
    }

    [Fact]
    public void Edit_DeletionWithEmptyNew_Works()
    {
        Seed();
        _repo.Edit(Page, "* two\n", "");
        Assert.EndsWith("* one\n", ReadRaw());
    }

    [Fact]
    public void Edit_BreadcrumbOverlap_Refused()
    {
        Seed();
        var crumb = ReadRaw().Split('\n')[0];
        Assert.EndsWith(Breadcrumb.Marker, crumb);

        var ex = Assert.Throws<InvalidOperationException>(() => _repo.Edit(Page, "[Home]", "[Start]"));
        Assert.Contains("breadcrumb is managed by Wikidown", ex.Message);
        Assert.StartsWith(crumb + "\n", ReadRaw());
    }

    [Fact]
    public void Edit_BreadcrumbUnaffected_ByBodyEdit()
    {
        Seed();
        var crumb = ReadRaw().Split('\n')[0];
        _repo.Edit(Page, "# Child", "# Child Page");
        Assert.StartsWith(crumb + "\n\n# Child Page\n", ReadRaw());
    }

    [Fact]
    public void Edit_NonExistentPage_Throws_AndDoesNotCreate()
    {
        Seed();
        var missing = PagePath.Parse("/Parent/Missing");
        var ex = Assert.Throws<FileNotFoundException>(() => _repo.Edit(missing, "a", "b"));
        Assert.Contains("Page not found: /Parent/Missing", ex.Message);
        Assert.Contains("wikidown new", ex.Message);
        Assert.False(File.Exists(Path.Combine(_root, "Parent", "Missing.md")));
        Assert.DoesNotContain("Missing", File.ReadAllText(Path.Combine(_root, "Parent", ".order")));
    }

    [Fact]
    public void Edit_DoesNotTouchOrder()
    {
        Seed();
        var orderPath = Path.Combine(_root, "Parent", ".order");
        File.WriteAllText(orderPath, "Zed\nChild\n");
        _repo.Edit(Page, "* one", "* 1");
        Assert.Equal("Zed\nChild\n", File.ReadAllText(orderPath));
    }

    [Fact]
    public void Edit_DoesNotAddTrailingNewline()
    {
        Seed();
        WriteRaw(ReadRaw().TrimEnd('\n'));
        _repo.Edit(Page, "* two", "* 2");
        Assert.EndsWith("* 2", ReadRaw());
    }

    private (int ExitCode, string Stdout, string Stderr) RunCli(params string[] extra)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = new[] { "edit", "--root", _root, "--path", "/Parent/Child" }.Concat(extra).ToArray();
        return (CommandRunner.Run(args, stdout, stderr), stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Cli_Edit_InlineValues()
    {
        Seed();
        var (code, stdout, _) = RunCli("--old", "* one", "--new", "* uno");
        Assert.Equal(0, code);
        Assert.Contains("edited /Parent/Child (1 replacement, line 11)", stdout);
        Assert.Contains("* uno", ReadRaw());
    }

    [Fact]
    public void Cli_Edit_FileValues_AndAll()
    {
        Seed();
        var oldFile = Path.Combine(_root, "old.txt");
        var newFile = Path.Combine(_root, "new.txt");
        File.WriteAllText(oldFile, "* ");
        File.WriteAllText(newFile, "- ");
        var (code, _, _) = RunCli("--old-file", oldFile, "--new-file", newFile, "--all");
        Assert.Equal(0, code);
        Assert.Contains("- one\n- two", ReadRaw());
    }

    [Fact]
    public void Cli_Edit_MissingValues_IsUsageError()
    {
        Seed();
        var (code, _, stderr) = RunCli("--old", "* one");
        Assert.Equal(2, code);
        Assert.Contains("--new", stderr);
    }

    [Fact]
    public void Cli_Edit_Delete_RemovesTheMatch_WithoutAnEmptyStringArgument()
    {
        Seed();
        var (code, _, _) = RunCli("--old", ", exhausted", "--delete");
        Assert.Equal(0, code);
        Assert.Contains("Wry.\n", ReadRaw());
    }

    [Fact]
    public void Cli_Edit_DeleteWithReplacement_IsUsageError()
    {
        Seed();
        var (code, _, stderr) = RunCli("--old", "* one", "--new", "x", "--delete");
        Assert.Equal(2, code);
        Assert.Contains("--delete", stderr);
    }

    [Fact]
    public void Cli_Edit_NoMatch_ExitsOne()
    {
        Seed();
        var (code, _, stderr) = RunCli("--old", "zzz", "--new", "y");
        Assert.Equal(1, code);
        Assert.Contains("no match", stderr);
    }

    [Fact]
    public void Cli_Edit_Help()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CommandRunner.Run(new[] { "edit", "--help" }, stdout, TextWriter.Null));
        Assert.Contains("wikidown edit --path", stdout.ToString());
    }
}
