using System.Text.RegularExpressions;

namespace Wikidown.Core;

public sealed record KanbanCard(string Title, PagePath Page);

public sealed record KanbanColumn(string Title, PagePath Page, IReadOnlyList<KanbanCard> Cards);

// A kanban is plain wiki pages (see docs/Getting-Started/Kanban.md): a
// dashboard whose "## [Column](Board/Column.md)" headings each link to a
// direct sub-page, with one "- [Card](...)" line per item under the heading
// and again on the column's own page. Everything here keeps those three
// places in step: the card's page, the dashboard line and the column line.
public sealed partial record KanbanBoard(PagePath Dashboard, IReadOnlyList<KanbanColumn> Columns)
{
    private static readonly string[] DefaultColumns = ["To Do", "Doing", "Done"];

    [GeneratedRegex(@"^\[(?<text>[^\]]+)\]\((?<href>[^)\s]+)\)$")]
    private static partial Regex HeadingLink();

    [GeneratedRegex(@"^\s*[-*+]\s+\[(?<text>[^\]]+)\]\((?<href>[^)\s]+)\)\s*$")]
    private static partial Regex CardLine();

    /// <summary>The board a page describes, or null when the page is not a kanban dashboard.</summary>
    public static KanbanBoard? TryParse(PagePath dashboard, string markdown)
    {
        if (dashboard.IsRoot) return null;
        var lines = LineEndings.ToLf(markdown).Split('\n');
        var columns = Sections(dashboard, lines)
            .Select(s => new KanbanColumn(s.Title, s.Page,
                s.Cards.Select(c => new KanbanCard(c.Title, c.Page)).ToList()))
            .ToList();
        return columns.Count >= 2 ? new KanbanBoard(dashboard, columns) : null;
    }

    public static KanbanBoard? TryLoad(WikiRepository repo, PagePath dashboard) =>
        repo.Exists(dashboard) ? TryParse(dashboard, repo.Read(dashboard).Markdown) : null;

    /// <summary>Creates a dashboard page with one sub-page and one section per column.</summary>
    public static IReadOnlyList<PagePath> Create(WikiRepository repo, PagePath board, IEnumerable<string>? columns = null)
    {
        var sections = (columns ?? DefaultColumns).Select(s => board.Append(PageName.FromTitle(s))).ToList();
        foreach (var page in sections.Prepend(board))
        {
            if (repo.Exists(page))
                throw new InvalidOperationException($"page already exists: {page.ToLinkPath()}");
        }

        var dashboard = new System.Text.StringBuilder($"# {board.Name.Title}\n");
        foreach (var section in sections)
            dashboard.Append($"\n## [{section.Name.Title}]({board.Name.FileBase}/{section.Name.FileName})\n");
        repo.Write(new WikiPage(board, dashboard.ToString()));
        foreach (var section in sections)
            repo.Write(new WikiPage(section, $"# {section.Name.Title}\n"));
        return sections.Prepend(board).ToList();
    }

    /// <summary>Adds a card page at the end of a column and lists it on the dashboard and the column page.</summary>
    public static PagePath AddCard(WikiRepository repo, PagePath dashboard, PagePath column, string title)
    {
        var board = Require(repo, dashboard);
        RequireColumn(board, column);
        var name = PageName.FromTitle(title);
        var card = column.Append(name);
        if (repo.Exists(card))
            throw new InvalidOperationException($"'{name.Title}' is already on the board.");

        repo.Write(new WikiPage(card, $"# {name.Title}\n"));
        EditPage(repo, dashboard, lines => InsertInSection(lines, dashboard, column, int.MaxValue, Line(dashboard, card, name.Title)));
        EditPage(repo, column, lines => InsertInPage(lines, column, int.MaxValue, Line(column, card, name.Title)));
        return card;
    }

