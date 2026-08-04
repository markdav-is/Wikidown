namespace Wikidown.Core;

public enum IndexIssueKind
{
    // A subpage folder exists on disk but its sibling <Folder>.md (the
    // index/parent page) is missing. This makes the whole subtree invisible
    // to WikiRepository.Walk() (and therefore to wikidown list/search and
    // check-links' normal link scan too) since Walk only descends into a
    // page's subpage folder once that page has itself been discovered — so
    // this class of orphan has to be found by scanning the filesystem
    // directly, not via Walk().
    MissingParentPage,

    // The parent page exists, but its body has no link (relative or
    // absolute) resolving to this child — a reader browsing the rendered
    // page (or the raw file on GitHub) has no way to click through to it.
    ChildNotLinked,
}

public sealed record IndexIssue(PagePath Folder, PagePath? Child, IndexIssueKind Kind);

// Audits the "every folder needs an index page that links its children"
// invariant from issue #16. Complements LinkChecker (which validates links
// that already exist) by catching folders/pages that never got linked to
// or indexed at all.
public static class IndexChecker
{
    public static IEnumerable<IndexIssue> Check(WikiRepository repo)
    {
        foreach (var folder in EnumerateSubpageFolders(repo))
        {
            if (!repo.Exists(folder))
            {
                yield return new IndexIssue(folder, null, IndexIssueKind.MissingParentPage);
                continue;
            }

            var linkedFiles = ResolveLinkedFiles(repo, folder);
            foreach (var child in EnumerateChildPages(repo, folder))
            {
                var childFile = Path.GetFullPath(Path.Combine(repo.RootPath, child.ToFilePath()));
                if (!linkedFiles.Contains(childFile))
                    yield return new IndexIssue(folder, child, IndexIssueKind.ChildNotLinked);
            }
        }
    }

    // Every directory under the wiki root that could be a subpage folder —
    // found by walking the filesystem, not repo.Walk(), since Walk() can't
    // see a folder whose index page is missing.
    private static IEnumerable<PagePath> EnumerateSubpageFolders(WikiRepository repo)
    {
        if (!Directory.Exists(repo.RootPath)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(repo.RootPath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repo.RootPath, dir);
            var segments = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            if (segments.Any(s => s.StartsWith('.'))) continue; // .attachments, .git, etc.

            yield return new PagePath(segments.Select(PageName.FromFileBase).ToList());
        }
    }

    private static IEnumerable<PagePath> EnumerateChildPages(WikiRepository repo, PagePath folder)
    {
        var dir = Path.Combine(repo.RootPath, folder.ToFolderPath());
        if (!Directory.Exists(dir)) yield break;

        foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
        {
            var baseName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(baseName)) continue;
            yield return folder.Append(PageName.FromFileBase(baseName));
        }
    }

    private static HashSet<string> ResolveLinkedFiles(WikiRepository repo, PagePath page)
    {
        var markdown = repo.Read(page).Markdown;
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in LinkChecker.LinkTarget().Matches(markdown))
        {
            var file = ResolveTargetFile(repo, page, match.Groups[1].Value);
            if (file is not null) files.Add(file);
        }
        return files;
    }

    private static string? ResolveTargetFile(WikiRepository repo, PagePath page, string target)
    {
        if (target.Length == 0 || LinkChecker.IsExternal(target) || target.StartsWith('#'))
            return null;

        var hashIndex = target.IndexOf('#');
        var targetPath = hashIndex >= 0 ? target[..hashIndex] : target;
        if (targetPath.Length == 0) return null;

        if (targetPath.StartsWith('/'))
        {
            var parsed = PagePath.Parse(targetPath);
            return parsed.IsRoot
                ? null
                : Path.GetFullPath(Path.Combine(repo.RootPath, parsed.ToFilePath()));
        }

        var pageDir = Path.GetDirectoryName(page.ToFilePath()) ?? "";
        var combined = Path.Combine(repo.RootPath, pageDir, targetPath.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetFullPath(combined);
    }
}
