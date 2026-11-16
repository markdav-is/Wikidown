using Wikidown.Core;

namespace Wikidown.Web.Services;

public sealed record RemotePage(PagePath Path, string Markdown, string? Sha);

public sealed record RemoteEntry(string Name, bool IsFolder);

public sealed record CommitRequest(
    PagePath Page,
    string Markdown,
    string? ExpectedSha,
    string CommitMessage);

public sealed record CommitResult(string NewSha);

/// <summary>
/// Everything needed to render the nav tree: all page paths plus the parsed
/// .order entries per folder, keyed by the folder's link path ("/" = root).
/// </summary>
public sealed record WikiSnapshot(
    IReadOnlyList<PagePath> Pages,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Orders)
{
    public IReadOnlyList<string> OrderFor(PagePath folder) =>
        Orders.TryGetValue(folder.ToLinkPath(), out var entries) ? entries : Array.Empty<string>();
}

/// <summary>Every page and .order file of the wiki, keyed by docs-relative forward-slash path.</summary>
public sealed record WikiFiles(string? HeadId, IReadOnlyDictionary<string, byte[]> Files);

/// <summary>One file in a commit; null <see cref="Content"/> deletes it. Path is docs-relative.</summary>
public sealed record FileChange(string Path, byte[]? Content, bool IsNew);

public sealed class WikiConflictException(string message) : Exception(message);

public interface IWikiBackend
{
    WikiProvider Provider { get; }

    Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default);

    Task<RemotePage> ReadPageAsync(
        WikiConnection conn, PagePath page, CancellationToken ct = default);

    Task<WikiSnapshot> WalkAsync(
        WikiConnection conn, CancellationToken ct = default);

    Task<CommitResult> WritePageAsync(
        WikiConnection conn, CommitRequest request, CancellationToken ct = default);

    Task<WikiFiles> ReadAllAsync(
        WikiConnection conn, CancellationToken ct = default);

    /// <summary>
    /// Commits several files at once. <paramref name="expectedHead"/> is the
    /// <see cref="WikiFiles.HeadId"/> the changes were computed from; a branch
    /// that has moved on since throws <see cref="WikiConflictException"/>.
    /// </summary>
    Task CommitAsync(
        WikiConnection conn, IReadOnlyList<FileChange> changes, string message,
        string? expectedHead = null, CancellationToken ct = default);

    /// <summary>Raw bytes of a non-page file under the docs folder (images in .attachments); null if missing.</summary>
    Task<byte[]?> ReadBytesAsync(
        WikiConnection conn, string docsRelPath, CancellationToken ct = default);
}
