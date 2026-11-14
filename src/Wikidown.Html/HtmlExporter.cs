using Fluid;
using Fluid.Values;
using Wikidown.Core;

namespace Wikidown.Html;

public sealed record HtmlExportOptions(
    string OutputDirectory,
    string? Title = null,
    string? BaseUrl = null,
    bool Clean = false);

// OutsideWiki: references to files outside the wiki root, which the export
// can't ship (check-links --fix copies them in). Warnings: template
// references to pages that aren't published.
public sealed record HtmlExportResult(
    int PageCount,
    string OutputDirectory,
    IReadOnlyList<LinkIssue> OutsideWiki,
    IReadOnlyList<string> Warnings);

// Renders the wiki to a static site that any host can serve (GitHub Pages
// via Actions, GitLab Pages, Azure Static Web Apps, a file share).
// Templates are Liquid in Jekyll's dialect, and get what a data-driven
// landing page needs while wiki pages keep no front matter: _data/ files,
// other pages' bodies (the wiki_body filter), and per-page layouts chosen
// in _config.yml.
public static class HtmlExporter
{
    public static HtmlExportResult Export(WikiRepository repo, HtmlExportOptions options)
    {
        var theme = ThemeFiles.Load(repo.RootPath);
        var config = SiteConfig.Parse(theme.Text("_config.yml"));
        var title = options.Title ?? ResolveTitle(config, repo);
        var baseUrl = (options.BaseUrl ?? config.BaseUrl ?? "").TrimEnd('/');
        var warnings = new List<string>();

        var pages = repo.Walk()
            .Where(p => !PublishExclusions.IsExcluded(p, config.ExcludeFromSite))
            .ToList();
        var byLink = pages.ToDictionary(p => p.ToLinkPath(), StringComparer.OrdinalIgnoreCase);
        var markdown = pages.ToDictionary(p => p.ToLinkPath(), p => repo.Read(p).Markdown, StringComparer.OrdinalIgnoreCase);
        var rendered = pages.ToDictionary(
            p => p.ToLinkPath(),
            p => MarkdownPageRenderer.Render(markdown[p.ToLinkPath()], p.Name.Title),
            StringComparer.OrdinalIgnoreCase);

        var rootLink = config.RootPage;
        if (rootLink is not null)
        {
            if (!byLink.TryGetValue(rootLink, out var rootPage))
                throw new InvalidOperationException($"_config.yml: wikidown.root_page {rootLink} is not a published page");
            rootLink = rootPage.ToLinkPath();
        }
        string UrlOf(PagePath p) =>
            (rootLink is null ? p.IsTopLevelHome : p.ToLinkPath() == rootLink) ? "/" : p.ToLinkPath() + ".html";

        var pageObjects = pages.ToDictionary(p => p.ToLinkPath(), p => new Dictionary<string, object?>
        {
            ["url"] = UrlOf(p),
            ["title"] = rendered[p.ToLinkPath()].Title,
            ["name"] = p.Name.FileName,
            ["path"] = p.ToFilePath().Replace('\\', '/'),
            ["dir"] = p.Parent.IsRoot ? "/" : p.Parent.ToLinkPath() + "/",
            ["layout"] = config.LayoutFor(p.ToLinkPath()),
            ["content"] = rendered[p.ToLinkPath()].Html,
        }, StringComparer.OrdinalIgnoreCase);

        // The nav always comes from .order. _data/navigation.yml was written
        // for GitHub's Jekyll builder, which Wikidown no longer targets.
        var data = SiteData.Load(repo.RootPath);
        if (data.ContainsKey("navigation"))
            warnings.Add("_data/navigation.yml is ignored (the nav is built from .order files); delete it");
        data["navigation"] = NavData(NavTree.Build(pages, repo.ReadOrder, p => rendered[p.ToLinkPath()].Title), UrlOf);

        var site = new Dictionary<string, object?>(config.Values, StringComparer.Ordinal)
        {
            ["title"] = title,
            ["description"] = config.Description ?? "",
            ["repository_url"] = config.RepositoryUrl,
            ["baseurl"] = baseUrl,
            ["favicon"] = config.Favicon,
            ["url"] = config.Url,
            ["image"] = config.Image,
            ["data"] = data,
            ["pages"] = pages.Select(p => (object?)pageObjects[p.ToLinkPath()]).ToList(),
        };

        var bodies = new Dictionary<(string, bool), string>();
        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wiki = new WikiLookup(
            Resolve: raw =>
            {
                if (raw.Length > 0 && byLink.TryGetValue(SiteConfig.NormalizePagePath(raw), out var page)) return page.ToLinkPath();
                if (unresolved.Add(raw)) warnings.Add($"no published wiki page \"{raw}\" (wiki_body / wiki_title)");
                return null;
            },
            Body: (link, stripTitle) =>
            {
                if (!bodies.TryGetValue((link, stripTitle), out var body))
                    bodies[(link, stripTitle)] = body = MarkdownPageRenderer.RenderBody(markdown[link], link, baseUrl, stripTitle);
                return body;
            },
            Title: link => rendered[link].Title);

        var parser = new FluidParser();
        var templateOptions = new TemplateOptions { FileProvider = theme };
        LiquidFilters.Register(templateOptions.Filters, baseUrl, config.Url ?? "", wiki);

        var layouts = new Dictionary<string, (IFluidTemplate Template, string? Parent)>(StringComparer.Ordinal);
        (IFluidTemplate Template, string? Parent) Layout(string name)
        {
            if (layouts.TryGetValue(name, out var cached)) return cached;
            var file = $"_layouts/{name}.html";
            if (!theme.Has(file)) throw new InvalidOperationException($"layout \"{name}\" not found: no {file}");
            var (frontMatter, body) = FrontMatter.Split(theme.Text(file));
            return layouts[name] = (Parse(parser, body, file), frontMatter.GetValueOrDefault("layout")?.ToString());
        }
        // Jekyll-style: a layout's own front matter can name the layout it
        // sits inside, so a landing layout can reuse a base one.
        string ApplyLayouts(string? name, TemplateContext context, string content)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (name is not null && name is not ("none" or "null" or ""))
            {
                if (!seen.Add(name)) throw new InvalidOperationException($"layout \"{name}\" includes itself");
                var (template, parent) = Layout(name);
                context.SetValue("content", content);
                content = template.Render(context);
                name = parent;
            }
            return content;
        }
        foreach (var name in config.Layouts.Values.Append(SiteConfig.DefaultLayout).Distinct()) Layout(name);

