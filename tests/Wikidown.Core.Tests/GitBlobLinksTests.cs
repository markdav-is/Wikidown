using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class GitBlobLinksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("ssh://git@ssh.github.com:443/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("https://me@dev.azure.com/org/proj/_git/repo", "https://dev.azure.com/org/proj/_git/repo")]
    [InlineData("git@ssh.dev.azure.com:v3/org/proj/repo", "https://dev.azure.com/org/proj/_git/repo")]
    [InlineData("git@gitlab.com:group/sub/repo.git", "https://gitlab.com/group/sub/repo")]
    public void Remote_urls_map_to_the_repo_web_root(string remote, string webRoot)
    {
        Assert.Equal(webRoot, GitBlobLinks.ParseRemote(remote)?.WebRoot);
    }

    [Fact]
    public void Unknown_hosts_have_no_links()
    {
        Assert.Null(GitBlobLinks.ParseRemote("https://example.com/owner/repo.git"));
        Assert.Null(GitBlobLinks.ParseRemote(@"C:\repos\local"));
    }

    [Fact]
    public void Blob_urls_follow_each_hosts_scheme()
    {
        Assert.Equal("https://github.com/o/r/blob/main/docs/.attachments/a%20b.png",
            new GitBlobLinks(GitHostKind.GitHub, "https://github.com/o/r", "main").Blob("docs/.attachments/a b.png"));
        Assert.Equal("https://gitlab.com/o/r/-/blob/feature/x/docs/a.png",
            new GitBlobLinks(GitHostKind.GitLab, "https://gitlab.com/o/r", "feature/x").Blob("docs/a.png"));
        Assert.Equal("https://dev.azure.com/o/p/_git/r?path=/docs/a.png&version=GBmain",
            new GitBlobLinks(GitHostKind.AzureDevOps, "https://dev.azure.com/o/p/_git/r", "main").Blob("docs/a.png"));
    }

    [Fact]
    public void Find_reads_origin_and_branch_from_the_clone()
    {
        var docs = MakeClone("git@github.com:owner/repo.git", "ref: refs/heads/feature/pdf");
        var image = Path.Combine(docs, ".attachments", "pic.png");

        var links = GitBlobLinks.Find(docs);

        Assert.NotNull(links);
        Assert.Equal("https://github.com/owner/repo/blob/feature/pdf/docs/.attachments/pic.png", links.For(image));
        Assert.Null(links.For(Path.Combine(Path.GetTempPath(), "elsewhere.png")));
    }

    [Fact]
    public void Repository_source_links_resolved_images_to_the_host()
    {
        var docs = MakeClone("https://github.com/owner/repo.git", "ref: refs/heads/main");
        Directory.CreateDirectory(Path.Combine(docs, ".attachments"));
        File.WriteAllBytes(Path.Combine(docs, ".attachments", "pic.png"), [0]);
        var repo = new WikiRepository(docs);

        var blocks = MarkdownIrBuilder.Build(
            "![pic](.attachments/pic.png)\n\n![gone](nope.png)\n", PagePath.Parse("/A"),
            new RepositoryPdfPageSource(repo), allowHtmlSkip: false, out _);

        Assert.Equal("https://github.com/owner/repo/blob/main/docs/.attachments/pic.png",
            Assert.IsType<IrImage>(blocks[0]).LinkUrl);
        Assert.Null(Assert.IsType<IrImage>(blocks[1]).LinkUrl);
    }

    private string MakeClone(string origin, string head)
    {
        var git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllLines(Path.Combine(git, "config"),
        [
            "[core]",
            "\tbare = false",
            "[remote \"upstream\"]",
            "\turl = https://github.com/someone-else/repo.git",
            "[remote \"origin\"]",
            $"\turl = {origin}",
            "\tfetch = +refs/heads/*:refs/remotes/origin/*",
        ]);
        File.WriteAllText(Path.Combine(git, "HEAD"), head + Environment.NewLine);
        var docs = Path.Combine(_root, "docs");
        Directory.CreateDirectory(docs);
        return docs;
    }
}
