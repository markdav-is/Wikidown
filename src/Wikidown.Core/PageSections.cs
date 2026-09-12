namespace Wikidown.Core;

// StartLine/EndLine are 1-based and inclusive; the heading line is StartLine.
public sealed record SectionReadResult(string Markdown, HeadingInfo Heading, int StartLine, int EndLine, int MatchCount)
{
    public string? Note => MatchCount > 1 ? $"note: {MatchCount} headings matched; returned the first" : null;
}

// Lines are 1-based. For a replace, StartLine..EndLine is the old body
// span (empty when EndLine < StartLine); for a create, the new section.
public sealed record SectionWriteResult(string Summary, int StartLine, int EndLine, int NewLineCount, bool Created);

public static class PageSections
{
    // Replaces the body under a heading — everything after the heading line
    // up to the next heading of the same or higher level, ### children
    // included — with `lfBody`. The heading line itself is kept verbatim.
    // Exactly one blank line separates the heading from the body and the
    // body from the next heading, so repeated writes don't stack blanks.
    public static SectionWriteResult Write(PagePath page, string lfText, string section, string lfBody,
        bool createIfMissing, out string lfResult)
    {
        var lines = SplitLines(lfText);
        var headings = MarkdownHeadings.Parse(lines);
        var matches = MarkdownHeadings.Find(headings, section);
        if (matches.Count > 1) throw MarkdownHeadings.Ambiguous(page, section, matches.Count);

        var body = TrimBlankEdges(SplitLines(lfBody));

        if (matches.Count == 0)
        {
            if (!createIfMissing) throw MarkdownHeadings.NoSection(page, section, headings);
            return Create(page, lines, MarkdownHeadings.Normalize(section), body, out lfResult);
        }

        var heading = matches[0];
        var end = MarkdownHeadings.SectionEnd(headings, heading, lines.Length);
        var hasNext = end <= lines.Length;

        var block = new List<string>();
        if (body.Count > 0 || hasNext) block.Add("");
        block.AddRange(body);
        if (body.Count > 0 && hasNext) block.Add("");

        var result = new List<string>(lines[..heading.Line]);
        result.AddRange(block);
        result.AddRange(lines[(end - 1)..]);
        lfResult = Join(result);

        var oldStart = heading.Line + 1;
        var oldEnd = end - 1;
        var summary = oldEnd >= oldStart
            ? $"wrote section '{heading.Text}' in {page.ToLinkPath()} (replaced lines {oldStart}–{oldEnd} with {body.Count} lines)"
            : $"wrote section '{heading.Text}' in {page.ToLinkPath()} (inserted {body.Count} lines after line {heading.Line})";
        return new SectionWriteResult(summary, oldStart, oldEnd, body.Count, Created: false);
    }

    private static SectionWriteResult Create(PagePath page, string[] lines, string title, List<string> body, out string lfResult)
    {
        var result = TrimTrailingBlank(lines);
        if (result.Count > 0) result.Add("");
        var start = result.Count + 1;
        result.Add("## " + title);
        if (body.Count > 0)
        {
            result.Add("");
            result.AddRange(body);
        }
        lfResult = Join(result);
        var end = result.Count;
        return new SectionWriteResult(
            $"created section '{title}' in {page.ToLinkPath()} (lines {start}–{end})",
            start, end, body.Count, Created: true);
    }

    internal static string[] SplitLines(string lfText)
    {
        if (lfText.Length == 0) return Array.Empty<string>();
        var lines = lfText.Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    internal static List<string> TrimTrailingBlank(IReadOnlyList<string> lines)
    {
        var end = lines.Count;
        while (end > 0 && string.IsNullOrWhiteSpace(lines[end - 1])) end--;
        return lines.Take(end).ToList();
    }

    internal static List<string> TrimBlankEdges(IReadOnlyList<string> lines)
    {
        var start = 0;
        while (start < lines.Count && string.IsNullOrWhiteSpace(lines[start])) start++;
        return TrimTrailingBlank(lines.Skip(start).ToList());
    }

    internal static string Join(IReadOnlyList<string> lines) =>
        lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";

    // Heading line plus everything below it up to the next heading of the
    // same or higher level, so a ## section includes its ### children.
    // Trailing blank lines are dropped; the result ends with one newline.
    public static SectionReadResult Read(PagePath page, string lfText, string section)
    {
        var lines = lfText.Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0) lines = lines[..^1];
        var headings = MarkdownHeadings.Parse(lines);
        var matches = MarkdownHeadings.Find(headings, section);
        if (matches.Count == 0) throw MarkdownHeadings.NoSection(page, section, headings);

        var heading = matches[0];
        var end = MarkdownHeadings.SectionEnd(headings, heading, lines.Length) - 1;
        while (end > heading.Line && lines[end - 1].Length == 0) end--;

        var body = string.Join("\n", lines[(heading.Line - 1)..end]) + "\n";
        return new SectionReadResult(body, heading, heading.Line, end, matches.Count);
    }
}
