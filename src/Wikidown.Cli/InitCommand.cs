using Wikidown.Core;

namespace Wikidown.Cli;

public static class InitCommand
{
    private static readonly (string Resource, string Destination)[] ClaudeFiles =
    {
        ("agents/claude/mcp.json", ".mcp.json"),
        ("agents/skills/wikidown/SKILL.md", ".claude/skills/wikidown/SKILL.md"),
        ("agents/claude/wikidown.subagent.md", ".claude/agents/wikidown-editor.md"),
    };

    private static readonly (string Resource, string Destination)[] CopilotFiles =
    {
        ("agents/copilot/mcp.json", ".vscode/mcp.json"),
        ("agents/skills/wikidown/SKILL.md", ".github/skills/wikidown/SKILL.md"),
        ("agents/copilot/copilot-instructions.md", ".github/copilot-instructions.md"),
        ("agents/copilot/wikidown.agent.md", ".github/agents/wikidown.agent.md"),
        ("agents/copilot/wikidown.chatmode.md", ".github/chatmodes/wikidown.chatmode.md"),
    };

    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var agents = ParseAgents(args.Optional("agents") ?? "all");
        var force = args.Flag("force");
        var repoRoot = Path.GetDirectoryName(repo.RootPath)
            ?? throw new CliUsageException("--root must be a folder inside the repo, e.g. docs");

        SeedWiki(repo, w);

        if (agents.Claude)
        {
            foreach (var (resource, destination) in ClaudeFiles)
                WriteFile(repoRoot, destination, ReadResource(resource), force, w);
            AppendClaudeMd(repoRoot, w);
        }

        if (agents.Copilot)
        {
            foreach (var (resource, destination) in CopilotFiles)
                WriteFile(repoRoot, destination, ReadResource(resource), force, w);
        }

        return 0;
    }

    private static void SeedWiki(WikiRepository repo, TextWriter w)
    {
        if (repo.ListChildren(PagePath.Parse("/")).Count > 0)
        {
            w.WriteLine($"wiki root already has pages, skipped seeding");
            return;
        }
        repo.Write(new WikiPage(
            PagePath.Parse("/Home"),
            "# Home\n\nWelcome to your Wikidown wiki. Add pages with `wikidown new` or the `wiki_*` MCP tools.\n"));
        w.WriteLine("seeded /Home");
    }

    private static void AppendClaudeMd(string repoRoot, TextWriter w)
    {
        var snippet = ReadResource("agents/claude/CLAUDE.md");
        var path = Path.Combine(repoRoot, "CLAUDE.md");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, snippet);
            w.WriteLine("wrote CLAUDE.md");
            return;
        }
        var existing = File.ReadAllText(path);
        if (existing.Contains("Wikidown wiki", StringComparison.OrdinalIgnoreCase))
        {
            w.WriteLine("CLAUDE.md already mentions the wiki, skipped");
            return;
        }
        File.WriteAllText(path, existing.TrimEnd('\r', '\n') + "\n\n" + snippet);
        w.WriteLine("appended wiki section to CLAUDE.md");
    }

    private static void WriteFile(string repoRoot, string relPath, string content, bool force, TextWriter w)
    {
        var path = Path.Combine(repoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path) && !force)
        {
            w.WriteLine($"exists, skipped {relPath} (use --force to overwrite)");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        w.WriteLine($"wrote {relPath}");
    }

    private static (bool Claude, bool Copilot) ParseAgents(string value) => value switch
    {
        "all" => (true, true),
        "claude" => (true, false),
        "copilot" => (false, true),
        "none" => (false, false),
        _ => throw new CliUsageException("--agents must be claude, copilot, all, or none"),
    };

    private static string ReadResource(string name)
    {
        using var stream = typeof(InitCommand).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