        TemplateContext NewContext(Dictionary<string, object?> page)
        {
            var context = new TemplateContext(templateOptions);
            context.SetValue("site", site);
            context.SetValue("page", page);
            return context;
        }

        var output = Path.GetFullPath(options.OutputDirectory);
        // Empty the folder rather than delete it: on Windows a folder that is
        // open in Explorer, a terminal, or a dev server can't be removed.
        if (options.Clean && Directory.Exists(output))
        {
            foreach (var dir in Directory.GetDirectories(output)) Directory.Delete(dir, recursive: true);
            foreach (var file in Directory.GetFiles(output)) File.Delete(file);
        }
        Directory.CreateDirectory(output);

        foreach (var page in pages)
        {
            var link = page.ToLinkPath();
            var pageObject = pageObjects[link];
            var html = ApplyLayouts((string)pageObject["layout"]!, NewContext(pageObject), rendered[link].Html);

            var dest = Path.Combine(output, link.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + ".html");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllText(dest, html);
            if (link == rootLink) File.WriteAllText(Path.Combine(output, "index.html"), html);
        }

        // A root_page replaces the theme's index.html (by default a redirect
        // to /Home.html); otherwise index.html is rendered as a Liquid page.
        if (rootLink is null && theme.Has("index.html"))
        {
            var (frontMatter, body) = FrontMatter.Split(theme.Text("index.html"));
            var source = body
                .Replace("{{HOME}}", ThemeResources.HomeUrl(repo))
                .Replace("{{TITLE}}", title);
            var page = new Dictionary<string, object?>(frontMatter, StringComparer.Ordinal)
            {
                ["url"] = "/",
                ["name"] = "index.html",
                ["path"] = "index.html",
                ["dir"] = "/",
            };
            var context = NewContext(page);
            var html = Parse(parser, source, "index.html").Render(context);
            html = ApplyLayouts(frontMatter.GetValueOrDefault("layout")?.ToString(), context, html);
            File.WriteAllText(Path.Combine(output, "index.html"), html);
        }

