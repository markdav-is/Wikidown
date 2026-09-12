using System.Text.RegularExpressions;

namespace Wikidown.Core;

// Line is 1-based. Text is the heading without its #s or closing #s.
public sealed record HeadingInfo(int Line, int Level, string Text);

// Heading matching shared by the section tools (read / write_section /
// append): case-insensitive, ignoring leading #s and surrounding
// whitespace, ATX headings only, and never inside a fenced code block.
public static partial class MarkdownHeadings
{
    [GeneratedRegex(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*$")]
    private static partial Regex Atx();

    [GeneratedRegex(@"[ \t]+#+$")]
    private static partial Regex ClosingHashes();

    public static string Normalize(string heading)
    {
        var s = heading.Trim().TrimStart('#').Trim();
        s = ClosingHashes().Replace(s, "");
        return s.Trim();
    }

    public static IReadOnlyList<HeadingInfo> Parse(string[] lfLines)
    {
        var result = new List<HeadingInfo>();
        char fence = '\0';
        for (var i = 0; i < lfLines.Length; i++)
        {
            var line = lfLines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                var c = trimmed[0];
                if (fence == '\0') fence = c;
                else if (fence == c) fence = '\0';
                continue;
            }
            if (fence != '\0') continue;

            var m = Atx().Match(line);
            if (!m.Success) continue;
            var text = Normalize(m.Groups[2].Value);
            result.Add(new HeadingInfo(i + 1, m.Groups[1].Length, text));
        }
        return result;
    }

    public static IReadOnlyList<HeadingInfo> Find(IReadOnlyList<HeadingInfo> headings, string section)
    {
        var wanted = Normalize(section);
        return headings.Where(h => string.Equals(h.Text, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // 1-based line just past the section: the next heading of the same or
    // higher level, or one past the last line.
    public static int SectionEnd(IReadOnlyList<HeadingInfo> headings, HeadingInfo heading, int lineCount)
    {
        var next = headings.FirstOrDefault(h => h.Line > heading.Line && h.Level <= heading.Level);
        return next?.Line ?? lineCount + 1;
    }

    public static string Describe(IReadOnlyList<HeadingInfo> headings) =>
        headings.Count == 0
            ? "page has no headings"
            : "headings: " + string.Join(" · ", headings.Select(h => h.Text));

    public static InvalidOperationException NoSection(PagePath page, string section, IReadOnlyList<HeadingInfo> headings) =>
        new($"no section '{Normalize(section)}' in {page.ToLinkPath()}; {Describe(headings)}");

    public static InvalidOperationException Ambiguous(PagePath page, string section, int count) =>
        new($"{count} headings match '{Normalize(section)}' in {page.ToLinkPath()}; pass more of the heading text (or its #s) to pick one");
}