    /// <summary>
    /// Moves a card to <paramref name="index"/> in a column (the same one to
    /// reorder). Returns the card's page path, which changes with its column.
    /// </summary>
    public static PagePath MoveCard(WikiRepository repo, PagePath dashboard, PagePath card, PagePath toColumn, int index)
    {
        var board = Require(repo, dashboard);
        RequireColumn(board, toColumn);
        var from = board.Columns.FirstOrDefault(c => c.Cards.Any(k => Same(k.Page, card)))
                   ?? throw new InvalidOperationException($"{card.ToLinkPath()} is not on the board.");
        var title = from.Cards.First(k => Same(k.Page, card)).Title;

        var moved = card;
        if (!Same(from.Page, toColumn))
        {
            moved = toColumn.Append(card.Name);
            if (repo.Exists(moved))
                throw new InvalidOperationException($"'{title}' is already in that column.");
            MoveLinkRewriter.MoveAndRewrite(repo, card, moved);
            EditPage(repo, from.Page, lines => RemoveCardLines(lines, from.Page, moved));
        }

        EditPage(repo, dashboard, lines =>
        {
            RemoveCardLines(lines, dashboard, moved);
            InsertInSection(lines, dashboard, toColumn, index, Line(dashboard, moved, title));
        });
        EditPage(repo, toColumn, lines =>
        {
            RemoveCardLines(lines, toColumn, moved);
            InsertInPage(lines, toColumn, index, Line(toColumn, moved, title));
        });
        return moved;
    }

    /// <summary>Deletes a card's page (and its subpages) and its lines on the dashboard and column page.</summary>
    public static void RemoveCard(WikiRepository repo, PagePath dashboard, PagePath card)
    {
        Require(repo, dashboard);
        repo.Delete(card, deleteSubpages: true);
        EditPage(repo, dashboard, lines => RemoveCardLines(lines, dashboard, card));
        if (repo.Exists(card.Parent))
            EditPage(repo, card.Parent, lines => RemoveCardLines(lines, card.Parent, card));
    }

    /// <summary>Adds a column: its page, and a section at the end of the dashboard.</summary>
    public static PagePath AddColumn(WikiRepository repo, PagePath dashboard, string title)
    {
        if (!repo.Exists(dashboard))
            throw new InvalidOperationException($"Page not found: {dashboard.ToLinkPath()}");
        var name = PageName.FromTitle(title);
        var column = dashboard.Append(name);
        if (repo.Exists(column))
            throw new InvalidOperationException($"page already exists: {column.ToLinkPath()}");

        repo.Write(new WikiPage(column, $"# {name.Title}\n"));
        EditPage(repo, dashboard, lines =>
        {
            TrimTrailingBlank(lines);
            lines.Add("");
            lines.Add($"## [{name.Title}]({RelativeLink(dashboard, column)})");
        });
        return column;
    }

    private sealed record CardRef(int LineIndex, string Title, PagePath Page);

    // Start/End are 0-based line indexes of the section body: [Start, End).
    private sealed record Section(string Title, PagePath Page, int Start, int End, List<CardRef> Cards);

    private static List<Section> Sections(PagePath dashboard, string[] lines)
    {
        var headings = MarkdownHeadings.Parse(lines);
        var result = new List<Section>();
        foreach (var heading in headings.Where(h => h.Level == 2))
        {
            var match = HeadingLink().Match(heading.Text);
            if (!match.Success) continue;
            var page = Resolve(dashboard, match.Groups["href"].Value);
            if (page is null || !Same(page.Parent, dashboard)) continue;

            var end = MarkdownHeadings.SectionEnd(headings, heading, lines.Length) - 1;
            result.Add(new Section(match.Groups["text"].Value, page, heading.Line, end,
                Cards(dashboard, lines, heading.Line, end)));
        }
        return result;
    }

    private static List<CardRef> Cards(PagePath page, IReadOnlyList<string> lines, int start, int end)
    {
        var cards = new List<CardRef>();
        for (var i = start; i < end && i < lines.Count; i++)
        {
            var match = CardLine().Match(lines[i]);
            if (!match.Success) continue;
            if (Resolve(page, match.Groups["href"].Value) is { } target)
                cards.Add(new CardRef(i, match.Groups["text"].Value, target));
        }
        return cards;
    }

