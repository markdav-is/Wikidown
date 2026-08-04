using System.Text.RegularExpressions;

namespace Wikidown.Core;

public sealed record LinkRewrite(PagePath Page, int LineNumber, string OldTarget, string NewTarget);

public sealed class MoveLinkPlan
{
    internal MoveLinkPlan(
        IReadOnlyDictionary<string, PagePath> movedPages,
        IReadOnlyDictionary<PagePath, string> newContentByOldPage,
        IReadOnlyList<LinkRewrite> rewrites)
    {
        MovedPages = movedPages;
        NewContentByOldPage = newContentByOldPage;
        Rewrites = rewrites;
    }

    internal IReadOnlyDictionary<string, PagePath> MovedPages { get; }
    internal IReadOnlyDictionary<PagePath, string> NewContentByOldPage { get; }
    public IReadOnlyList<LinkRewrite> Rewrites { get; }
}

// Rewrites in-wiki links affected by a page move: inbound links that pointed
// at the moved page/subtree, and the moved pages' own relative links whose
// correct depth changed. Operates in two steps because file identities shift
// mid-move: Plan() reads pre-move content and computes the rewrite, then
// Apply() writes it after WikiRepository.Move has relocated the files.
public static partial class MoveLinkRewriter
{
    public static MoveLinkPlan Plan(WikiRepository repo, PagePath from, PagePath to)
    {
        var movedPages = BuildMovedMap(repo, from, to);
        var newContent = new Dictionary<PagePath, string>();
        var rewrites = new List<LinkRewrite>();

        foreach (var oldPage in repo.Walk())
        {
            var newPage = movedPages.GetValueOrDefault(oldPage.ToLinkPath(), oldPage);
            var lines = repo.Read(oldPage).Markdown.Split('\n');
            var changed = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                lines[i] = LinkChecker.LinkTarget().Replace(lines[i], match =>
                {
                    var group = match.Groups[1];
                    var target = group.Value;
                    var replacement = ComputeReplacement(repo, from, to, movedPages, oldPage, newPage, target);
                    if (replacement is null || replacement == target) return match.Value;

                    changed = true;
                    rewrites.Add(new LinkRewrite(oldPage, lineNumber, target, replacement));
                    var offset = group.Index - match.Index;
                    return match.Value[..offset] + replacement + match.Value[(offset + group.Length)..];
                });
            }

            if (changed) newContent[oldPage] = string.Join('\n', lines);
        }

        return new MoveLinkPlan(movedPages, newContent, rewrites);
    }

    // Moves the page (and subpages), then rewrites inbound links and the
    // moved pages' own relative links/images for their new depth.
    public static IReadOnlyList<LinkRewrite> MoveAndRewrite(WikiRepository repo, PagePath from, PagePath to)
    {
        var plan = Plan(repo, from, to);
        repo.Move(from, to);
        Apply(repo, plan);
        return plan.Rewrites;
    }

    public static void Apply(WikiRepository repo, MoveLinkPlan plan)
    {
        foreach (var (oldPage, content) in plan.NewContentByOldPage)
        {
            var newPage = plan.MovedPages.GetValueOrDefault(oldPage.ToLinkPath(), oldPage);
            repo.Write(new WikiPage(newPage, content));
        }
    }

    private static Dictionary<string, PagePath> BuildMovedMap(WikiRepository repo, PagePath from, PagePath to)
    {
        var map = new Dictionary<string, PagePath>(StringComparer.OrdinalIgnoreCase)
        {
            [from.ToLinkPath()] = to,
        };
        foreach (var descendant in repo.Walk(from))
        {
            var suffix = descendant.Segments.Skip(from.Segments.Count);
            var newPath = suffix.Aggregate(to, (acc, seg) => acc.Append(seg));
            map[descendant.ToLinkPath()] = newPath;
        }
        return map;
    }

    private static string? ComputeReplacement(
        WikiRepository repo, PagePath from, PagePath to,
        IReadOnlyDictionary<string, PagePath> movedPages,
        PagePath oldPage, PagePath newPage, string target)
    {
        if (target.Length == 0 || LinkChecker.IsExternal(target) || target.StartsWith('#'))
            return null;

        var hashIndex = target.IndexOf('#');
        var targetPath = hashIndex >= 0 ? target[..hashIndex] : target;
        var fragment = hashIndex >= 0 ? target[hashIndex..] : "";
        if (targetPath.Length == 0) return null;

        if (targetPath.StartsWith('/'))
        {
            var remapped = RemapPagePath(PagePath.Parse(targetPath), from, to);
            return remapped is null ? null : remapped.ToLinkPath() + fragment;
        }

        var oldPageDir = Path.GetDirectoryName(oldPage.ToFilePath()) ?? "";
        var oldTargetFull = Path.GetFullPath(
            Path.Combine(repo.RootPath, oldPageDir, targetPath.Replace('/', Path.DirectorySeparatorChar)));
        var oldTargetRel = Path.GetRelativePath(repo.RootPath, oldTargetFull);

        var newTargetRel = RemapDiskPath(oldTargetRel, from, to) ?? oldTargetRel;
        var newTargetFull = Path.GetFullPath(Path.Combine(repo.RootPath, newTargetRel));

        var newPageDirFull = Path.GetFullPath(
            Path.Combine(repo.RootPath, Path.GetDirectoryName(newPage.ToFilePath()) ?? ""));

        var newRelative = Path.GetRelativePath(newPageDirFull, newTargetFull)
            .Replace(Path.DirectorySeparatorChar, '/');
        return newRelative + fragment;
    }

    private static string? RemapDiskPath(string relPath, PagePath from, PagePath to)
    {
        var fromFile = from.ToFilePath();
        if (string.Equals(relPath, fromFile, StringComparison.OrdinalIgnoreCase))
            return to.ToFilePath();

        var fromFolder = from.ToFolderPath();
        var prefix = fromFolder + Path.DirectorySeparatorChar;
        if (relPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(to.ToFolderPath(), relPath[prefix.Length..]);

        return null;
    }

    private static PagePath? RemapPagePath(PagePath p, PagePath from, PagePath to)
    {
        if (p.IsRoot || p.Segments.Count < from.Segments.Count) return null;
        if (!SegmentsPrefixEqual(p, from)) return null;
        if (p.Segments.Count == from.Segments.Count) return to;

        var suffix = p.Segments.Skip(from.Segments.Count);
        return suffix.Aggregate(to, (acc, seg) => acc.Append(seg));
    }

    private static bool SegmentsPrefixEqual(PagePath p, PagePath prefix) =>
        p.Segments.Take(prefix.Segments.Count).Select(s => s.FileBase)
            .SequenceEqual(prefix.Segments.Select(s => s.FileBase), StringComparer.OrdinalIgnoreCase);
}
