using System.Text;

namespace Wikidown.Core;

public sealed record EditResult(int Replacements, IReadOnlyList<(int Start, int End)> Lines, string Summary);

// Exact-substring patching of a page body: the Edit-tool contract (find
// `old`, replace with `new`, refuse ambiguity) applied to raw markdown.
public static class PageEdit
{
    private const int ContextLines = 2;

    public static EditResult Apply(PagePath page, string lfText, string oldText, string newText, bool replaceAll,
        out string lfResult)
    {
        if (string.IsNullOrEmpty(oldText))
            throw new ArgumentException("old text must not be empty; pass the exact text to replace", nameof(oldText));

        var old = LineEndings.ToLf(oldText);
        var @new = LineEndings.ToLf(newText);
        if (old == @new)
            throw new InvalidOperationException(
                "old and new text are identical; nothing to change (pass the changed text as new)");

        var matches = FindAll(lfText, old);
        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"no match for old text in {page.ToLinkPath()}; read the page (or section) and pass the exact current text, including punctuation and spacing");
        if (matches.Count > 1 && !replaceAll)
            throw new InvalidOperationException(
                $"old text matches {matches.Count} times in {page.ToLinkPath()}; pass more surrounding context to make it unique, or replaceAll=true to change every occurrence");

        var breadcrumbEnd = BreadcrumbLineEnd(lfText);
        if (breadcrumbEnd >= 0 && matches[0] < breadcrumbEnd)
            throw new InvalidOperationException(
                "breadcrumb is managed by Wikidown; edit the body only (start old below the first line)");

        var sb = new StringBuilder(lfText.Length - matches.Count * old.Length + matches.Count * @new.Length);
        var ranges = new List<(int Start, int End)>();
        var pos = 0;
        foreach (var m in matches)
        {
            sb.Append(lfText, pos, m - pos);
            var startIdx = sb.Length;
            sb.Append(@new);
            var endIdx = Math.Max(startIdx, sb.Length - 1);
            ranges.Add((LineOf(sb, startIdx), LineOf(sb, endIdx)));
            pos = m + old.Length;
        }
        sb.Append(lfText, pos, lfText.Length - pos);
        lfResult = sb.ToString();

        var summary = RenderSummary(page, lfResult, ranges);
        return new EditResult(matches.Count, ranges, summary);
    }

    private static List<int> FindAll(string text, string needle)
    {
        var hits = new List<int>();
        var idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            hits.Add(idx);
            idx += needle.Length;
        }
        return hits;
    }

    // Index just past the breadcrumb line's newline, or -1 when the page
    // has no breadcrumb. A match starting before it touches the breadcrumb.
    private static int BreadcrumbLineEnd(string lfText)
    {
        var nl = lfText.IndexOf('\n');
        var firstLine = nl < 0 ? lfText : lfText[..nl];
        if (!firstLine.EndsWith(Breadcrumb.Marker, StringComparison.Ordinal)) return -1;
        return nl < 0 ? lfText.Length : nl + 1;
    }

    private static int LineOf(StringBuilder sb, int index)
    {
        var line = 1;
        var limit = Math.Min(index, sb.Length);
        for (var i = 0; i < limit; i++)
            if (sb[i] == '\n') line++;
        return line;
    }

    private static string RenderSummary(PagePath page, string lfResult, List<(int Start, int End)> ranges)
    {
        var lines = lfResult.Split('\n');
        if (lines.Length > 1 && lines[^1].Length == 0) lines = lines[..^1];
        var lineCount = Math.Max(lines.Length, 1);

        var where = string.Join(", ", ranges.Select(r => r.Start == r.End ? $"{r.Start}" : $"{r.Start}–{r.End}"));
        var noun = ranges.Count == 1 ? "replacement" : "replacements";
        var lineWord = ranges.Count == 1 && ranges[0].Start == ranges[0].End ? "line" : "lines";
        var sb = new StringBuilder();
        sb.Append($"edited {page.ToLinkPath()} ({ranges.Count} {noun}, {lineWord} {where})");

        var changed = new HashSet<int>();
        foreach (var r in ranges)
            for (var l = r.Start; l <= r.End; l++) changed.Add(l);

        var windows = new List<(int Start, int End)>();
        foreach (var r in ranges)
        {
            var s = Math.Max(1, r.Start - ContextLines);
            var e = Math.Min(lineCount, r.End + ContextLines);
            if (windows.Count > 0 && s <= windows[^1].End + 1)
                windows[^1] = (windows[^1].Start, Math.Max(windows[^1].End, e));
            else
                windows.Add((s, e));
        }

        var width = windows[^1].End.ToString().Length;
        foreach (var (start, end) in windows)
        {
            if (windows.Count > 1 && start != windows[0].Start) sb.Append("\n  ...");
            for (var l = start; l <= end; l++)
            {
                var text = l - 1 < lines.Length ? lines[l - 1] : "";
                var marker = changed.Contains(l) ? ">" : " ";
                sb.Append('\n').Append(marker).Append(' ').Append(l.ToString().PadLeft(width)).Append(" | ").Append(text);
            }
        }
        return sb.ToString();
    }
}
