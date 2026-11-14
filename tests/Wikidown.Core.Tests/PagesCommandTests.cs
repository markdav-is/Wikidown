using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

public class PagesCommandTests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _wikiRoot;
    private readonly WikiRepository _repo;

    public PagesCommandTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), "wikidown-pages-" + Guid.NewGuid().ToString("N"), "MyProject");
        _wikiRoot = Path.Combine(_repoRoot, "docs");
        Directory.CreateDirectory(_wikiRoot);
        _repo = new WikiRepository(_wikiRoot, LineEndings.Lf);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_repoRoot)!, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(params string[] extra) => Run(TextWriter.Null, extra);

    private int Run(TextWriter stdout, params string[] extra)
    {
        var args = new[] { "pages", "--root", _wikiRoot }.Concat(extra).ToArray();
        return CommandRunner.Run(args, stdout, TextWriter.Null);
    }

    private string At(string relPath) =>
        Path.Combine(_wikiRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

    private string ReadAt(string relPath) => File.ReadAllText(At(relPath));

    private string Workflow(string root) =>
        File.ReadAllText(Path.Combine(root, ".github", "workflows", "pages.yml"));

    private void Seed()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n\n[Usage](Guides/Usage.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Usage"), "# Usage\n"));
    }

    [Fact]
    public void Pages_WritesWorkflowAndConfig_NotTheTheme()
    {
        Seed();
        Assert.Equal(0, Run());

        Assert.True(File.Exists(At("_config.yml")));
        Assert.DoesNotContain("jekyll", ReadAt("_config.yml"), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(At("_layouts/wikidown.html")));
        Assert.False(File.Exists(At("index.html")));
        Assert.False(File.Exists(At("_data/navigation.yml")));

        var workflow = Workflow(_repoRoot);
        Assert.Contains("uses: markdav-is/Wikidown/.github/workflows/wiki-pages.yml@main", workflow);
        Assert.Contains("root: docs\n", workflow);
        Assert.Contains("pages: write", workflow);
    }

    [Fact]
    public void Pages_PutsWorkflowAtTheGitRoot_WithTheWikiPathRelativeToIt()
    {
        var gitRoot = Path.GetDirectoryName(_repoRoot)!;
        Directory.CreateDirectory(Path.Combine(gitRoot, ".git"));
        Seed();

        Run();

        Assert.Contains("root: MyProject/docs\n", Workflow(gitRoot));
        Assert.False(File.Exists(Path.Combine(_repoRoot, ".github", "workflows", "pages.yml")));
    }

    [Fact]
    public void Pages_Theme_CopiesLayoutsIncludesAssetsAndIndex()
    {
        Seed();
        Run("--theme");

        Assert.True(File.Exists(At("_layouts/wikidown.html")));
        Assert.True(File.Exists(At("_includes/nav-tree.html")));
        Assert.True(File.Exists(At("assets/wikidown.css")));
        Assert.Contains("'/Home.html' | relative_url", ReadAt("index.html"));
    }

    [Fact]
    public void Pages_TitleDefaultsToRepoFolderName_AndHonorsOverride()
    {
        Seed();
        Run();
        Assert.Contains("title: \"MyProject\"", ReadAt("_config.yml"));

        Run("--force", "--title", "My \"Wiki\"");
        Assert.Contains("title: \"My \\\"Wiki\\\"\"", ReadAt("_config.yml"));
    }

    [Fact]
    public void Pages_IndexRedirectsToHome_OrFirstPageWhenNoHome()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Zeta"), "# Zeta\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Alpha"), "# Alpha\n"));
        _repo.WriteOrder(PagePath.Root, new[] { "Zeta", "Alpha" });
        Run("--theme");
        Assert.Contains("'/Zeta.html' | relative_url", ReadAt("index.html"));
        Assert.DoesNotContain("{{HOME}}", ReadAt("index.html"));
    }

    [Fact]
    public void Pages_DoesNotOverwriteExistingFilesWithoutForce()
    {
        Seed();
        Run("--theme");
        File.WriteAllText(At("assets/wikidown.css"), "/* mine */");
        var workflow = Path.Combine(_repoRoot, ".github", "workflows", "pages.yml");
        File.WriteAllText(workflow, "# mine");

        Run("--theme");
        Assert.Equal("/* mine */", ReadAt("assets/wikidown.css"));
        Assert.Equal("# mine", File.ReadAllText(workflow));

        Run("--theme", "--force");
        Assert.NotEqual("/* mine */", ReadAt("assets/wikidown.css"));
        Assert.NotEqual("# mine", File.ReadAllText(workflow));
    }

    [Fact]
    public void Pages_PointsOutJekyllLeftovers()
    {
        Seed();
        Directory.CreateDirectory(At("_data"));
        File.WriteAllText(At("_data/navigation.yml"), "- title: Home\n");
        File.WriteAllText(At("Gemfile"), "source 'https://rubygems.org'\n");
        File.WriteAllText(At("_config.yml"), "title: X\nplugins:\n  - jekyll-relative-links\n");

        var output = new StringWriter();
        Run(output);

        Assert.Contains("_data/navigation.yml", output.ToString());
        Assert.Contains("Gemfile", output.ToString());
        Assert.Contains("_config.yml still has Jekyll settings", output.ToString());
    }

    [Fact]
    public void Pages_RepositoryChangesNoLongerWriteNavigationData()
    {
        Seed();
        Run();
        _repo.Write(new WikiPage(PagePath.Parse("/Release-Notes"), "# Release Notes\n"));
        Assert.False(File.Exists(At("_data/navigation.yml")));
    }

    [Fact]
    public void Pages_ScaffoldedFoldersDoNotTripIndexCheck()
    {
        Seed();
        Run("--theme");
        Assert.Empty(IndexChecker.Check(_repo));
    }
}
