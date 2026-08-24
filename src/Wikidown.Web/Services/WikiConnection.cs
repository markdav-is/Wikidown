namespace Wikidown.Web.Services;

public enum WikiProvider
{
    GitHub,
    AzureDevOps,
    GitLab,
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
        WikiProvider.GitLab when !string.IsNullOrWhiteSpace(Host) =>
            $"GitLab ({new Uri(Host).Host}): {Owner}/{Repo}@{Branch}/{DocsPath}",
        _ => $"{Provider}: {Owner}/{Repo}@{Branch}/{DocsPath}",
    };
}
