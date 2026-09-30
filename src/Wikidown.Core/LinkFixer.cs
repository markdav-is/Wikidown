namespace Wikidown.Core;

public enum CopyIssueKind
{
    // The source the copy mirrors has changed since it was copied.
    Stale,
    // No page references the copy any more.
    Unused,
}

// Path is relative to the wiki root, "/"-separated.
public sealed record CopyIssue(string Path, CopyIssueKind Kind);

public enum CopyActionKind { Created, Updated, Deleted }

public sealed record CopyAction(string Path, CopyActionKind Kind);

public sealed record LinkFixResult(
    IReadOnlyList<LinkRewrite> Rewrites,
    IReadOnlyList<CopyAction> Copies,
    IReadOnlyList<string> Warnings);

// check-links --fix: copies files that pages reference from outside the wiki
// into .attachments/from-repo (or from-external) and points the links at the copies,
// so every publishing route can ship them, and turns "/"-rooted links to
// wiki pages and files into relative ones. Also keeps the managed copies in
// step with their sources and removes the ones nothing references.
public static class LinkFixer
{
    public static LinkFixResult Fix(WikiRepository repo, bool dryRun = false)
    {
        var resolver = new TargetResolver(repo);
        var rewrites = new List<LinkRewrite>();
        var warnings = new List<string>();
        var copies = new Dictionary<string, string>(TargetResolver.PathComparer);
        var contents = new Dictionary<PagePath, string>();
        var changed = new List<PagePath>();

        foreach (var page in repo.Walk())
        {
            var lines = repo.Read(page).Markdown.Split('\n');
            var pageChanged = false;
            foreach (var i in LinkChecker.ProseLines(lines))
            {
                // Rightmost first, so earlier indexes stay valid.
                foreach (var group in LinkChecker.TargetGroups(lines[i]).Reverse())
                {
                    var target = group.Value.Trim();
                    string? destination;
                    if (LinkChecker.IsCopyable(target)
                        && resolver.Resolve(page, target) is { Exists: true, InsideWiki: false } outside)
                    {
                        var copy = resolver.CopyPathFor(outside.FullPath);
                        copies[copy] = outside.FullPath;
                        if (copy.StartsWith(TargetResolver.ExternalCopies + "/", StringComparison.Ordinal))
                            warnings.Add($"{page.ToLinkPath()}: {target} is outside the project; copied to {copy}, but it can't be refreshed from its source");
                        destination = CopyFullPath(repo, copy);
                    }
                    else
                    {
                        destination = resolver.ResolveRooted(target);
                    }
                    if (destination is null) continue;

                    var hash = target.IndexOf('#');
                    var newTarget = RelativeFromPage(repo, page, destination) + (hash < 0 ? "" : target[hash..]);
                    rewrites.Add(new LinkRewrite(page, i + 1, target, newTarget));
                    lines[i] = lines[i][..group.Index] + newTarget + lines[i][(group.Index + group.Length)..];
                    pageChanged = true;
                }
            }
            contents[page] = string.Join('\n', lines);
            if (pageChanged) changed.Add(page);
        }

        var actions = new List<CopyAction>();
        foreach (var (copy, source) in copies)
        {
            var kind = Sync(source, CopyFullPath(repo, copy), dryRun);
            if (kind is not null) actions.Add(new CopyAction(copy, kind.Value));
        }

        foreach (var issue in CheckCopies(repo, resolver, contents))
        {
            if (copies.ContainsKey(issue.Path)) continue;
            var full = CopyFullPath(repo, issue.Path);
            if (issue.Kind == CopyIssueKind.Unused)
            {
                if (!dryRun) File.Delete(full);
                actions.Add(new CopyAction(issue.Path, CopyActionKind.Deleted));
            }
            else if (Sync(resolver.SourceForCopy(full)!, full, dryRun) is { } kind)
            {
                actions.Add(new CopyAction(issue.Path, kind));
            }
        }

        foreach (var copy in ManagedCopies(repo))
        {
            var source = resolver.SourceForCopy(copy);
            if (source is not null && !File.Exists(source) && File.Exists(copy))
                warnings.Add($"{WikiRelative(repo, copy)}: source {source} is gone; keeping the copy");
        }

        if (!dryRun)
        {
            foreach (var page in changed) repo.Write(new WikiPage(page, contents[page]));
            RemoveEmptyFolders(repo);
        }

        rewrites.Reverse();
        return new LinkFixResult(
            rewrites.OrderBy(r => r.Page.ToLinkPath(), StringComparer.Ordinal).ThenBy(r => r.LineNumber).ToList(),
            actions.OrderBy(a => a.Path, StringComparer.Ordinal).ToList(),
            warnings);
    }

    /// <summary>Managed copies that are stale or that no page references.</summary>
    public static IEnumerable<CopyIssue> CheckCopies(WikiRepository repo)
    {
        var contents = repo.Walk().ToDictionary(p => p, p => repo.Read(p).Markdown);
        return CheckCopies(repo, new TargetResolver(repo), contents);
    }

    private static List<CopyIssue> CheckCopies(
        WikiRepository repo, TargetResolver resolver, IReadOnlyDictionary<PagePath, string> contents)
    {
        var referenced = new HashSet<string>(TargetResolver.PathComparer);
        foreach (var (page, markdown) in contents)
        {
            foreach (var (_, target) in LinkChecker.LinkTargets(markdown))
            {
                if (resolver.Resolve(page, target) is { Exists: true, InsideWiki: true } resolved)
                    referenced.Add(resolved.FullPath);
            }
        }

        var issues = new List<CopyIssue>();
        foreach (var copy in ManagedCopies(repo))
        {
            var relative = WikiRelative(repo, copy);
            if (!referenced.Contains(copy))
                issues.Add(new CopyIssue(relative, CopyIssueKind.Unused));
            else if (resolver.SourceForCopy(copy) is { } source && File.Exists(source) && !SameBytes(source, copy))
                issues.Add(new CopyIssue(relative, CopyIssueKind.Stale));
        }
        return issues;
    }

    private static IEnumerable<string> ManagedCopies(WikiRepository repo) =>
        new[] { TargetResolver.RepoCopies, TargetResolver.ExternalCopies }
            .Select(folder => CopyFullPath(repo, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal);

    private static CopyActionKind? Sync(string source, string copy, bool dryRun)
    {
        CopyActionKind kind;
        if (!File.Exists(copy)) kind = CopyActionKind.Created;
        else if (!SameBytes(source, copy)) kind = CopyActionKind.Updated;
        else return null;

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(source, copy, overwrite: true);
        }
        return kind;
    }

    private static bool SameBytes(string a, string b) =>
        new FileInfo(a).Length == new FileInfo(b).Length
        && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));

    private static string RelativeFromPage(WikiRepository repo, PagePath page, string fullPath)
    {
        var pageDir = Path.Combine(repo.RootPath, Path.GetDirectoryName(page.ToFilePath()) ?? "");
        return Path.GetRelativePath(pageDir, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string CopyFullPath(WikiRepository repo, string wikiRelative) =>
        Path.GetFullPath(Path.Combine(repo.RootPath, wikiRelative.Replace('/', Path.DirectorySeparatorChar)));

    private static string WikiRelative(WikiRepository repo, string fullPath) =>
        Path.GetRelativePath(repo.RootPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    private static void RemoveEmptyFolders(WikiRepository repo)
    {
        foreach (var folder in new[] { TargetResolver.RepoCopies, TargetResolver.ExternalCopies })
        {
            var root = CopyFullPath(repo, folder);
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
        }
    }
}
