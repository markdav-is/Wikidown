using Wikidown.Core;

namespace Wikidown.Cli;

public static class Commands
{
    public static int List(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var parent = PagePath.Parse(args.Optional("path") ?? "/");
        foreach (var child in repo.ListChildren(parent))
            w.WriteLine($"{child.ToLinkPath()}\t{child.Name.Title}");
        return 0;
    }

    public static int Read(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var page = repo.Read(PagePath.Parse(args.Require("path")));
        w.Write(page.Markdown);
        if (!page.Markdown.EndsWith('\n')) w.WriteLine();
        return 0;
    }

    public static int Write(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var path = PagePath.Parse(args.Require("path"));
        var content = LoadContent(args);
        repo.Write(new WikiPage(path, content));
        w.WriteLine($"wrote {path.ToLinkPath()}");
        return 0;
    }

    public static int New(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var path = PagePath.Parse(args.Require("path"));
        if (repo.Exists(path))
            throw new CliUsageException($"page already exists: {path.ToLinkPath()}");
        var title = args.Optional("title") ?? path.Name.Title;
        var body = args.Options.ContainsKey("file") || args.Flag("stdin")
            ? LoadContent(args)
            : $"# {title}\n\n";
        repo.Write(new WikiPage(path, body));
        w.WriteLine($"created {path.ToLinkPath()}");
        return 0;
    }

    public static int Move(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var from = PagePath.Parse(args.Require("from"));
        var to = PagePath.Parse(args.Require("to"));
        repo.Move(from, to);
        w.WriteLine($"moved {from.ToLinkPath()} -> {to.ToLinkPath()}");
        return 0;
    }

    public static int Delete(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var path = PagePath.Parse(args.Require("path"));
        repo.Delete(path, deleteSubpages: args.Flag("recursive"));
        w.WriteLine($"deleted {path.ToLinkPath()}");
        return 0;
    }

    public static int Reorder(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var folder = PagePath.Parse(args.Require("folder"));
        var names = args.Require("names")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        repo.WriteOrder(folder, names);
        w.WriteLine($"reordered {folder.ToLinkPath()} ({names.Length} entries)");
        return 0;
    }

    public static int Search(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var query = args.Require("query");
        var caseSensitive = args.Flag("case-sensitive");
        var hits = 0;
        foreach (var hit in PageSearch.Search(repo, query, caseSensitive))
        {
            w.WriteLine($"{hit.Path.ToLinkPath()}:{hit.LineNumber}: {hit.Line}");
            hits++;
        }
        return hits > 0 ? 0 : 1;
    }

    private static string LoadContent(ParsedArgs args)
    {
        var file = args.Optional("file");
        if (file is not null) return File.ReadAllText(file);
        if (args.Flag("stdin")) return Console.In.ReadToEnd();
        throw new CliUsageException("provide --file <path> or --stdin");
    }
}
