using System.Text.RegularExpressions;

namespace Wikidown.Core;

public enum LinkIssueKind
{
    Broken,
    AbsoluteTitlePath,
    // Resolves on Windows/macOS, 404s once published to a case-sensitive host.
    CaseMismatch,
}

public sealed record LinkIssue(PagePath Page, int LineNumber, string Target, LinkIssueKind Kind);

public static partial class LinkChecker
{
    public static IEnumerable<LinkIssue> Check(WikiRepository repo, bool flagAbsolutePaths = true)
    {
        foreach (var path in repo.Walk())
        {
            foreach (var (line, target) in LinkTargets(repo.Read(path).Markdown))
            {
                var issue = Classify(repo, path, line, target.Trim(), flagAbsolutePaths);
                if (issue is not null) yield return issue;
            }
        }
    }

    // Link/image targets with their 1-based line, skipping fenced code
    // blocks and inline code spans — those are examples, not links.
    internal static IEnumerable<(int Line, string Target)> LinkTargets(string markdown)
    {
        var lines = markdown.Split('\n');
        string? fence = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            var opener = FenceOpener().Match(trimmed);
            if (fence is null && opener.Success)
            {
                fence = opener.Value;
                continue;
            }
            if (fence is not null)
            {
                if (trimmed.TrimEnd().Length >= fence.Length
                    && trimmed.TrimEnd().All(c => c == fence[0]))
                    fence = null;
                continue;
            }

            foreach (Match match in LinkTarget().Matches(CodeSpan().Replace(lines[i], "")))
                yield return (i + 1, match.Groups[1].Value);
        }
    }

    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex FenceOpener();

    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpan();

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

        var withoutFragment = target.Split('#')[0];
        if (withoutFragment.Length == 0) return null;

        var full = ResolveFullPath(repo, page, withoutFragment);
        if (!File.Exists(full))
            return new LinkIssue(page, lineNumber, target, LinkIssueKind.Broken);
        return PathCase.ExistsExactBelow(repo.RootPath, full)
            ? null
            : new LinkIssue(page, lineNumber, target, LinkIssueKind.CaseMismatch);
    }

    internal static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// Resolves a relative link/image target as written on <paramref name="page"/> to a
    /// forward-slash path relative to the docs root, with no filesystem access.
    /// A leading "/" is the docs root (the Azure DevOps wiki convention for
    /// <c>/.attachments/x.png</c>); otherwise the target is relative to the page's folder.
    /// Returns null for external targets, fragments, or paths that climb above the root.
    /// </summary>
    public static string? ResolveDocsRelativePath(PagePath page, string target)
    {
        var withoutFragment = target.Split('#')[0].Trim();
        if (withoutFragment.Length == 0 || IsExternal(withoutFragment)) return null;
        if (withoutFragment.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

        var stack = new List<string>();
        if (!withoutFragment.StartsWith('/'))
        {
            var pageDir = Path.GetDirectoryName(page.ToFilePath()) ?? string.Empty;
            stack.AddRange(pageDir.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var seg in withoutFragment.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..")
            {
                if (stack.Count == 0) return null;
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(Uri.UnescapeDataString(seg));
        }

        return stack.Count == 0 ? null : string.Join('/', stack);
    }

    [GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    internal static partial Regex LinkTarget();
}
