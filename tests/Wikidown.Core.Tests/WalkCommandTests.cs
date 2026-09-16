using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

public class WalkCommandTests : IDisposable
{
    private readonly string _root;

    public WalkCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-walk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private string Run(params string[] extra)
    {
        var stdout = new StringWriter();
        var code = CommandRunner.Run(new[] { "walk", "--root", _root }.Concat(extra).ToArray(), stdout, TextWriter.Null);
        Assert.Equal(0, code);
        return stdout.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void Walk_ListsEveryPageDepthFirstInOrder()
    {
        var repo = new WikiRepository(_root);
        repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Guide"), "# Guide\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Guide/Second"), "# Second\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Guide/First"), "# First\n"));
        File.WriteAllText(Path.Combine(_root, "Guide", ".order"), "First" + "\n" + "Second" + "\n");

        Assert.Equal("/Home\tHome\n/Guide\tGuide\n/Guide/First\tFirst\n/Guide/Second\tSecond\n", Run());
    }

    [Fact]
    public void Walk_WithPath_ListsOnlyDescendants()
    {
        var repo = new WikiRepository(_root);
        repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Guide"), "# Guide\n"));
        repo.Write(new WikiPage(PagePath.Parse("/Guide/Child"), "# Child\n"));

        Assert.Equal("/Guide/Child\tChild\n", Run("--path", "/Guide"));
    }

    [Fact]
    public void Walk_OnEmptyWiki_SaysSo()
    {
        Assert.Equal("(empty wiki)\n", Run());
    }
}
