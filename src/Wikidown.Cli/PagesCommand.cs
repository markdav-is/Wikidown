using Wikidown.Core;
using Wikidown.Html;

namespace Wikidown.Cli;

// Sets a wiki up to publish through a pipeline: a GitHub Actions workflow
// that runs `wikidown export-html` and deploys to GitHub Pages, plus the
// site's _config.yml. The theme stays built in (and keeps improving with
// the CLI) unless --theme copies it into the wiki for editing.
public static class PagesCommand
{
    public const string WorkflowPath = ".github/workflows/pages.yml";

    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var force = args.Flag("force");
        var title = args.Optional("title") ?? DefaultTitle(repo);
        var home = ThemeResources.HomeUrl(repo);
        var ending = repo.NewFileEnding();

        var files = args.Flag("theme") ? ThemeResources.Files : ["_config.yml"];
        foreach (var file in files)
        {
            var content = ThemeResources.Read(file)
                .Replace("{{TITLE}}", title.Replace("\"", "\\\""))
                .Replace("{{HOME}}", home);
            WriteFile(repo.RootPath, file, content, force, ending, w);
        }

        var repoRoot = RepoRoot(repo.RootPath);
        var wikiFolder = Path.GetRelativePath(repoRoot, repo.RootPath).Replace('\\', '/');
        WriteFile(repoRoot, WorkflowPath, Workflow(wikiFolder), force, ending, w);

        WarnAboutJekyllLeftovers(repo.RootPath, w);

        w.WriteLine();
        w.WriteLine("Next: in GitHub, Settings → Pages → Source: \"GitHub Actions\". Commit and push,");
        w.WriteLine("      and the workflow publishes the wiki.");
        w.WriteLine("Preview locally: wikidown export-html --output _site --clean");
        return 0;
    }

    public static string Workflow(string wikiFolder) =>
        string.Join('\n',
            "# Publishes the Wikidown wiki to GitHub Pages with `wikidown export-html`.",
            "# Settings → Pages → Source must be \"GitHub Actions\". Written by `wikidown pages`.",
            "name: pages",
            "",
            "on:",
            "  push:",
            "    branches: [main]",
            "  workflow_dispatch:",
            "",
            "permissions:",
            "  contents: read",
            "  pages: write",
            "  id-token: write",
            "",
            "concurrency:",
            "  group: pages",
            "  cancel-in-progress: false",
            "",
            "jobs:",
            "  pages:",
            "    uses: markdav-is/Wikidown/.github/workflows/wiki-pages.yml@main",
            "    with:",
            $"      root: {wikiFolder}",
            "      # cli-version: 0.14.0   # pin a Wikidown.Cli version; default is the latest",
            "");

    // Files only GitHub's Jekyll builder read. Harmless, but stale.
    private static void WarnAboutJekyllLeftovers(string wikiRoot, TextWriter w)
    {
        foreach (var leftover in new[] { "_data/navigation.yml", "Gemfile", "Gemfile.lock" })
            if (File.Exists(Path.Combine(wikiRoot, leftover.Replace('/', Path.DirectorySeparatorChar))))
                w.WriteLine($"note: {leftover} was for GitHub's Jekyll builder and is no longer used; delete it");

        var config = Path.Combine(wikiRoot, "_config.yml");
        if (File.Exists(config) && File.ReadAllText(config).Contains("jekyll-", StringComparison.Ordinal))
            w.WriteLine("note: _config.yml still has Jekyll settings (markdown, plugins, relative_links, ...); export-html ignores them");
    }

    // The git checkout the wiki lives in, where .github/ belongs; else the
    // folder containing the wiki, as `wikidown init` assumes.
    private static string RepoRoot(string wikiRoot)
    {
        for (var dir = new DirectoryInfo(wikiRoot); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
        return Path.GetDirectoryName(wikiRoot) ?? wikiRoot;
    }

    private static string DefaultTitle(WikiRepository repo)
    {
        var repoRoot = Path.GetDirectoryName(repo.RootPath);
        var name = string.IsNullOrEmpty(repoRoot) ? null : Path.GetFileName(repoRoot);
        return string.IsNullOrEmpty(name) ? "Wiki" : name;
    }

    private static void WriteFile(string root, string relPath, string content, bool force, string ending, TextWriter w)
    {
        var path = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path) && !force)
        {
            w.WriteLine($"exists, skipped {relPath} (use --force to overwrite)");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        LineEndings.WriteFile(path, content, ending);
        w.WriteLine($"wrote {relPath}");
    }
}
