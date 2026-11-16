using Wikidown.Core;

namespace Wikidown.Web.Services;

/// <summary>
/// Runs structural changes (new, rename, move, delete, reorder, kanban) with
/// the same <see cref="WikiRepository"/> code the CLI uses: the wiki's pages
/// are checked out into the browser's in-memory filesystem, the change is
/// made there, and whatever differs afterwards goes back as one commit. That
/// keeps .order files, breadcrumbs and inbound links right without a second
/// implementation of those rules.
/// </summary>
public sealed class WikiWorkspace(BackendResolver backends)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Returns the action's result; commits only if it changed something.</summary>
    public async Task<T> RunAsync<T>(
        WikiConnection conn, string message, Func<WikiRepository, T> action, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            try
            {
                return await RunOnceAsync(conn, message, action, ct);
            }
            catch (WikiConflictException)
            {
                // Someone else pushed in between: redo it on top of their commit.
                return await RunOnceAsync(conn, message, action, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RunAsync(WikiConnection conn, string message, Action<WikiRepository> action, CancellationToken ct = default) =>
        RunAsync(conn, message, repo => { action(repo); return true; }, ct);

    private async Task<T> RunOnceAsync<T>(
        WikiConnection conn, string message, Func<WikiRepository, T> action, CancellationToken ct)
    {
        var backend = backends.For(conn.Provider);
        var before = await backend.ReadAllAsync(conn, ct);

        var root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var (rel, bytes) in before.Files)
            {
                var file = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllBytesAsync(file, bytes, ct);
            }

            var result = action(new WikiRepository(root));

            var changes = new List<FileChange>();
            var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                after.Add(rel);
                var bytes = await File.ReadAllBytesAsync(file, ct);
                if (!before.Files.TryGetValue(rel, out var old))
                    changes.Add(new FileChange(rel, bytes, IsNew: true));
                else if (!bytes.AsSpan().SequenceEqual(old))
                    changes.Add(new FileChange(rel, bytes, IsNew: false));
            }
            changes.AddRange(before.Files.Keys.Where(rel => !after.Contains(rel))
                .Select(rel => new FileChange(rel, null, IsNew: false)));

            if (changes.Count > 0)
                await backend.CommitAsync(conn, changes, message, before.HeadId, ct);
            return result;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
