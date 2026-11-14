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
    public void Export_RewritesMarkdownLinksInRawHtmlAndSvg()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Wavir"),
            "# Wavir\n\n" +
            "<a href=\"Wavir/Balic.md\">Balic</a>\n\n" +
            "<svg viewBox=\"0 0 10 10\" xmlns=\"http://www.w3.org/2000/svg\">\n" +
            "  <image href=\".attachments/atlas.png\" width=\"10\" height=\"10\"/>\n" +
            "  <a href=\"Wavir/Balic.md#pin\"><title>Balic</title><circle r=\"1\"/></a>\n" +
            "  <a xlink:href='Wavir/Old.md'><circle r=\"1\"/></a>\n" +
            "  <a href=\"https://x.y/z.md\"><circle r=\"1\"/></a>\n" +
            "  <a href=\"mailto:a@b.md\"><circle r=\"1\"/></a>\n" +
            "</svg>\n\n" +
            "Write `<a href=\"Code.md\">` to link.\n"));

        var html = MarkdownPageRenderer.Render(_repo.Read(PagePath.Parse("/Wavir")).Markdown, "Wavir").Html;

        Assert.Contains("<a href=\"Wavir/Balic.html\">Balic</a>", html);
        Assert.Contains("<a href=\"Wavir/Balic.html#pin\">", html);
        Assert.Contains("<a xlink:href='Wavir/Old.html'>", html);
        Assert.Contains("href=\"https://x.y/z.md\"", html);
        Assert.Contains("href=\"mailto:a@b.md\"", html);
        Assert.Contains("<image href=\".attachments/atlas.png\"", html);
        Assert.Contains("&lt;a href=&quot;Code.md&quot;&gt;", html);
    }

    [Fact]
    public void Render_KeepsSvgWithBlankLinesAsOneHtmlBlock()
    {
        var markdown = "# Map\r\n\r\n<svg viewBox=\"0 0 10 10\">\r\n  <image href=\"a.png\"/>\r\n\r\n" +
            "  <a href=\"B.md\"><circle r=\"1\"/></a>\r\n   \r\n</svg>\r\n\r\n```html\r\n<svg>\r\n\r\n</svg>\r\n```\r\n";

        var html = MarkdownPageRenderer.Render(markdown, "Map").Html;

        Assert.DoesNotContain("<p>", html);
        Assert.Contains("<a href=\"B.html\"><circle r=\"1\"/></a>", html);
        Assert.Contains("&lt;svg&gt;\n\n&lt;/svg&gt;", html.Replace("\r", ""));
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
        Assert.Equal(0, CommandRunner.Run(new[] { "pages", "--root", _wikiRoot, "--title", "Scaffolded", "--theme" }, TextWriter.Null, TextWriter.Null));
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
    public void Export_ExcludeFromSite_SkipsSubtreesInPagesAndNav()
    {
        Seed();
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"),
            "title: T\nwikidown:\n  exclude_from_site:\n    - /Guides\n");
        var result = HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Clean: true));

        Assert.Equal(1, result.PageCount);
        Assert.True(File.Exists(Out("Home.html")));
        Assert.False(File.Exists(Out("Guides.html")));
        Assert.False(File.Exists(Out("Guides/Install.html")));

        var home = ReadOut("Home.html");
        var sidebar = home[home.IndexOf("<aside", StringComparison.Ordinal)..home.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.DoesNotContain("Guides", sidebar);
        Assert.Contains(">Home</a>", sidebar);
    }

    [Fact]
    public void Export_WithUrlAndImage_EmitsAbsoluteLinkPreviewTags()
    {
        Seed();
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"),
            "title: T\ndescription: D\nurl: https://example.org/\nimage: /images/logo.png\n");
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, BaseUrl: "/wiki", Clean: true));

        var page = ReadOut("Guides/Install.html");
        Assert.Contains("<meta property=\"og:title\" content=\"Install\">", page);
        Assert.Contains("<meta property=\"og:url\" content=\"https://example.org/wiki/Guides/Install.html\">", page);
        Assert.Contains("<meta property=\"og:image\" content=\"https://example.org/wiki/images/logo.png\">", page);
        Assert.Contains("twitter:card", page);
    }

    [Fact]
    public void Export_WithoutUrl_OmitsAbsoluteLinkPreviewTags()
    {
        Seed();
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"), "title: T\nimage: /images/logo.png\n");
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Clean: true));

        var page = ReadOut("Home.html");
        Assert.Contains("og:title", page);
        Assert.DoesNotContain("og:image", page);
        Assert.DoesNotContain("og:url", page);
    }

    [Fact]
    public void Export_NavTreeLinksHomeToSiteRoot_AndMarksItActive()
    {
        Seed();
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        var home = ReadOut("Home.html");
        var sidebar = home[home.IndexOf("<aside", StringComparison.Ordinal)..home.IndexOf("</aside>", StringComparison.Ordinal)];
        Assert.Contains("href=\"/\"", sidebar);
        Assert.DoesNotContain("Home.html", sidebar);
        Assert.Contains("nav-item is-active", sidebar);

        var guides = ReadOut("Guides.html");
        Assert.Contains("href=\"/\"", guides[guides.IndexOf("<aside", StringComparison.Ordinal)..guides.IndexOf("</aside>", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Export_CopiesRootStaticFiles_NotSourcesOrJekyllMachinery()
    {
        Seed();
        File.WriteAllText(Path.Combine(_wikiRoot, "install.sh"), "#!/bin/sh");
        Directory.CreateDirectory(Path.Combine(_wikiRoot, "images"));
        File.WriteAllBytes(Path.Combine(_wikiRoot, "images", "logo.png"), new byte[] { 9 });
        File.WriteAllText(Path.Combine(_wikiRoot, "Gemfile"), "gems");
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"), "title: T\n");
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Clean: true));

        Assert.True(File.Exists(Out("install.sh")));
        Assert.True(File.Exists(Out("images/logo.png")));
        Assert.True(File.Exists(Out(".attachments/map.png")));
        Assert.False(File.Exists(Out("Gemfile")));
        Assert.False(File.Exists(Out("_config.yml")));
        Assert.False(File.Exists(Out("Home.md")));
        Assert.False(File.Exists(Out(".order")));
    }

    [Fact]
    public void Export_FaviconFromConfig_RendersLinkTag()
    {
        Seed();
        File.WriteAllText(Path.Combine(_wikiRoot, "_config.yml"), "title: T\nfavicon: /favicon.ico\n");
        HtmlExporter.Export(_repo, new HtmlExportOptions(_output, Clean: true, BaseUrl: "/p"));
        Assert.Contains("<link rel=\"icon\" href=\"/p/favicon.ico\">", ReadOut("Home.html"));
    }

    [Fact]
    public void PublishExclusions_ParseAndMatch()
    {
        var list = PublishExclusions.Parse(
            "wikidown:\n  exclude_from_site:\n    - /Meta\n    - \"/Testing\"\n  other: x\nexclude:\n  - vendor\n");
        Assert.Equal(new[] { "/Meta", "/Testing" }, list);
        Assert.True(PublishExclusions.IsExcluded(PagePath.Parse("/Meta/Notes"), list));
        Assert.True(PublishExclusions.IsExcluded(PagePath.Parse("/Testing"), list));
        Assert.False(PublishExclusions.IsExcluded(PagePath.Parse("/Meta-Data"), list));
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

    private void WriteWikiFile(string relPath, string text)
    {
        var path = Path.Combine(_wikiRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    // A layout that renders only the given Liquid, so assertions see just it.
    private void ProbeLayout(string liquid) => WriteWikiFile("_layouts/wikidown.html", liquid);

    [Fact]
    public void Export_LoadsDataFolder_JsonAndYaml_FoldersAsNestedKeys()
    {
        Seed();
        WriteWikiFile("_data/site.json", "{ \"currentHop\": \"2026-11\", \"social\": { \"instagram\": \"https://ig.example/x\" } }");
        WriteWikiFile("_data/hops/2026-11.json", "{ \"date\": \"2026-11-07\", \"venues\": [ { \"page\": \"Guides/Usage.md\", \"stop\": 1 } ] }");
        WriteWikiFile("_data/team.yml", "- name: Ann\n  active: yes\n  age: 40\n- name: \"Bo\"\n");
        WriteWikiFile("_data/navigation.yml", "- title: Stale\n  url: /Stale.html\n");
        ProbeLayout(
            "{% assign hop = site.data.hops[site.data.site.currentHop] %}" +
            "[{{ site.data.social.instagram }}{{ site.data.site.social.instagram }}]" +
            "[{{ hop.date | date: \"%B\" }} {{ hop.venues[0].stop }}]" +
            "[{{ site.data.team[0].name }} {{ site.data.team[0].active }} {{ site.data.team[0].age | plus: 1 }} {{ site.data.team[1].name }}]" +
            "[{{ site.data.navigation | map: \"title\" | join: \",\" }}]");

        var result = HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        var html = ReadOut("Home.html");
        Assert.Contains("[https://ig.example/x]", html);
        Assert.Contains("[November 1]", html);
        Assert.Contains("[Ann true 41 Bo]", html);
        Assert.Contains("[Home,Guides]", html);
        Assert.Contains(result.Warnings, w => w.StartsWith("_data/navigation.yml is ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_BadDataFile_NamesTheFile()
    {
        Seed();
        WriteWikiFile("_data/broken.json", "{ nope");
        var e = Assert.Throws<InvalidOperationException>(() => HtmlExporter.Export(_repo, new HtmlExportOptions(_output)));
        Assert.Contains("_data/broken.json", e.Message);
    }

    [Fact]
    public void Export_SitePagesCarryContent_AndJekyllFiltersBehave()
    {
        Seed();
        WriteWikiFile("_data/tiers.json", "[ { \"price\": 10 }, { \"price\": 3 }, { \"price\": 5 } ]");
        ProbeLayout(
            "{% assign p = site.pages | where: \"path\", \"Guides/Usage.md\" | first %}" +
            "[{{ p.title }}|{{ p.url }}|{{ p.content | strip_html | normalize_whitespace | truncatewords: 5 }}]" +
            "[{{ site.data.tiers | map: \"price\" | sort | join: \",\" }}]" +
            "[{{ site.data.tiers | sort: \"price\" | map: \"price\" | first }}]" +
            "[{{ \"Hello  World\" | slugify }}|{{ \"a\n\n b\" | normalize_whitespace }}|{{ \"*x*\" | markdownify | strip }}]" +
            "[{{ site.data.tiers[1] | jsonify }}]");

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        var html = ReadOut("Home.html");
        Assert.Contains("[Usage|/Guides/Usage.html|Home / Guides / Usage...]", html);
        Assert.Contains("[3,5,10]", html);
        Assert.Contains("[3]", html);
        Assert.Contains("[hello-world|a b|<p><em>x</em></p>]", html);
        Assert.Contains("[{\"price\":3}]", html);
    }

    [Fact]
    public void WikiBody_DropsBreadcrumb_OptionallyTitle_AndMakesLinksSiteAbsolute()
    {
        Seed();
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"),
            Breadcrumb.Render(_repo, PagePath.Parse("/Guides/Install")) + "\n\n# Install\n\nSee [Usage](Usage.md) and ![map](../.attachments/map.png).\n"));
        ProbeLayout(
            "{% if page.path == \"Home.md\" %}" +
            "<full>{{ \"Guides/Install.md\" | wiki_body }}</full>" +
            "<bare>{{ \"/Guides/Install\" | wiki_body: \"strip_title\" }}</bare>" +
            "<obj>{% assign g = site.pages | where: \"path\", \"Guides.md\" | first %}{{ g | wiki_title }}</obj>" +
            "<t>{{ \"Guides/Install.md\" | wiki_title }}</t>" +
            "<none>{{ \"Nope.md\" | wiki_body }}</none>" +
            "{% endif %}");

        var result = HtmlExporter.Export(_repo, new HtmlExportOptions(_output, BaseUrl: "/base"));

        var html = ReadOut("Home.html");
        var full = html[html.IndexOf("<full>")..html.IndexOf("</full>")];
        Assert.DoesNotContain("wikidown:breadcrumb", full);
        Assert.Contains("<h1 id=\"install\">Install</h1>", full);
        Assert.Contains("href=\"/base/Guides/Usage.html\"", full);
        Assert.Contains("src=\"/base/.attachments/map.png\"", full);
        var bare = html[html.IndexOf("<bare>")..html.IndexOf("</bare>")];
        Assert.DoesNotContain("<h1", bare);
        Assert.Contains("See <a", bare);
        Assert.Contains("<obj>Guides</obj>", html);
        Assert.Contains("<t>Install</t>", html);
        Assert.Contains("<none></none>", html);
        Assert.Contains(result.Warnings, w => w.Contains("Nope.md"));
    }

    [Fact]
    public void Export_LayoutsFromConfig_ExactAndSubtree_WithParentLayouts()
    {
        Seed();
        WriteWikiFile("_config.yml", "title: T\nwikidown:\n  layouts:\n    /Home: landing\n    Guides/*: guide\n");
        WriteWikiFile("_layouts/base.html", "<base-layout>{{ content }}</base-layout>");
        WriteWikiFile("_layouts/landing.html", "---\nlayout: base\n---\n<landing>{{ page.title }}</landing>");
        WriteWikiFile("_layouts/guide.html", "<guide>{{ content }}</guide>");

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        Assert.Equal("<base-layout><landing>Home</landing></base-layout>", ReadOut("Home.html"));
        Assert.StartsWith("<guide>", ReadOut("Guides/Install.html"));
        Assert.StartsWith("<guide>", ReadOut("Guides/Usage.html"));
        Assert.Contains("class=\"shell\"", ReadOut("Guides.html"));
    }

    [Fact]
    public void Export_MissingLayout_Throws()
    {
        Seed();
        WriteWikiFile("_config.yml", "title: T\nwikidown:\n  layouts: { /Home: nowhere }\n");
        var e = Assert.Throws<InvalidOperationException>(() => HtmlExporter.Export(_repo, new HtmlExportOptions(_output)));
        Assert.Contains("_layouts/nowhere.html", e.Message);
    }

    [Fact]
    public void Export_RootPage_RendersAsIndex_InsteadOfRedirect()
    {
        Seed();
        WriteWikiFile("_config.yml", "title: T\nwikidown:\n  root_page: /Guides\n");
        ProbeLayout("<p>{{ page.title }} at {{ page.url }}</p>{% for n in site.data.navigation %}[{{ n.title }} {{ n.url }}]{% endfor %}");

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        Assert.StartsWith("<p>Guides at /</p>", ReadOut("index.html"));
        Assert.Equal(ReadOut("Guides.html"), ReadOut("index.html"));
        Assert.Contains("[Home /Home.html][Guides /]", ReadOut("Home.html"));
    }

    [Fact]
    public void Export_RootPage_MustBePublished()
    {
        Seed();
        WriteWikiFile("_config.yml", "title: T\nwikidown:\n  root_page: /Missing\n");
        Assert.Throws<InvalidOperationException>(() => HtmlExporter.Export(_repo, new HtmlExportOptions(_output)));
    }

    [Fact]
    public void Export_RootIndexHtml_GetsPageObject_AndFrontMatter()
    {
        Seed();
        WriteWikiFile("index.html", "---\nlayout: none\ntitle: Landing\n---\n<p>{{ page.url }}|{{ page.title }}|{{ \"Home.md\" | wiki_title }}</p>");

        HtmlExporter.Export(_repo, new HtmlExportOptions(_output));

        Assert.Equal("<p>/|Landing|Home</p>", ReadOut("index.html"));
    }

    [Fact]
    public void SiteConfig_ParsesWikidownLayoutsAndRootPage_AndExposesAllKeys()
    {
        var config = SiteConfig.Parse("title: T\ncustom:\n  n: 2\nwikidown:\n  root_page: Home.md\n  layouts: { /Map: map, Venues/*: venue }\n");
        Assert.Equal("/Home", config.RootPage);
        Assert.Equal("map", config.LayoutFor("/Map"));
        Assert.Equal("venue", config.LayoutFor("/Venues/Pinebox"));
        Assert.Equal("venue", config.LayoutFor("/Venues/Pinebox/Notes"));
        Assert.Equal("wikidown", config.LayoutFor("/Venues"));
        Assert.Equal(2L, ((Dictionary<string, object?>)config.Values["custom"]!)["n"]);
    }
}
