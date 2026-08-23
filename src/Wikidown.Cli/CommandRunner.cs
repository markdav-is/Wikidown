using Wikidown.Core;

namespace Wikidown.Cli;

public static class CommandRunner
{
    public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage(stdout);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var parsed = ParsedArgs.Parse(args);
            var repo = new WikiRepository(parsed.Root);
            return parsed.Command switch
            {
                "list" => Commands.List(repo, parsed, stdout),
                "read" => Commands.Read(repo, parsed, stdout),
                "write" => Commands.Write(repo, parsed, stdout),
                "new" => Commands.New(repo, parsed, stdout),
                "move" => Commands.Move(repo, parsed, stdout),
                "delete" => Commands.Delete(repo, parsed, stdout),
                "reorder" => Commands.Reorder(repo, parsed, stdout),
                "search" => Commands.Search(repo, parsed, stdout),
                "check-links" => Commands.CheckLinks(repo, parsed, stdout),
                "backfill-breadcrumbs" => Commands.BackfillBreadcrumbs(repo, parsed, stdout),
                "export-pdf" => ExportPdfCommand.Run(repo, parsed, stdout),
                "init" => InitCommand.Run(repo, parsed, stdout),
                "pages" => PagesCommand.Run(repo, parsed, stdout),
                "export-html" => ExportHtmlCommand.Run(repo, parsed, stdout),
                _ => Unknown(parsed.Command, stderr),
            };
        }
        catch (CliUsageException ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Unknown(string command, TextWriter stderr)
    {
        stderr.WriteLine($"error: unknown command '{command}'. Try 'wikidown --help'.");
        return 2;
    }

    private static bool IsHelp(string a) =>
        a is "-h" or "--help" or "help";

    private static void PrintUsage(TextWriter w)
    {
        w.WriteLine("wikidown — maintain a Wikidown /docs wiki");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  wikidown <command> [--root <path>] [options]");
        w.WriteLine();
        w.WriteLine("Commands:");
        w.WriteLine("  list     [--path /Link/Path]                 list children of a page (or root)");
        w.WriteLine("  read     --path /Link/Path                   print page markdown to stdout");
        w.WriteLine("  write    --path /Link/Path [--file F | --stdin]  write/overwrite a page");
        w.WriteLine("  new      --path /Link/Path [--title T] [--file F | --stdin]  create a page");
        w.WriteLine("  move     --from /A --to /B [--dry-run]       rename/move a page (and subpages);");
        w.WriteLine("           rewrites inbound links and the moved page's own relative links");
        w.WriteLine("  delete   --path /P [--recursive]             delete a page (and optionally subpages)");
        w.WriteLine("  reorder  --folder /P --names a,b,c           rewrite .order for a folder");
        w.WriteLine("  search   --query <text> [--case-sensitive]   search all page bodies");
        w.WriteLine("  check-links  [--no-absolute-check] [--no-index-check]  validate relative");
        w.WriteLine("               links/images; also flags absolute title-path body links and");
        w.WriteLine("               folders with a missing or under-linking index page, unless disabled");
        w.WriteLine("  backfill-breadcrumbs [--dry-run]             add/refresh the breadcrumb line");
        w.WriteLine("               on every existing page that predates it (write/move do this");
        w.WriteLine("               automatically going forward; this is a one-time catch-up)");
        w.WriteLine("  export-pdf --output <path> [--title T] [--from /Link/Path]");
        w.WriteLine("               [--no-toc] [--no-cover] [--allow-html-skip]");
        w.WriteLine("               combine the wiki (or a subtree) into one linked PDF");
        w.WriteLine("  init     [--agents claude|copilot|all|none] [--force]");
        w.WriteLine("           seed the wiki and install AI agent configs (Claude Code +");
        w.WriteLine("           GitHub Copilot) in the folder containing the wiki root");
        w.WriteLine("  pages    [--title T] [--force]");
        w.WriteLine("           scaffold a Jekyll site + starter theme into the wiki root so");
        w.WriteLine("           GitHub Pages can publish it; (re)generates _data/navigation.yml");
        w.WriteLine("  export-html --output <dir> [--base-url /prefix] [--title T] [--clean]");
        w.WriteLine("           render the wiki to a static HTML site with the same theme, no");
        w.WriteLine("           Jekyll/Ruby needed — for GitLab Pages, any static host, or local preview");
        w.WriteLine();
        w.WriteLine("Global:");
        w.WriteLine("  --root <path>   path to docs folder (default: ./docs)");
    }
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}
