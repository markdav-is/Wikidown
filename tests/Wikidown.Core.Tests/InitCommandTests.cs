using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

public class InitCommandTests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _wikiRoot;

    public InitCommandTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), "wikidown-init-" + Guid.NewGuid().ToString("N"));
        _wikiRoot = Path.Combine(_repoRoot, "docs");
        Directory.CreateDirectory(_repoRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoRoot, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(params string[] extra)
    {
        var args = new[] { "init", "--root", _wikiRoot }.Concat(extra).ToArray();
        return CommandRunner.Run(args, TextWriter.Null, TextWriter.Null);
    }

    private string At(string relPath) =>
        Path.Combine(_repoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Init_ScaffoldsBothAgentSetsAndSeedsWiki()
    {
        Assert.Equal(0, Run());

        Assert.True(File.Exists(Path.Combine(_wikiRoot, "Home.md")));
        Assert.True(File.Exists(At(".mcp.json")));
        Assert.True(File.Exists(At(".claude/skills/wikidown/SKILL.md")));
        Assert.True(File.Exists(At(".claude/agents/wikidown-editor.md")));
        Assert.True(File.Exists(At("CLAUDE.md")));
        Assert.True(File.Exists(At(".vscode/mcp.json")));
        Assert.True(File.Exists(At(".github/skills/wikidown/SKILL.md")));
        Assert.True(File.Exists(At(".github/copilot-instructions.md")));
        Assert.True(File.Exists(At(".github/agents/wikidown.agent.md")));
        Assert.True(File.Exists(At(".github/chatmodes/wikidown.chatmode.md")));
    }

    [Fact]
    public void Init_SharedSkillIsIdenticalForBothAgents()
    {
        Run();
        Assert.Equal(
            File.ReadAllText(At(".claude/skills/wikidown/SKILL.md")),
            File.ReadAllText(At(".github/skills/wikidown/SKILL.md")));
    }

    [Fact]
    public void Init_AgentsClaude_SkipsCopilotFiles()
    {
        Run("--agents", "claude");
        Assert.True(File.Exists(At(".claude/skills/wikidown/SKILL.md")));
        Assert.False(File.Exists(At(".github/copilot-instructions.md")));
        Assert.False(File.Exists(At(".vscode/mcp.json")));
    }

    [Fact]
    public void Init_AgentsCopilot_SkipsClaudeFiles()
    {
        Run("--agents", "copilot");
        Assert.True(File.Exists(At(".github/skills/wikidown/SKILL.md")));
        Assert.False(File.Exists(At(".mcp.json")));
        Assert.False(File.Exists(At("CLAUDE.md")));
    }

    [Fact]
    public void Init_AgentsNone_OnlySeedsWiki()
    {
        Run("--agents", "none");
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "Home.md")));
        Assert.False(File.Exists(At(".mcp.json")));
        Assert.False(File.Exists(At(".github/copilot-instructions.md")));
    }

    [Fact]
    public void Init_DoesNotSeedNonEmptyWiki()
    {
        Directory.CreateDirectory(_wikiRoot);
        File.WriteAllText(Path.Combine(_wikiRoot, "Existing.md"), "# Existing\n");
        Run();
        Assert.False(File.Exists(Path.Combine(_wikiRoot, "Home.md")));
    }

    [Fact]
    public void Init_SkipsExistingFilesWithoutForce()
    {
        File.WriteAllText(At(".mcp.json"), "{ \"custom\": true }");
        Run();
        Assert.Equal("{ \"custom\": true }", File.ReadAllText(At(".mcp.json")));
    }

    [Fact]
    public void Init_ForceOverwritesExistingFiles()
    {
        File.WriteAllText(At(".mcp.json"), "{ \"custom\": true }");
        Run("--force");
        Assert.Contains("wikidown-mcp", File.ReadAllText(At(".mcp.json")));
    }

    [Fact]
    public void Init_AppendsToClaudeMdWithoutWikiSection()
    {
        File.WriteAllText(At("CLAUDE.md"), "# My project\n\nExisting guidance.\n");
        Run();
        var content = File.ReadAllText(At("CLAUDE.md"));
        Assert.StartsWith("# My project", content);
        Assert.Contains("Wikidown wiki", content);
    }

    [Fact]
    public void Init_LeavesClaudeMdThatAlreadyMentionsWiki()
    {
        File.WriteAllText(At("CLAUDE.md"), "# Mine\n\nThe Wikidown wiki is documented elsewhere.\n");
        Run();
        Assert.DoesNotContain("wikidown-editor", File.ReadAllText(At("CLAUDE.md")));
    }

    [Fact]
    public void Init_RejectsUnknownAgentsValue()
    {
        Assert.Equal(2, Run("--agents", "cursor"));
    }
}
