using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

public class KanbanInitCommandTests : IDisposable
{
    private readonly string _root;

    public KanbanInitCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-kanban-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(params string[] extra) =>
        CommandRunner.Run(new[] { "kanban", "init", "--root", _root }.Concat(extra).ToArray(),
            TextWriter.Null, TextWriter.Null);

    [Fact]
    public void Init_CreatesDashboardAndThreeSectionsInOrder()
    {
        Assert.Equal(0, Run());

        var repo = new WikiRepository(_root);
        var sections = repo.ListChildren(PagePath.Parse("/Kanban")).Select(p => p.ToLinkPath());
        Assert.Equal(["/Kanban/To-Do", "/Kanban/Doing", "/Kanban/Done"], sections);

        var dashboard = repo.Read(PagePath.Parse("/Kanban")).Markdown.Replace("\r\n", "\n");
        Assert.Equal(
            "# Kanban\n\n## [To Do](Kanban/To-Do.md)\n\n## [Doing](Kanban/Doing.md)\n\n## [Done](Kanban/Done.md)\n",
            dashboard);
        Assert.Contains("\n# To Do", repo.Read(PagePath.Parse("/Kanban/To-Do")).Markdown);
        Assert.Empty(LinkChecker.Check(repo));
    }

    [Fact]
    public void Init_WithPath_NestsTheKanban()
    {
        new WikiRepository(_root).Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\n- [Garden](Garden.md)\n"));
        new WikiRepository(_root).Write(new WikiPage(PagePath.Parse("/Garden"), "# Garden\n\n- [Chores](Garden/Chores.md)\n"));

        Assert.Equal(0, Run("--path", "Garden/Chores"));

        var repo = new WikiRepository(_root);
        Assert.True(repo.Exists(PagePath.Parse("/Garden/Chores/Done")));
        Assert.Contains("## [Doing](Chores/Doing.md)", repo.Read(PagePath.Parse("/Garden/Chores")).Markdown);
        Assert.Empty(LinkChecker.Check(repo));
    }

    [Fact]
    public void Append_AfterSectionName_MatchesTheLinkedHeading()
    {
        Assert.Equal(0, Run());
        var repo = new WikiRepository(_root);

        repo.Append(PagePath.Parse("/Kanban"), "- [Buy paint](Kanban/To-Do/Buy-Paint.md)\n", afterSection: "to do");

        var dashboard = repo.Read(PagePath.Parse("/Kanban")).Markdown.Replace("\r\n", "\n");
        Assert.Contains("## [To Do](Kanban/To-Do.md)\n\n- [Buy paint](Kanban/To-Do/Buy-Paint.md)\n\n## [Doing]", dashboard);
    }

    [Fact]
    public void Init_Twice_IsAUsageError()
    {
        Assert.Equal(0, Run());
        Assert.Equal(2, Run());
    }

    [Fact]
    public void Kanban_WithoutSubcommand_IsAUsageError()
    {
        Assert.Equal(2, CommandRunner.Run(["kanban", "--root", _root], TextWriter.Null, TextWriter.Null));
    }
}
