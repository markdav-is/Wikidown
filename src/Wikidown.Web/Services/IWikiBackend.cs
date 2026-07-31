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
}
