using Wikidown.Core;
using Wikidown.Html;

namespace Wikidown.Cli;

// Scaffolds a Jekyll site into the wiki root so GitHub Pages can publish
// it straight from the branch (Settings → Pages → /docs), with a starter
// theme whose left nav honors .order via _data/navigation.yml.
public static class PagesCommand
{
    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var force = args.Flag("force");
        var title = args.Optional("title") ?? DefaultTitle(repo);
        var home = ThemeResources.HomeUrl(repo);

        foreach (var file in ThemeResources.Files)
        {
            var content = ThemeResources.Read(file)
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
}
