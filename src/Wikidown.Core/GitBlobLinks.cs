namespace Wikidown.Core;

public enum GitHostKind
{
    GitHub,
    GitLab,
    AzureDevOps,
}

/// <summary>
/// Browser URLs for files in a hosted Git repo: the web root of the repo
/// (https://github.com/owner/repo), the ref files are viewed at, and the host's
/// URL scheme for a single file.
/// </summary>
public sealed class GitBlobLinks(GitHostKind host, string webRoot, string gitRef, bool refIsCommit = false, string? localRoot = null)
{
    public string WebRoot { get; } = webRoot.TrimEnd('/');
    public string Ref { get; } = gitRef;

    /// <summary>The URL for a path relative to the repo root ("/" separators).</summary>
    public string Blob(string repoRelativePath)
    {
        var path = EscapePath(repoRelativePath.Replace('\\', '/').Trim('/'));
        return host switch
        {
            GitHostKind.GitLab => $"{WebRoot}/-/blob/{EscapePath(Ref)}/{path}",
            GitHostKind.AzureDevOps =>
                $"{WebRoot}?path=/{path}&version={(refIsCommit ? "GC" : "GB")}{Uri.EscapeDataString(Ref)}",
            _ => $"{WebRoot}/blob/{EscapePath(Ref)}/{path}",
        };
    }

    /// <summary>The URL for a file on disk inside the clone this was found from, else null.</summary>
    public string? For(string fullPath)
    {
        if (localRoot is null) return null;
        var relative = Path.GetRelativePath(localRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            return null;
        return Blob(relative);
    }

    /// <summary>
    /// Reads the origin remote and checked-out branch of the clone holding
    /// <paramref name="start"/>. Null when there's no clone, no origin, or a
    /// host whose URL scheme isn't known.
    /// </summary>
    public static GitBlobLinks? Find(string start)
    {
        try
        {
            for (var dir = Path.GetFullPath(start); dir is not null; dir = Path.GetDirectoryName(dir))
            {
                var dotGit = Path.Combine(dir, ".git");
                if (Directory.Exists(dotGit)) return FromGitDir(dir, dotGit);
                if (File.Exists(dotGit))
                {
                    // Worktrees and submodules: ".git" is a file pointing at the real git dir.
                    var line = File.ReadAllText(dotGit).Trim();
                    if (!line.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
                    return FromGitDir(dir, Path.GetFullPath(Path.Combine(dir, line["gitdir:".Length..].Trim())));
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static GitBlobLinks? FromGitDir(string workTree, string gitDir)
    {
        var commonDir = gitDir;
        var commonFile = Path.Combine(gitDir, "commondir");
        if (File.Exists(commonFile))
            commonDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim()));

        var configPath = Path.Combine(commonDir, "config");
        var headPath = Path.Combine(gitDir, "HEAD");
        if (!File.Exists(configPath) || !File.Exists(headPath)) return null;

        var remote = OriginUrl(File.ReadAllLines(configPath));
        if (remote is null || ParseRemote(remote) is not { } parsed) return null;

        var head = File.ReadAllText(headPath).Trim();
        const string branchPrefix = "ref: refs/heads/";
        var isCommit = !head.StartsWith(branchPrefix, StringComparison.Ordinal);
        var gitRef = isCommit ? head : head[branchPrefix.Length..];
        if (gitRef.Length == 0) return null;

        return new GitBlobLinks(parsed.Host, parsed.WebRoot, gitRef, isCommit, workTree);
    }

    private static string? OriginUrl(IEnumerable<string> configLines)
    {
        var inOrigin = false;
        foreach (var raw in configLines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = line.Replace(" ", "").Equals("[remote\"origin\"]", StringComparison.Ordinal);
                continue;
            }
            if (!inOrigin) continue;
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals("url", StringComparison.OrdinalIgnoreCase))
                return line[(eq + 1)..].Trim();
        }
        return null;
    }

    /// <summary>
    /// A remote URL (https, ssh://, or scp-style git@host:path) to the repo's
    /// web root and host kind, or null for hosts with no known file URL scheme.
    /// </summary>
    public static (GitHostKind Host, string WebRoot)? ParseRemote(string remote)
    {
        string host, path;
        if (Uri.TryCreate(remote, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            host = uri.Host;
            path = uri.AbsolutePath;
        }
        else
        {
            var at = remote.IndexOf('@');
            var colon = remote.IndexOf(':', at + 1);
            if (colon < 0) return null;
            host = remote[(at + 1)..colon];
            path = remote[(colon + 1)..];
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        host = host.ToLowerInvariant();

        if (host == "github.com" || host.EndsWith(".github.com", StringComparison.Ordinal))
            return (GitHostKind.GitHub, $"https://github.com/{path}");
        if (host.Contains("gitlab", StringComparison.Ordinal))
            return (GitHostKind.GitLab, $"https://{host}/{path}");
        if (host == "dev.azure.com")
            return (GitHostKind.AzureDevOps, $"https://dev.azure.com/{path}");
        if (host == "ssh.dev.azure.com")
        {
            // git@ssh.dev.azure.com:v3/org/project/repo
            var parts = path.Split('/');
            return parts.Length == 4 && parts[0] == "v3"
                ? (GitHostKind.AzureDevOps, $"https://dev.azure.com/{parts[1]}/{parts[2]}/_git/{parts[3]}")
                : null;
        }
        return null;
    }

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
