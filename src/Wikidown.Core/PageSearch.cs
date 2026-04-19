namespace Wikidown.Core;

public sealed record SearchHit(PagePath Path, int LineNumber, string Line);

public static class PageSearch
{
    public static IEnumerable<SearchHit> Search(
        WikiRepository repo, string query, bool caseSensitive = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        var cmp = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        foreach (var path in repo.Walk())
        {
            var page = repo.Read(path);
            var lines = page.Markdown.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(query, cmp))
                    yield return new SearchHit(path, i + 1, lines[i].TrimEnd('\r'));
            }
        }
    }
}