        foreach (var (relative, copyTo) in theme.Assets())
        {
            var dest = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            copyTo(dest);
        }

        CopyStaticFiles(repo.RootPath, output);

        var outside = LinkChecker.Check(repo, pages, flagAbsolutePaths: false)
            .Where(i => i.Kind == LinkIssueKind.OutsideWiki)
            .ToList();
        return new HtmlExportResult(pages.Count, output, outside, warnings);
    }

    // Everything else in the wiki root ships verbatim, mirroring Jekyll's
    // copy-through of unknown files: images, install scripts, favicons,
    // extra stylesheets, and .attachments. Wiki sources (.md, .order),
    // Jekyll machinery (_-prefixed), other dot-files, Gemfiles, and the
    // separately-rendered root index.html stay out.
    private static void CopyStaticFiles(string wikiRoot, string output)
    {
        var pending = new Stack<(string Dir, string RelPrefix)>();
        pending.Push((wikiRoot, ""));
        while (pending.Count > 0)
        {
            var (dir, relPrefix) = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith('_')) continue;
                if (name.StartsWith('.') && !name.Equals(".attachments", StringComparison.OrdinalIgnoreCase)) continue;
                pending.Push((sub, relPrefix + name + "/"));
            }
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileName(file);
                var inAttachments = relPrefix.StartsWith(".attachments/", StringComparison.OrdinalIgnoreCase)
                    || relPrefix.Equals(".attachments/", StringComparison.OrdinalIgnoreCase);
                if (!inAttachments)
                {
                    if (name.StartsWith('_') || name.StartsWith('.')) continue;
                    if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name is "Gemfile" or "Gemfile.lock") continue;
                    if (relPrefix.Length == 0 && name.Equals("index.html", StringComparison.OrdinalIgnoreCase)) continue;
                }
                var dest = Path.Combine(output, (relPrefix + name).Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
        }
    }

    private static IFluidTemplate Parse(FluidParser parser, string liquid, string name)
    {
        if (!parser.TryParse(ThemeFiles.ToFluidSyntax(liquid), out var template, out var error))
            throw new InvalidOperationException($"{name}: {error}");
        return template;
    }

    private static List<object?> NavData(IReadOnlyList<NavNode> nodes, Func<PagePath, string> urlOf)
    {
        var list = new List<object?>();
        foreach (var node in nodes)
        {
            var item = new Dictionary<string, object?> { ["title"] = node.Title };
            if (node.IsPage) item["url"] = urlOf(node.Path);
            if (node.Children.Count > 0)
            {
                item["prefix"] = node.Path.ToLinkPath() + "/";
                item["children"] = NavData(node.Children, urlOf);
            }
            list.Add(item);
        }
        return list;
    }

    private static string ResolveTitle(SiteConfig config, WikiRepository repo)
    {
        if (config.Title is not null && config.Title != "{{TITLE}}") return config.Title;
        var repoRoot = Path.GetDirectoryName(repo.RootPath);
        var name = string.IsNullOrEmpty(repoRoot) ? null : Path.GetFileName(repoRoot);
        return string.IsNullOrEmpty(name) ? "Wiki" : name;
    }

}
