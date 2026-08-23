using Wikidown.Cli;
using Wikidown.Html;
using Xunit;

namespace Wikidown.Core.Tests;

public class HtmlExporterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _wikiRoot;
    private readonly string _output;
    private readonly WikiRepository _repo;

    public HtmlExporterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "wikidown-html-" + Guid.NewGuid().ToString("N"));
        _wikiRoot = Path.Combine(_tempRoot, "MyProject", "docs");
        _output = Path.Combine(_tempRoot, "public");
        Directory.CreateDirectory(_wikiRoot);
        _repo = new WikiRepository(_wikiRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private void Seed()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\nStart at [Guides](Guides.md).\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n\n[Usage](Guides/Usage.md) · [Install](Guides/Install.md#steps)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n\n## Steps\n\n![map](../.attachments/map.png)\n\nSee <https://example.com> and [Home](../Home.md).\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Usage"), "# Usage\n\n| a | b |\n|---|---|\n| 1 | 2 |\n"));
        _repo.WriteOrder(PagePath.Parse("/Guides"), new[] { "Usage", "Install" });
        Directory.CreateDirectory(Path.Combine(_wikiRoot, ".attachments"));
        File.WriteAllBytes(Path.Combine(_wikiRoot, ".attachments", "map.png"), new byte[] { 1, 2, 3 });
    }

    private string Out(string relPath) => Path.Combine(_output, relPath.Replace('/', Path.DirectorySeparatorChar));
    private string ReadOut(string relPath) => File.ReadAllText(Out(relPath));

    [Fact]
    public void Export_WithoutScaffold_UsesEmbeddedTheme()
    {
        Seed();
        var result = HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        Assert.Equal(4, result.PageCount);
        Assert.True(File.Exists(Out("Home.html")));
        Assert.True(File.Exists(Out("Guides.html")));
        Assert.True(File.Exists(Out("Guides/Install.html")));
        Assert.True(File.Exists(Out("Guides/Usage.html")));
        Assert.True(File.Exists(Out("index.html")));
        Assert.True(File.Exists(Out("assets/wikidown.css")));
        Assert.True(File.Exists(Out(".attachments/map.png")));
        Assert.False(Directory.Exists(Out("_layouts")));
    }

    [Fact]
    public void Export_RewritesRelativeMarkdownLinks_AndLeavesOthers()
    {
        Seed();
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        var guides = ReadOut("Guides.html");
        Assert.Contains("href=\"Guides/Usage.html\"", guides);
        Assert.Contains("href=\"Guides/Install.html#steps\"", guides);

        var install = ReadOut("Guides/Install.html");
        Assert.Contains("href=\"../Home.html\"", install);
        Assert.Contains("href=\"https://example.com\"", install);
        Assert.Contains("src=\"../.attachments/map.png\"", install);
        Assert.DoesNotContain(".md\"", install);
    }

    [Fact]
    public void Export_RendersNavTreeWithActiveAndOpenState()
    {
        Seed();
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        var install = ReadOut("Guides/Install.html");
        Assert.Contains("<details open>", install);
        Assert.Contains("class=\"nav-item is-active\"", install);
        Assert.Contains(">Install</a>", install);
        Assert.True(
            install.IndexOf(">Usage</a>", StringComparison.Ordinal) < install.IndexOf(">Install</a>", StringComparison.Ordinal),
            "nav should follow .order (Usage before Install)");

        var home = ReadOut("Home.html");
        Assert.DoesNotContain("<details open>", home);
        Assert.DoesNotContain("{{", home);
        Assert.DoesNotContain("{%", home);
    }

    [Fact]
    public void Export_TitleFromHeading_AndSiteTitleFromRepoFolder()
    {
        Seed();
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));
        Assert.Contains("<title>Install · MyProject</title>", ReadOut("Guides/Install.html"));

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Title: "Docs & Things"));
        Assert.Contains("<title>Install · Docs & Things</title>", ReadOut("Guides/Install.html"));
    }

    [Fact]
    public void Export_BaseUrlPrefixesThemeLinks_NotPageBodyLinks()
    {
        Seed();
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, BaseUrl: "/myproject/"));

        var install = ReadOut("Guides/Install.html");
        Assert.Contains("href=\"/myproject/assets/wikidown.css\"", install);
        Assert.Contains("href=\"/myproject/Guides/Usage.html\"", install);
        Assert.Contains("href=\"../Home.html\"", install);
        Assert.Contains("url=/myproject/Home.html", ReadOut("index.html"));
    }

    [Fact]
    public void Export_UsesScaffoldedThemeAndConfigWhenPresent()
    {
        Seed();
        Assert.Equal(0, CommandRunner.Run(new[] { "pages", "--root", _wikiRoot, "--title", "Scaffolded" }, TextWriter.Null, TextWriter.Null));
        File.WriteAllText(Path.Combine(_wikiRoot, "assets", "wikidown.css"), "/* custom */");
        File.WriteAllText(Path.Combine(_wikiRoot, "_includes", "nav-tree.html"), "<nav>CUSTOM {{ include.items.size }}</nav>");
        var config = File.ReadAllText(Path.Combine(_wikiRoot, "_config.yml"));
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"),
            config.Replace("# repository_url: https://github.com/<owner>/<repo>", "repository_url: https://example.com/repo"));

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        Assert.Equal("/* custom */", ReadOut("assets/wikidown.css"));
        var home = ReadOut("Home.html");
        Assert.Contains("<title>Home · Scaffolded</title>", home);
        Assert.Contains("<nav>CUSTOM 2</nav>", home);
        Assert.Contains("href=\"https://example.com/repo\"", home);
    }

    [Fact]
    public void Export_CleanRemovesStaleOutput()
    {
        Seed();
        Directory.CreateDirectory(_output);
        File.WriteAllText(Out("stale.html"), "old");

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));
        Assert.True(File.Exists(Out("stale.html")));

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Clean: true));
        Assert.False(File.Exists(Out("stale.html")));
        Assert.True(File.Exists(Out("Home.html")));
    }

    [Fact]
    public void Cli_ExportHtml_RequiresOutput()
    {
        Seed();
        var err = new StringWriter();
        Assert.Equal(2, CommandRunner.Run(new[] { "export-html", "--root", _wikiRoot }, TextWriter.Null, err));
        Assert.Contains("--output", err.ToString());

        var outText = new StringWriter();
        Assert.Equal(0, CommandRunner.Run(new[] { "export-html", "--root", _wikiRoot, "--output", _output }, outText, TextWriter.Null));
        Assert.Contains("exported 4 page(s)", outText.ToString());
    }

    [Theory]
    [InlineData("Foo.md", "Foo.html")]
    [InlineData("../A/B.md#frag", "../A/B.html#frag")]
    [InlineData("#frag", "#frag")]
    [InlineData("/Foo/Bar", "/Foo/Bar")]
    [InlineData("https://x.y/z.md", "https://x.y/z.md")]
    [InlineData("../.attachments/a.png", "../.attachments/a.png")]
    public void RewriteMarkdownLink(string input, string expected) =>
        Assert.Equal(expected, MarkdownPageRenderer.RewriteMarkdownLink(input));

    [Fact]
    public void ToFluidSyntax_RewritesJekyllIncludes()
    {
        Assert.Equal(
            "{% include 'nav-tree.html', items: site.data.navigation %}",
            ThemeFiles.ToFluidSyntax("{% include nav-tree.html items=site.data.navigation %}"));
        Assert.Equal(
            "<ul>{% for i in items %}{{ i }}{% endfor %}</ul> include.items",
            ThemeFiles.ToFluidSyntax("<ul>{% for i in include.items %}{{ i }}{% endfor %}</ul> include.items"));
    }

    [Fact]
    public void SiteConfig_ParsesScalars_IgnoresNestedAndComments()
    {
        var config = SiteConfig.Parse("title: \"My \\\"Wiki\\\"\"\n# repository_url: nope\nbaseurl: /p # trailing\nkramdown:\n  input: GFM\ndescription: ''\n");
        Assert.Equal("My \"Wiki\"", config.Title);
        Assert.Null(config.RepositoryUrl);
        Assert.Equal("/p", config.BaseUrl);
        Assert.Null(config.Description);
    }
}
