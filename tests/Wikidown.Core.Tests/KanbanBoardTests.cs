using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class KanbanBoardTests : IDisposable
{
    private static readonly PagePath Board = PagePath.Parse("/Kanban");
    private static readonly PagePath ToDo = PagePath.Parse("/Kanban/To-Do");
    private static readonly PagePath Doing = PagePath.Parse("/Kanban/Doing");

    private readonly string _root;
    private readonly WikiRepository _repo;

    public KanbanBoardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-board-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root, LineEndings.Lf);
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\n- [Kanban](Kanban.md)\n"));
        KanbanBoard.Create(_repo, Board);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private string Read(PagePath page) => _repo.Read(page).Markdown.Replace("\r\n", "\n");

    private string[] Titles(PagePath column) =>
        KanbanBoard.TryLoad(_repo, Board)!.Columns.Single(c => c.Page.ToLinkPath() == column.ToLinkPath())
            .Cards.Select(c => c.Title).ToArray();

    [Fact]
    public void TryParse_ReadsColumnsAndCards()
    {
        var board = KanbanBoard.TryParse(Board,
            "# Kanban\r\n\r\n## [To Do](Kanban/To-Do.md)\r\n\r\n- [Buy paint](Kanban/To-Do/Buy-Paint.md)\r\n\r\n## [Done](Kanban/Done.md)\r\n");

        Assert.NotNull(board);
        Assert.Equal(["To Do", "Done"], board.Columns.Select(c => c.Title));
        var card = Assert.Single(board.Columns[0].Cards);
        Assert.Equal("Buy paint", card.Title);
        Assert.Equal("/Kanban/To-Do/Buy-Paint", card.Page.ToLinkPath());
        Assert.Empty(board.Columns[1].Cards);
    }

    [Theory]
    [InlineData("# Notes\n\n## Plain heading\n\n## Another\n")]
    [InlineData("# One column\n\n## [To Do](Kanban/To-Do.md)\n")]
    [InlineData("# Links elsewhere\n\n## [A](Other/A.md)\n\n## [B](Other/B.md)\n")]
    public void TryParse_OrdinaryPage_IsNotABoard(string markdown) =>
        Assert.Null(KanbanBoard.TryParse(Board, markdown));

    [Fact]
    public void AddCard_CreatesPageAndListsItTwice()
    {
        var card = KanbanBoard.AddCard(_repo, Board, ToDo, "Buy paint");

        Assert.Equal("/Kanban/To-Do/Buy-paint", card.ToLinkPath());
        Assert.Contains("# Buy paint", Read(card));
        Assert.Contains("## [To Do](Kanban/To-Do.md)\n\n- [Buy paint](Kanban/To-Do/Buy-paint.md)\n\n## [Doing]", Read(Board));
        Assert.EndsWith("# To Do\n\n- [Buy paint](To-Do/Buy-paint.md)\n", Read(ToDo));
        Assert.Empty(LinkChecker.Check(_repo));
        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void AddCard_AppendsAfterExistingCards()
    {
        KanbanBoard.AddCard(_repo, Board, ToDo, "One");
        KanbanBoard.AddCard(_repo, Board, ToDo, "Two");

        Assert.Equal(["One", "Two"], Titles(ToDo));
        Assert.Throws<InvalidOperationException>(() => KanbanBoard.AddCard(_repo, Board, ToDo, "Two"));
    }

    [Fact]
    public void MoveCard_ToAnotherColumn_MovesPageAndBothLines()
    {
        KanbanBoard.AddCard(_repo, Board, ToDo, "One");
        var two = KanbanBoard.AddCard(_repo, Board, ToDo, "Two");
        KanbanBoard.AddCard(_repo, Board, Doing, "Busy");

        var moved = KanbanBoard.MoveCard(_repo, Board, two, Doing, 0);

        Assert.Equal("/Kanban/Doing/Two", moved.ToLinkPath());
        Assert.False(_repo.Exists(two));
        Assert.Equal(["One"], Titles(ToDo));
        Assert.Equal(["Two", "Busy"], Titles(Doing));
        Assert.DoesNotContain("Two", Read(ToDo));
        Assert.Contains("- [Two](Doing/Two.md)\n- [Busy](Doing/Busy.md)", Read(Doing));
        Assert.Contains("[Doing](../Doing.md) / Two", Read(moved));
        Assert.Empty(LinkChecker.Check(_repo));
        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void MoveCard_IntoEmptyColumn_KeepsBlankLinesAroundTheItem()
    {
        var card = KanbanBoard.AddCard(_repo, Board, ToDo, "One");

        KanbanBoard.MoveCard(_repo, Board, card, Doing, 0);

        Assert.Contains(
            "## [To Do](Kanban/To-Do.md)\n\n## [Doing](Kanban/Doing.md)\n\n- [One](Kanban/Doing/One.md)\n\n## [Done](Kanban/Done.md)\n",
            Read(Board));
    }

    [Fact]
    public void MoveCard_WithinAColumn_Reorders()
    {
        var a = KanbanBoard.AddCard(_repo, Board, ToDo, "A");
        KanbanBoard.AddCard(_repo, Board, ToDo, "B");
        KanbanBoard.AddCard(_repo, Board, ToDo, "C");

        var moved = KanbanBoard.MoveCard(_repo, Board, a, ToDo, 2);

        Assert.Equal(a.ToLinkPath(), moved.ToLinkPath());
        Assert.Equal(["B", "C", "A"], Titles(ToDo));
        Assert.Contains("- [B](To-Do/B.md)\n- [C](To-Do/C.md)\n- [A](To-Do/A.md)", Read(ToDo));
    }

    [Fact]
    public void RemoveCard_DeletesPageAndLines()
    {
        var card = KanbanBoard.AddCard(_repo, Board, ToDo, "Gone");

        KanbanBoard.RemoveCard(_repo, Board, card);

        Assert.False(_repo.Exists(card));
        Assert.DoesNotContain("Gone", Read(Board));
        Assert.DoesNotContain("Gone", Read(ToDo));
        Assert.Empty(LinkChecker.Check(_repo));
    }

    [Fact]
    public void AddColumn_AddsPageAndSection()
    {
        var column = KanbanBoard.AddColumn(_repo, Board, "Waiting");

        Assert.Equal("/Kanban/Waiting", column.ToLinkPath());
        Assert.EndsWith("## [Done](Kanban/Done.md)\n\n## [Waiting](Kanban/Waiting.md)\n", Read(Board));
        Assert.Equal(4, KanbanBoard.TryLoad(_repo, Board)!.Columns.Count);
    }

    [Fact]
    public void Move_RenameInPlace_KeepsItsPositionInOrder()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Last"), "# Last\n"));

        _repo.Move(Board, PagePath.Parse("/Board"));

        Assert.Equal(["Home", "Board", "Last"], _repo.ReadOrder(PagePath.Root));
    }

    [Fact]
    public void Move_FolderWithoutAPage_TakesItsSubpages()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Loose/Child"), "# Child\n"));

        MoveLinkRewriter.MoveAndRewrite(_repo, PagePath.Parse("/Loose"), PagePath.Parse("/Tidy"));

        Assert.True(_repo.Exists(PagePath.Parse("/Tidy/Child")));
        Assert.False(Directory.Exists(Path.Combine(_root, "Loose")));
        Assert.False(File.Exists(Path.Combine(_root, "Tidy.md")));
        Assert.DoesNotContain("Tidy", _repo.ReadOrder(PagePath.Root));
    }
}