    private static PagePath? Resolve(PagePath from, string href)
    {
        var rel = LinkChecker.ResolveDocsRelativePath(from, href);
        return rel is not null && rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? PagePath.Parse("/" + rel[..^3])
            : null;
    }

    private static void RemoveCardLines(List<string> lines, PagePath page, PagePath card)
    {
        foreach (var hit in Cards(page, lines, 0, lines.Count).Where(c => Same(c.Page, card)).Reverse())
        {
            var i = hit.LineIndex;
            lines.RemoveAt(i);
            // Taking the only item out of a list leaves the blank lines that
            // were on either side of it next to each other.
            if (i > 0 && i < lines.Count && lines[i - 1].Trim().Length == 0 && lines[i].Trim().Length == 0)
                lines.RemoveAt(i);
        }
    }

    private static void InsertInSection(List<string> lines, PagePath dashboard, PagePath column, int index, string text)
    {
        var section = Sections(dashboard, lines.ToArray()).FirstOrDefault(s => Same(s.Page, column))
                      ?? throw new InvalidOperationException($"no column for {column.ToLinkPath()} on {dashboard.ToLinkPath()}");
        if (section.Cards.Count > 0)
        {
            InsertAmong(lines, section.Cards, index, text);
            return;
        }

        // Empty column: heading, blank, item, blank, next heading.
        var at = section.Start;
        if (at >= lines.Count || lines[at].Trim().Length != 0) lines.Insert(at, "");
        lines.Insert(at + 1, text);
        if (at + 2 < lines.Count && lines[at + 2].Trim().Length != 0) lines.Insert(at + 2, "");
    }

    private static void InsertInPage(List<string> lines, PagePath page, int index, string text)
    {
        var cards = Cards(page, lines, 0, lines.Count);
        if (cards.Count > 0)
        {
            InsertAmong(lines, cards, index, text);
            return;
        }
        TrimTrailingBlank(lines);
        lines.Add("");
        lines.Add(text);
    }

    private static void InsertAmong(List<string> lines, List<CardRef> cards, int index, string text) =>
        lines.Insert(index >= 0 && index < cards.Count ? cards[index].LineIndex : cards[^1].LineIndex + 1, text);

    private static void TrimTrailingBlank(List<string> lines)
    {
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
    }

    private static void EditPage(WikiRepository repo, PagePath page, Action<List<string>> edit)
    {
        var lines = LineEndings.ToLf(repo.Read(page).Markdown).Split('\n').ToList();
        edit(lines);
        repo.Write(new WikiPage(page, string.Join('\n', lines)));
    }

    private static string Line(PagePath from, PagePath card, string title) =>
        $"- [{title}]({RelativeLink(from, card)})";

    internal static string RelativeLink(PagePath from, PagePath to)
    {
        var fromDir = from.Segments.Take(from.Segments.Count - 1).Select(s => s.FileBase).ToList();
        var target = to.Segments.Select(s => s.FileBase).ToList();
        target[^1] += ".md";

        var common = 0;
        while (common < fromDir.Count && common < target.Count - 1
               && string.Equals(fromDir[common], target[common], StringComparison.OrdinalIgnoreCase))
            common++;
        return string.Concat(Enumerable.Repeat("../", fromDir.Count - common)) + string.Join('/', target.Skip(common));
    }

    private static KanbanBoard Require(WikiRepository repo, PagePath dashboard) =>
        TryLoad(repo, dashboard)
        ?? throw new InvalidOperationException($"{dashboard.ToLinkPath()} is not a kanban dashboard.");

    private static void RequireColumn(KanbanBoard board, PagePath column)
    {
        if (!board.Columns.Any(c => Same(c.Page, column)))
            throw new InvalidOperationException($"no column for {column.ToLinkPath()} on {board.Dashboard.ToLinkPath()}");
    }

    private static bool Same(PagePath a, PagePath b) =>
        string.Equals(a.ToLinkPath(), b.ToLinkPath(), StringComparison.OrdinalIgnoreCase);
}
