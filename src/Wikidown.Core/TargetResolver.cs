namespace Wikidown.Core;

public sealed record ResolvedTarget(string FullPath, bool Exists, bool InsideWiki);

// Where a link or image target written on a page points on disk. A leading
// "/" means the wiki root when the file is there (the Azure DevOps
// /.attachments convention), else the project root, which is how GitHub
// reads it. The project root is the Git work tree holding the wiki, else the
// wiki's parent folder.
public sealed class TargetResolver
{
    public const string RepoCopies = ".attachments/from-repo";
    public const string ExternalCopies = ".attachments/from-external";

    private readonly WikiRepository _repo;

    public TargetResolver(WikiRepository repo)
    {
        _repo = repo;
        ProjectRoot = FindGitRoot(repo.RootPath)
            ?? Path.GetDirectoryName(repo.RootPath)
            ?? repo.RootPath;
    }

    public string ProjectRoot { get; }

    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>Null for targets that aren't files on disk: URLs, anchors, data: URIs.</summary>
    public ResolvedTarget? Resolve(PagePath page, string target)
    {
        var path = target.Split('#')[0].Trim();
        if (path.Length == 0 || LinkChecker.IsExternal(path)
            || path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile ? Make(uri.LocalPath) : null;

        if (path.StartsWith('/'))
        {
            var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var inWiki = Path.GetFullPath(Path.Combine(_repo.RootPath, relative));
            if (File.Exists(inWiki)) return Make(inWiki);
            var inProject = Path.GetFullPath(Path.Combine(ProjectRoot, relative));
            return Make(File.Exists(inProject) ? inProject : inWiki);
        }

        if (Path.IsPathFullyQualified(path)) return Make(Path.GetFullPath(path));

        var pageDir = Path.GetDirectoryName(page.ToFilePath()) ?? string.Empty;
        return Make(Path.GetFullPath(Path.Combine(
            _repo.RootPath, pageDir, path.Replace('/', Path.DirectorySeparatorChar))));
    }

    public bool IsInsideWiki(string fullPath) => IsBelow(_repo.RootPath, fullPath);

    /// <summary>
    /// Where check-links --fix keeps its copy of an outside file, relative to
    /// the wiki root with "/" separators: the source's path from the project
    /// root under from-repo, or its full path minus the drive under from-external.
    /// </summary>
    public string CopyPathFor(string sourceFullPath)
    {
        if (IsBelow(ProjectRoot, sourceFullPath))
            return RepoCopies + "/" + Slashes(Path.GetRelativePath(ProjectRoot, sourceFullPath));

        var root = Path.GetPathRoot(sourceFullPath) ?? "";
        var unc = root.StartsWith(@"\\", StringComparison.Ordinal) ? Slashes(root.Trim('\\', '/')) + "/" : "";
        return ExternalCopies + "/" + unc + Slashes(sourceFullPath[root.Length..].TrimStart('\\', '/'));
    }

    /// <summary>The source a managed copy mirrors, or null for copies under from-external.</summary>
    public string? SourceForCopy(string copyFullPath)
    {
        var relative = Slashes(Path.GetRelativePath(_repo.RootPath, copyFullPath));
        var prefix = RepoCopies + "/";
        if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetFullPath(Path.Combine(ProjectRoot, relative[prefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
    }

    private ResolvedTarget Make(string fullPath) =>
        new(fullPath, File.Exists(fullPath), IsInsideWiki(fullPath));

    private static bool IsBelow(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static string Slashes(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    // .git is a directory in a normal clone and a file in a worktree or submodule.
    private static string? FindGitRoot(string start)
    {
        for (var dir = start; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var git = Path.Combine(dir, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return dir;
        }
        return null;
    }
}
