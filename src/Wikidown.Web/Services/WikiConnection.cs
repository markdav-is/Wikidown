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
    string DocsPath)
{
    public const string DefaultDocsPath = "docs";
    public const string DefaultBranch = "main";

    public string Display => $"{Provider}: {Owner}/{Repo}@{Branch}/{DocsPath}";
}
