using Fluid;
using Fluid.Values;
using Wikidown.Core;

namespace Wikidown.Html;

public sealed record HtmlExportOptions(
    string OutputDirectory,
    string? Title = null,
    string? BaseUrl = null,
    bool Clean = false);

public sealed record HtmlExportResult(int PageCount, string OutputDirectory);

// Renders the wiki to a static site with the same theme, layout, and nav
// data GitHub Pages' Jekyll would use — so the output is host-agnostic
// (GitLab Pages, Azure Static Web Apps, a file share) and needs no Ruby.
public static class HtmlExporter
{
    private const string LayoutFile = "_layouts/wikidown.html";

    public static HtmlExportResult Export(WikiRepository repo, HtmlExportOptions options)
    {
        var theme = ThemeFiles.Load(repo.RootPath);
        var config = SiteConfig.Parse(theme.Text("_config.yml"));
        var title = options.Title ?? ResolveTitle(config, repo);
        var baseUrl = (options.BaseUrl ?? config.BaseUrl ?? "").TrimEnd('/');

        var pages = repo.Walk()
            .Where(p => !PublishExclusions.IsExcluded(p, config.ExcludeFromSite))
            .ToList();
        var rendered = pages.ToDictionary(
            p => p.ToLinkPath(),
            p => MarkdownPageRenderer.Render(repo.Read(p).Markdown, p.Name.Title),
            StringComparer.Ordinal);

        var site = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["description"] = config.Description ?? "",
            ["repository_url"] = config.RepositoryUrl,
            ["baseurl"] = baseUrl,
            ["favicon"] = config.Favicon,
            ["data"] = new Dictionary<string, object?>
            {
                ["navigation"] = NavData(NavTree.Build(pages, repo.ReadOrder)),
            },
            ["pages"] = pages.Select(p => (object?)new Dictionary<string, object?>
            {
                ["url"] = p.ToLinkPath() + ".html",
                ["title"] = rendered[p.ToLinkPath()].Title,
                ["name"] = p.Name.FileName,
                ["path"] = p.ToFilePath().Replace('\\', '/'),
            }).ToList(),
        };

        var parser = new FluidParser();
        var templateOptions = new TemplateOptions { FileProvider = theme };
        templateOptions.Filters.AddFilter("relative_url", (input, _, _) =>
            new StringValue(baseUrl + input.ToStringValue()));

        var layout = Parse(parser, theme.Text(LayoutFile), LayoutFile);

        var output = Path.GetFullPath(options.OutputDirectory);
        if (options.Clean && Directory.Exists(output)) Directory.Delete(output, recursive: true);
        Directory.CreateDirectory(output);

        foreach (var page in pages)
        {
            var link = page.ToLinkPath();
            var context = new TemplateContext(templateOptions);
            context.SetValue("site", site);
            context.SetValue("page", new Dictionary<string, object?>
            {
                ["url"] = link + ".html",
                ["title"] = rendered[link].Title,
                ["name"] = page.Name.FileName,
                ["path"] = page.ToFilePath().Replace('\\', '/'),
            });
            context.SetValue("content", rendered[link].Html);

            var dest = Path.Combine(output, link.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + ".html");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllText(dest, layout.Render(context));
        }

        if (theme.Has("index.html"))
        {
            var source = StripFrontMatter(theme.Text("index.html"))
                .Replace("{{HOME}}", ThemeResources.HomeUrl(repo))
                .Replace("{{TITLE}}", title);
            var index = Parse(parser, source, "index.html");
            var context = new TemplateContext(templateOptions);
            context.SetValue("site", site);
            File.WriteAllText(Path.Combine(output, "index.html"), index.Render(context));
        }

        foreach (var (relative, copyTo) in theme.Assets())
        {
            var dest = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            copyTo(dest);
        }

        CopyStaticFiles(repo.RootPath, output);

        return new HtmlExportResult(pages.Count, output);
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

    private static List<object?> NavData(IReadOnlyList<NavNode> nodes)
    {
        var list = new List<object?>();
        foreach (var node in nodes)
        {
            var item = new Dictionary<string, object?> { ["title"] = node.Title };
            if (node.IsPage) item["url"] = node.Path.ToLinkPath() + ".html";
            if (node.Children.Count > 0)
            {
                item["prefix"] = node.Path.ToLinkPath() + "/";
                item["children"] = NavData(node.Children);
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

    private static string StripFrontMatter(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal)) return text;
        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return text;
        var afterDelimiter = text.IndexOf('\n', end + 1);
        return afterDelimiter < 0 ? "" : text[(afterDelimiter + 1)..];
    }

}
