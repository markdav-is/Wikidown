using System.Text.RegularExpressions;

namespace Wikidown.Core;

public enum LinkIssueKind
{
    Broken,
    // A "/"-rooted link to a page or file in the wiki: works in an ADO wiki,
    // 404s on github.com and on Pages project sites.
    AbsolutePath,
    // Resolves on Windows/macOS, 404s once published to a case-sensitive host.
    CaseMismatch,
    // A file outside the wiki root: no publishing route ships it.
    OutsideWiki,
}

public sealed record LinkIssue(PagePath Page, int LineNumber, string Target, LinkIssueKind Kind);

public static partial class LinkChecker
{
    public static IEnumerable<LinkIssue> Check(WikiRepository repo, bool flagAbsolutePaths = true) =>
        Check(repo, repo.Walk(), flagAbsolutePaths);

    public static IEnumerable<LinkIssue> Check(
        WikiRepository repo, IEnumerable<PagePath> pages, bool flagAbsolutePaths = true)
    {
        var resolver = new TargetResolver(repo);
        foreach (var path in pages)
        {
            foreach (var (line, target) in LinkTargets(repo.Read(path).Markdown))
            {
                var issue = Classify(repo, resolver, path, line, target.Trim(), flagAbsolutePaths);
                if (issue is not null) yield return issue;
            }
        }
    }

    // Link/image targets with their 1-based line, skipping fenced code
    // blocks and inline code spans — those are examples, not links.
    internal static IEnumerable<(int Line, string Target)> LinkTargets(string markdown)
    {
        var lines = markdown.Split('\n');
        foreach (var i in ProseLines(lines))
        {
            foreach (var group in TargetGroups(lines[i]))
                yield return (i + 1, group.Value);
        }
    }

    // Indexes of lines outside fenced code blocks.
    internal static IEnumerable<int> ProseLines(string[] lines)
    {
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
            yield return i;
        }
    }

    // The target of each markdown link/image and raw <img src> on one line,
    // in order, except those inside inline code spans.
    internal static IEnumerable<Group> TargetGroups(string line)
    {
        var code = CodeSpan().Matches(line).Select(m => (Start: m.Index, End: m.Index + m.Length)).ToList();
        bool InCode(int index) => code.Any(span => index >= span.Start && index < span.End);

        return LinkTarget().Matches(line).Where(m => !InCode(m.Index)).Select(m => m.Groups[1])
            .Concat(ImgSrc().Matches(line).Where(m => !InCode(m.Index)).Select(m => m.Groups["src"]))
            .OrderBy(g => g.Index);
    }

    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex FenceOpener();

    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"<img\b[^>]*?\bsrc\s*=\s*(?:""(?<src>[^""]*)""|'(?<src>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrc();

    private static LinkIssue? Classify(
        WikiRepository repo, TargetResolver resolver, PagePath page, int lineNumber, string target, bool flagAbsolutePaths)
    {
        if (target.Length == 0) return null;
        if (target.StartsWith('#')) return null;
        var resolved = resolver.Resolve(page, target);
        if (resolved is null) return null;

        if (IsCopyable(target) && resolved is { Exists: true, InsideWiki: false })
            return new LinkIssue(page, lineNumber, target, LinkIssueKind.OutsideWiki);

        if (target.StartsWith('/'))
        {
            if (target.Split('#')[0].Trim('/').Length == 0) return null;
            if (resolver.ResolveRooted(target) is null && !resolved.Exists)
                return new LinkIssue(page, lineNumber, target, LinkIssueKind.Broken);
            return flagAbsolutePaths
                ? new LinkIssue(page, lineNumber, target, LinkIssueKind.AbsolutePath)
                : null;
        }

        if (!resolved.Exists)
            return new LinkIssue(page, lineNumber, target, LinkIssueKind.Broken);
        return PathCase.ExistsExactBelow(repo.RootPath, resolved.FullPath)
            ? null
            : new LinkIssue(page, lineNumber, target, LinkIssueKind.CaseMismatch);
    }

    // Pages are linked, not copied: a .md outside the wiki is some other
    // document, and check-links --fix leaves it alone.
    internal static bool IsCopyable(string target) =>
        !target.Split('#')[0].TrimEnd().EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("//", StringComparison.Ordinal);

    // Resolves a relative link target (fragment already stripped) to an
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

    // Anchored on "](" rather than the whole "[text](" so an image nested in
    // a link, [![alt](img.png)](full.png), yields both targets.
    [GeneratedRegex(@"\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    internal static partial Regex LinkTarget();
}
