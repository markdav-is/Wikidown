using Wikidown.Core;

namespace Wikidown.Web.Services;

public enum WikiProvider
{
    GitHub,
    AzureDevOps,
    GitLab,
    Demo,
}

public sealed record WikiConnection(
    WikiProvider Provider,
    string Token,
    string Owner,
    string Repo,
    string Branch,
    string DocsPath,
    string Project = "",
    string Host = "")
{
    public const string DefaultDocsPath = "docs";
    public const string DefaultBranch = "main";

    public string Display => Provider switch
    {
        WikiProvider.AzureDevOps => $"ADO: {Owner}/{Project}/{Repo}@{Branch}/{DocsPath}",
        WikiProvider.Demo => "Sample wiki (this browser only)",
        WikiProvider.GitLab when !string.IsNullOrWhiteSpace(Host) =>
            $"GitLab ({new Uri(Host).Host}): {Owner}/{Repo}@{Branch}/{DocsPath}",
        _ => $"{Provider}: {Owner}/{Repo}@{Branch}/{DocsPath}",
    };

    /// <summary>The short name shown in the app bar and lists.</summary>
    public string Label => Provider switch
    {
        WikiProvider.AzureDevOps => $"{Owner}/{Project}/{Repo}",
        WikiProvider.Demo => "Sample wiki",
        _ => $"{Owner}/{Repo}",
    };

    public GitBlobLinks BlobLinks => Provider switch
    {
        WikiProvider.AzureDevOps => new(GitHostKind.AzureDevOps,
            $"https://dev.azure.com/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Project)}/_git/{Uri.EscapeDataString(Repo)}",
            Branch),
        WikiProvider.GitLab => new(GitHostKind.GitLab,
            $"{(string.IsNullOrWhiteSpace(Host) ? "https://gitlab.com" : Host.TrimEnd('/'))}/{Owner.Trim('/')}/{Repo.Trim('/')}",
            Branch),
        _ => new(GitHostKind.GitHub, $"https://github.com/{Owner}/{Repo}", Branch),
    };

    /// <summary>A browser URL for a file given relative to <see cref="DocsPath"/>.</summary>
    public string DocsBlobUrl(string docsRelativePath) =>
        BlobLinks.Blob(string.Join('/', new[] { DocsPath.Trim('/'), docsRelativePath.Trim('/') }.Where(s => s.Length > 0)));
}
