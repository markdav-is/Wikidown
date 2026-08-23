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
        _repo = new WikiRepository(_wikiRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_repoRoot)!, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(params string[] extra)
    {
        var args = new[] { "pages", "--root", _wikiRoot }.Concat(extra).ToArray();
        return CommandRunner.Run(args, TextWriter.Null, TextWriter.Null);
    }

    private string At(string relPath) =>
        Path.Combine(_wikiRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

    private string ReadAt(string relPath) => File.ReadAllText(At(relPath));

    private void Seed()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n\n[Usage](Guides/Usage.md) · [Install](Guides/Install.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Usage"), "# Usage\n"));
        _repo.WriteOrder(PagePath.Parse("/Guides"), new[] { "Usage", "Install" });
    }

    [Fact]
    public void Pages_ScaffoldsThemeAndNavigation()
    {
        Seed();
        Assert.Equal(0, Run());

        Assert.True(File.Exists(At("_config.yml")));
        Assert.True(File.Exists(At("index.html")));
        Assert.True(File.Exists(At("_layouts/wikidown.html")));
        Assert.True(File.Exists(At("_includes/nav-tree.html")));
        Assert.True(File.Exists(At("assets/wikidown.css")));
        Assert.True(File.Exists(At("_data/navigation.yml")));
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
        Run();
        Assert.Contains("'/Zeta.html' | relative_url", ReadAt("index.html"));
        Assert.DoesNotContain("{{HOME}}", ReadAt("index.html"));

        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n"));
        Run("--force");
        Assert.Contains("'/Home.html' | relative_url", ReadAt("index.html"));
    }

    [Fact]
    public void Pages_NavigationHonorsOrderFiles()
    {
        Seed();
        Run();
        var yaml = ReadAt("_data/navigation.yml");
        Assert.Equal(
            "- title: \"Home\"\n" +
            "  url: \"/Home.html\"\n" +
            "- title: \"Guides\"\n" +
            "  url: \"/Guides.html\"\n" +
            "  prefix: \"/Guides/\"\n" +
            "  children:\n" +
            "    - title: \"Usage\"\n" +
            "      url: \"/Guides/Usage.html\"\n" +
            "    - title: \"Install\"\n" +
            "      url: \"/Guides/Install.html\"\n",
            yaml[yaml.IndexOf("- title", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void Pages_DoesNotOverwriteEditedThemeWithoutForce()
    {
        Seed();
        Run();
        File.WriteAllText(At("assets/wikidown.css"), "/* mine */");
        Run();
        Assert.Equal("/* mine */", ReadAt("assets/wikidown.css"));
        Run("--force");
        Assert.NotEqual("/* mine */", ReadAt("assets/wikidown.css"));
    }

    [Fact]
    public void Pages_NavigationRegeneratesOnRepositoryChanges()
    {
        Seed();
        Run();
        Assert.DoesNotContain("Release Notes", ReadAt("_data/navigation.yml"));

        _repo.Write(new WikiPage(PagePath.Parse("/Release-Notes"), "# Release Notes\n"));
        Assert.Contains("- title: \"Release Notes\"", ReadAt("_data/navigation.yml"));

        _repo.Move(PagePath.Parse("/Release-Notes"), PagePath.Parse("/Guides/Release-Notes"));
        Assert.Contains("url: \"/Guides/Release-Notes.html\"", ReadAt("_data/navigation.yml"));

        _repo.WriteOrder(PagePath.Parse("/Guides"), new[] { "Release-Notes", "Usage", "Install" });
        var yaml = ReadAt("_data/navigation.yml");
        Assert.True(yaml.IndexOf("Release Notes", StringComparison.Ordinal) < yaml.IndexOf("Usage", StringComparison.Ordinal));

        _repo.Delete(PagePath.Parse("/Guides/Release-Notes"));
        Assert.DoesNotContain("Release Notes", ReadAt("_data/navigation.yml"));
    }

    [Fact]
    public void Pages_NavigationNotCreatedForWikisThatNeverOptedIn()
    {
        Seed();
        Assert.False(File.Exists(At("_data/navigation.yml")));
    }

    [Fact]
    public void Pages_ScaffoldedFoldersDoNotTripIndexCheck()
    {
        Seed();
        Run();
        Assert.Empty(IndexChecker.Check(_repo));
    }

    [Fact]
    public void Navigation_EmptyWikiRendersEmptyList()
    {
        Assert.EndsWith("[]\n", JekyllNavigation.Render(_repo));
    }
}
