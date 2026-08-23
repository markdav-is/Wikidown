using Wikidown.Core;

namespace Wikidown.Cli;

// Scaffolds a Jekyll site into the wiki root so GitHub Pages can publish
// it straight from the branch (Settings → Pages → /docs), with a starter
// theme whose left nav honors .order via _data/navigation.yml.
public static class PagesCommand
{
    private static readonly string[] ThemeFiles =
    {
        "_config.yml",
        "index.html",
        "_layouts/wikidown.html",
        "_includes/nav-tree.html",
        "assets/wikidown.css",
    };

    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var force = args.Flag("force");
        var title = args.Optional("title") ?? DefaultTitle(repo);
        var home = HomeUrl(repo);

        foreach (var file in ThemeFiles)
        {
            var content = ReadResource("pages/" + file)
                .Replace("{{TITLE}}", title.Replace("\"", "\\\""))
                .Replace("{{HOME}}", home);
            WriteFile(repo.RootPath, file, content, force, w);
        }

        JekyllNavigation.Write(repo);
        w.WriteLine($"wrote {JekyllNavigation.DataFile} (regenerated on every change from now on)");

        w.WriteLine();
        w.WriteLine("Next: in GitHub, Settings → Pages → Source: \"Deploy from a branch\",");
        w.WriteLine($"      branch: main, folder: /{Path.GetFileName(repo.RootPath)}. Push, and the wiki is live.");
        return 0;
    }

    private static string DefaultTitle(WikiRepository repo)
    {
        var repoRoot = Path.GetDirectoryName(repo.RootPath);
        var name = string.IsNullOrEmpty(repoRoot) ? null : Path.GetFileName(repoRoot);
        return string.IsNullOrEmpty(name) ? "Wiki" : name;
    }

    private static string HomeUrl(WikiRepository repo)
    {
        var home = PagePath.Parse("/Home");
        if (repo.Exists(home)) return home.ToLinkPath() + ".html";
        var first = repo.ListChildren(PagePath.Root).FirstOrDefault();
        return first is null ? "/Home.html" : first.ToLinkPath() + ".html";
    }

    private static void WriteFile(string root, string relPath, string content, bool force, TextWriter w)
    {
        var path = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path) && !force)
        {
            w.WriteLine($"exists, skipped {relPath} (use --force to overwrite)");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        w.WriteLine($"wrote {relPath}");
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(PagesCommand).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
