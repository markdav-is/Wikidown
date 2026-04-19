using Wikidown.Core;

namespace Wikidown.Web.Services;

public sealed record RemotePage(PagePath Path, string Markdown, string? Sha);

public sealed record RemoteEntry(string Name, bool IsFolder);

public interface IWikiBackend
{
    WikiProvider Provider { get; }

    Task<IReadOnlyList<RemoteEntry>> ListFolderAsync(
        WikiConnection conn, string folderRelPath, CancellationToken ct = default);

    Task<RemotePage> ReadPageAsync(
        WikiConnection conn, PagePath page, CancellationToken ct = default);

    Task<IReadOnlyList<PagePath>> WalkAsync(
        WikiConnection conn, CancellationToken ct = default);
}
