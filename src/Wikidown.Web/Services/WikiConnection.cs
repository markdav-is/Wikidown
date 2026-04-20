namespace Wikidown.Web.Services;

public enum WikiProvider
{
    GitHub,
    AzureDevOps,
}

public sealed record WikiConnection(
    WikiProvider Provider,
    string Token,
    string Owner,
    string Repo,
    string Branch,
    string DocsPath,
    string Project = "")
{
    public const string DefaultDocsPath = "docs";
    public const string DefaultBranch = "main";

    public string Display => Provider switch
    {
        WikiProvider.AzureDevOps => $"ADO: {Owner}/{Project}/{Repo}@{Branch}/{DocsPath}",
        _ => $"{Provider}: {Owner}/{Repo}@{Branch}/{DocsPath}",
    };
}
