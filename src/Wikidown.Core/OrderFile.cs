namespace Wikidown.Core;

// An ADO wiki `.order` file: one page base-name per line, defining the
// display order of pages within a folder. Blank lines and comments ignored on
// read; writes are normalized to LF with a trailing newline.
public static class OrderFile
{
    public const string FileName = ".order";

    public static IReadOnlyList<string> Parse(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0) continue;
            if (line.StartsWith('#')) continue;
            result.Add(line);
        }
        return result;
    }

    public static string Render(IEnumerable<string> entries)
    {
        var filtered = entries
            .Select(e => e?.Trim() ?? string.Empty)
            .Where(e => e.Length > 0)
            .ToList();
        return filtered.Count == 0 ? string.Empty : string.Join("\n", filtered) + "\n";
    }
}
