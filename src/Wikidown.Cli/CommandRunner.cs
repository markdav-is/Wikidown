using Wikidown.Core;

namespace Wikidown.Cli;

public static class CommandRunner
{
    public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            PrintUsage(stdout);
            return 1;
        }

        if (IsHelp(args[0]))
        {
            var target = args.Length > 1 && !IsHelp(args[1]) ? args[1] : null;
            return PrintHelp(target, stdout, stderr);
        }

        // --help/-h are parameterless flags, valid anywhere after the command,
        // and short-circuit before argument validation (e.g. missing --path).
        if (args.Skip(1).Any(IsHelpFlag))
            return PrintHelp(args[0], stdout, stderr);

        try
        {
            var parsed = ParsedArgs.Parse(args);
            var repo = new WikiRepository(parsed.Root);
            return parsed.Command switch
            {
                "list" => Commands.List(repo, parsed, stdout),
                "read" => Commands.Read(repo, parsed, stdout),
                "write" => Commands.Write(repo, parsed, stdout),
                "edit" => Commands.Edit(repo, parsed, stdout),
                "write-section" => Commands.WriteSection(repo, parsed, stdout),
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

    private static bool IsHelpFlag(string a) =>
        a is "-h" or "--help";

    private static int PrintHelp(string? command, TextWriter stdout, TextWriter stderr)
    {
        if (command is null)
        {
            PrintUsage(stdout);
            return 0;
        }

        if (!CommandHelp.TryGetValue(command, out var text))
            return Unknown(command, stderr);

        stdout.Write(text);
        return 0;
    }

    private static void PrintUsage(TextWriter w)
    {
        w.WriteLine("wikidown — maintain a Wikidown /docs wiki");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  wikidown <command> [--root <path>] [options]");
        w.WriteLine();
        w.WriteLine("Commands:");
        w.WriteLine("  list     [--path /Link/Path]                 list children of a page (or root)");
        w.WriteLine("  read     --path /Link/Path [--section H]     print page markdown (or one section) to stdout");
        w.WriteLine("  write    --path /Link/Path [--file F | --stdin]  write/overwrite a page");
        w.WriteLine("  edit     --path /P (--old T | --old-file F) (--new T | --new-file F | --stdin) [--all]");
        w.WriteLine("           replace exact text in a page, leaving the rest untouched");
        w.WriteLine("  write-section --path /P --section H [--file F | --stdin] [--create]");
        w.WriteLine("           replace the body under one heading, keeping the rest of the page");
        w.WriteLine("  new    --path /Link/Path [--title T] [--file F | --stdin]  create a page");
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

    private static readonly Dictionary<string, string> CommandHelp = new(StringComparer.Ordinal)
    {
        ["list"] =
            """
            Usage:
              wikidown list [--path /Link/Path] [--root <path>]

            List child pages of a wiki page, or the root if --path is omitted.

            Options:
              --path    Wiki link path to list children of (default: /)
              --root    Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown list
              wikidown list --path /Getting-Started

            """,

        ["read"] =
            """
            Usage:
              wikidown read --path /Link/Path [--section <heading>] [--root <path>]

            Print a page's Markdown to stdout, or just one section of it.
            --section matches a heading case-insensitively, ignoring leading
            #s and whitespace, and prints that heading plus everything below
            it up to the next heading of the same or higher level (a ##
            section includes its ### children). A miss lists the page's
            headings; if several headings match, the first is printed with a
            trailing note.

            Options:
              --path      Required title-form wiki path, e.g. /Getting-Started/Format
              --section   Heading text of the one section to print
              --root      Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown read --path /Getting-Started
              wikidown read --path /Getting-Started/Format --root ./my-wiki
              wikidown read --path /MCP-Server --section "Wiki root"

            """,

        ["write"] =
            """
            Usage:
              wikidown write --path /Link/Path (--file <path> | --stdin) [--root <path>]

            Overwrite an existing page's content. Auto-injects or refreshes the
            page's breadcrumb line.

            Options:
              --path    Required title-form wiki path
              --file    Read the new Markdown body from a file
              --stdin   Read the new Markdown body from standard input
              --root    Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown write --path /Getting-Started --file getting-started.md
              cat page.md | wikidown write --path /Getting-Started --stdin

            """,

        ["edit"] =
            """
            Usage:
              wikidown edit --path /Link/Path (--old <text> | --old-file <path>)
                            (--new <text> | --new-file <path> | --stdin) [--all] [--root <path>]

            Replace an exact substring of a page in place, leaving the rest of
            the page untouched — the small-change alternative to `write`. The
            old text must match the page's raw Markdown exactly (line endings
            are normalized, so CRLF vs LF never matters) and, without --all,
            exactly once. The breadcrumb line and .order are never modified,
            and a missing page is an error, not a create. Prints the changed
            line numbers with two lines of context.

            Options:
              --path       Required title-form wiki path
              --old        Text to replace (use --old-file for multi-line text)
              --old-file   Read the text to replace from a file
              --new        Replacement text (an empty string deletes)
              --new-file   Read the replacement text from a file
              --stdin      Read the replacement text from standard input
              --all        Replace every occurrence instead of failing on ambiguity
              --root       Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown edit --path /Home --old "coming soon" --new "shipped in 0.6"
              wikidown edit --path /CLI --old-file before.txt --new-file after.txt
              wikidown edit --path /Home --old colour --new color --all

            """,

        ["write-section"] =
            """
            Usage:
              wikidown write-section --path /Link/Path --section <heading>
                                     (--file <path> | --stdin) [--create] [--root <path>]

            Replace the body under one heading, keeping the rest of the page.
            The heading line is preserved verbatim; the replaced span runs
            from the line after it to the next heading of the same or higher
            level, so ### children inside a ## section are replaced too.
            Exactly one blank line is kept around the new body. The heading
            matches case-insensitively, ignoring leading #s and whitespace; a
            miss lists the page's headings, and an ambiguous match is an
            error. The breadcrumb line and .order are never modified.

            Options:
              --path      Required title-form wiki path
              --section   Required heading text of the section to replace
              --file      Read the new section body from a file
              --stdin     Read the new section body from standard input
              --create    Append a new "## <section>" at the end when none matches
              --root      Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown write-section --path /CLI --section "Wiki root" --file root.md
              cat notes.md | wikidown write-section --path /Home --section Notes --stdin --create

            """,

        ["new"] =
            """
            Usage:
              wikidown new --path <wiki-path> [--title <title>] [--file <path> | --stdin] [--root <path>]

            Create a new page. The body defaults to a bare "# Title" heading
            when neither --file nor --stdin is given.

            Options:
              --path    Required title-form wiki path, e.g. /Specifications/Photo-Upload
              --title   Optional page heading (default: derived from --path)
              --file    Read Markdown body from a file
              --stdin   Read Markdown body from standard input
              --root    Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown new --path /Specifications/Photo-Upload --file photo-upload.md
              cat photo-upload.md | wikidown new --path /Specifications/Photo-Upload --stdin
              wikidown new --path /FAQ --title "Frequently Asked Questions"

            """,

        ["move"] =
            """
            Usage:
              wikidown move --from /A --to /B [--dry-run] [--root <path>]

            Rename or move a page (subpages travel with it). Rewrites inbound
            links from every other page, the moved page's own relative
            links/images if the move changed its folder depth, and
            regenerates breadcrumbs for the moved page and its descendants.

            Options:
              --from      Required current title-form wiki path
              --to        Required destination title-form wiki path
              --dry-run   Preview the rewrite without touching any files
              --root      Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown move --from /Old-Name --to /New-Name
              wikidown move --from /Foo --to /Bar/Foo --dry-run

            """,

        ["delete"] =
            """
            Usage:
              wikidown delete --path /Link/Path [--recursive] [--root <path>]

            Delete a page.

            Options:
              --path        Required title-form wiki path
              --recursive   Also delete subpages
              --root        Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown delete --path /Draft-Page
              wikidown delete --path /Old-Section --recursive

            """,

        ["reorder"] =
            """
            Usage:
              wikidown reorder --folder /Link/Path --names a,b,c [--root <path>]

            Rewrite the .order file for a folder.

            Options:
              --folder   Required title-form wiki path of the folder to reorder
              --names    Required comma-separated list of page base-names, in order
              --root     Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown reorder --folder /Getting-Started --names Format,Updating,Publishing-to-GitHub-Pages
              wikidown reorder --folder / --names Home,Getting-Started,CLI

            """,

        ["search"] =
            """
            Usage:
              wikidown search --query <text> [--case-sensitive] [--root <path>]

            Full-text search across page bodies. Exits 0 if any hits were
            found, 1 otherwise.

            Options:
              --query           Required search text
              --case-sensitive  Match case exactly (default: case-insensitive)
              --root            Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown search --query breadcrumb
              wikidown search --query Jekyll --case-sensitive

            """,

        ["check-links"] =
            """
            Usage:
              wikidown check-links [--no-absolute-check] [--no-index-check] [--root <path>]

            Validate that relative links/images resolve, that page bodies
            don't use absolute title-path links (they 404 on github.com), and
            that every subpage folder has an index page linking each child.
            Exits non-zero if any issues are found.

            Options:
              --no-absolute-check   Skip the absolute title-path link check
              --no-index-check      Skip the index-page audit
              --root                Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown check-links
              wikidown check-links --no-index-check

            """,

        ["backfill-breadcrumbs"] =
            """
            Usage:
              wikidown backfill-breadcrumbs [--dry-run] [--root <path>]

            One-time catch-up: add or refresh the breadcrumb line on every
            existing page that predates it. write/new/move maintain
            breadcrumbs automatically going forward.

            Options:
              --dry-run   List which pages would change without writing anything
              --root      Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown backfill-breadcrumbs --dry-run
              wikidown backfill-breadcrumbs

            """,

        ["export-pdf"] =
            """
            Usage:
              wikidown export-pdf --output <path> [--from /Link/Path] [--title T]
                                   [--no-cover] [--no-toc] [--allow-html-skip] [--root <path>]

            Combine the wiki (or a subtree) into a single linked PDF.

            Options:
              --output            Required output file path
              --from              Only export this page and its descendants
              --title             Cover page title (default: repo folder name)
              --no-cover          Skip the cover page
              --no-toc            Skip the table of contents page
              --allow-html-skip   Render unsupported raw HTML as a placeholder instead of failing
              --root              Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown export-pdf --output wiki.pdf
              wikidown export-pdf --output getting-started.pdf --from /Getting-Started --no-cover

            """,

        ["init"] =
            """
            Usage:
              wikidown init [--agents claude|copilot|all|none] [--force] [--root <path>]

            Seed an empty wiki with a /Home page and install AI agent configs
            into the folder containing the wiki root.

            Options:
              --agents   Which agent configs to install (default: all)
              --force    Overwrite existing files, including updated agent configs
              --root     Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown init
              wikidown init --agents claude --force

            """,

        ["pages"] =
            """
            Usage:
              wikidown pages [--title T] [--force] [--root <path>]

            Scaffold a Jekyll-based static site (starter theme, assets,
            _data/navigation.yml, root index.html redirect) into the wiki
            root so GitHub Pages can publish it.

            Options:
              --title   Site title (default: repo folder name)
              --force   Overwrite theme files you've edited
              --root    Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown pages
              wikidown pages --title "My Project Wiki" --force

            """,

        ["export-html"] =
            """
            Usage:
              wikidown export-html --output <dir> [--base-url /prefix] [--title T] [--clean] [--root <path>]

            Render the wiki to a complete static HTML site in-process
            (Markdig + Fluid), no Jekyll or Ruby needed.

            Options:
              --output     Required output directory
              --base-url   Prefix theme links for sites served under a path
              --title      Site title (default: repo folder name)
              --clean      Empty the output folder first
              --root       Path to the docs folder (default: ./docs)
              -h, --help

            Examples:
              wikidown export-html --output ./dist
              wikidown export-html --output ./dist --base-url /wikidown --clean

            """,
    };
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}
