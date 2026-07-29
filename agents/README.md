# Wikidown agent configs

Drop-in configs that teach AI coding assistants how to maintain a Wikidown
wiki via the MCP server (or, where MCP is unavailable, via the `wikidown` CLI).

The fastest install is the CLI scaffolder — from your repo root:

```bash
dotnet tool install -g Wikidown.Cli
wikidown init --agents all
```

`init` writes every file below to its destination (skipping anything that
already exists; `--force` overwrites, `--agents claude` / `--agents copilot`
narrows the set). The tables that follow are the manual-copy equivalent.

## Shared skill (Agent Skills standard)

One skill definition serves both assistants — Claude Code and GitHub Copilot
both support the Agent Skills `SKILL.md` format,
they just load it from different folders:

| File                          | Claude Code destination            | Copilot destination                |
| ----------------------------- | ---------------------------------- | ---------------------------------- |
| `skills/wikidown/SKILL.md`    | `.claude/skills/wikidown/SKILL.md` | `.github/skills/wikidown/SKILL.md` |

The skill carries the format rules, tool cheat sheet, CLI fallback, and
workflow. The per-agent files below are thin wiring on top of it.

## Claude Code

| File                                  | Where to put it in your repo        |
| ------------------------------------- | ----------------------------------- |
| `claude/wikidown.subagent.md`         | `.claude/agents/wikidown-editor.md` |
| `claude/CLAUDE.md`                    | append to your `CLAUDE.md`          |
| `../samples/mcp/claude-code.mcp.json` | `.mcp.json`                         |

The `.mcp.json` registers `wikidown-mcp` as an MCP server. The subagent owns
`/docs` edits; the CLAUDE.md snippet tells the main agent to delegate to it.

## GitHub Copilot

| File                              | Where to put it in your repo             |
| --------------------------------- | ---------------------------------------- |
| `copilot/copilot-instructions.md` | `.github/copilot-instructions.md`        |
| `copilot/wikidown.agent.md`       | `.github/agents/wikidown.agent.md`       |
| `copilot/wikidown.chatmode.md`    | `.github/chatmodes/wikidown.chatmode.md` |
| `copilot/mcp.json`                | `.vscode/mcp.json`                       |

The instructions file is loaded automatically into every Copilot chat in the
repo. The custom agent handles delegated wiki work (including the Copilot
coding agent on github.com); the chat mode adds a `wikidown` mode in VS Code.
The MCP config exposes the same tools to Copilot as `wikidown_wiki_*`.

## Both

Both agents need the `wikidown-mcp` binary on `PATH`:

```bash
dotnet tool install -g Wikidown.Mcp
```

Or run from source via the `dotnet run` form shown in the sample MCP configs.
