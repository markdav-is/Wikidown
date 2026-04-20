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

public sealed class WikiConflictException(string message) : Exception(message);

public interface IWikiBackend
{
    WikiProvider Provider { get; }

    Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default);

    Task<RemotePage> ReadPageAsync(
        WikiConnection conn, PagePath page, CancellationToken ct = default);

    Task<IReadOnlyList<PagePath>> WalkAsync(
        WikiConnection conn, CancellationToken ct = default);

    Task<CommitResult> WritePageAsync(
        WikiConnection conn, CommitRequest request, CancellationToken ct = default);
}
