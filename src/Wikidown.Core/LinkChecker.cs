using System.Text.RegularExpressions;

namespace Wikidown.Core;

public enum LinkIssueKind
{
    Broken,
    AbsoluteTitlePath,
}

public sealed record LinkIssue(PagePath Page, int LineNumber, string Target, LinkIssueKind Kind);

public static partial class LinkChecker
{
    public static IEnumerable<LinkIssue> Check(WikiRepository repo, bool flagAbsolutePaths = true)
    {
        foreach (var path in repo.Walk())
        {
            var page = repo.Read(path);
            var lines = page.Markdown.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in LinkTarget().Matches(lines[i]))
                {
                    var target = match.Groups[1].Value.Trim();
                    var issue = Classify(repo, path, i + 1, target, flagAbsolutePaths);
                    if (issue is not null) yield return issue;
                }
            }
        }
    }

    private static LinkIssue? Classify(
        WikiRepository repo, PagePath page, int lineNumber, string target, bool flagAbsolutePaths)
    {
        if (target.Length == 0) return null;
        if (IsExternal(target)) return null;
        if (target.StartsWith('#')) return null;

        if (target.StartsWith('/'))
            return flagAbsolutePaths
                ? new LinkIssue(page, lineNumber, target, LinkIssueKind.AbsoluteTitlePath)
                : null;

        return Resolves(repo, page, target)
            ? null
            : new LinkIssue(page, lineNumber, target, LinkIssueKind.Broken);
    }

    internal static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static bool Resolves(WikiRepository repo, PagePath page, string target)
    {
        var withoutFragment = target.Split('#')[0];
        if (withoutFragment.Length == 0) return true;
        return File.Exists(ResolveFullPath(repo, page, withoutFragment));
    }

    // Resolves a relative link/image target (fragment already stripped) to an
    // absolute disk path, relative to the page it appears on. Shared with
    // PdfExport, which needs the resolved path itself, not just whether it
    // exists.
    internal static string ResolveFullPath(WikiRepository repo, PagePath page, string targetWithoutFragment)
    {
        var pageDir = Path.GetDirectoryName(page.ToFilePath()) ?? string.Empty;
        var relative = targetWithoutFragment.Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.Combine(repo.RootPath, pageDir, relative);
        return Path.GetFullPath(combined);
    }

    [GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    internal static partial Regex LinkTarget();
}
