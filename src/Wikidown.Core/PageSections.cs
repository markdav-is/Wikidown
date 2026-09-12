namespace Wikidown.Core;

// StartLine/EndLine are 1-based and inclusive; the heading line is StartLine.
public sealed record SectionReadResult(string Markdown, HeadingInfo Heading, int StartLine, int EndLine, int MatchCount)
{
    public string? Note => MatchCount > 1 ? $"note: {MatchCount} headings matched; returned the first" : null;
}

public static class PageSections
{
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
