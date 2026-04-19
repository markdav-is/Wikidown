# Wikidown agent configs

Drop-in configs that teach AI coding assistants how to maintain a Wikidown
wiki via the MCP server (or, where MCP is unavailable, via the `wikidown` CLI).

## Claude Code

| File                                      | Where to put it in your repo            |
| ----------------------------------------- | --------------------------------------- |
| `claude/wikidown.subagent.md`             | `.claude/agents/wikidown-editor.md`     |
| `claude/wikidown.skill.md`                | `.claude/skills/wikidown/SKILL.md`      |
| `claude/CLAUDE.md`                        | append to your `CLAUDE.md`              |
| `../samples/mcp/claude-code.mcp.json`     | `.mcp.json`                             |

The `.mcp.json` registers `wikidown-mcp` as an MCP server. The subagent + skill
files give Claude prescriptive guidance on when and how to use those tools.

## GitHub Copilot (VS Code)

| File                                      | Where to put it in your repo                |
| ----------------------------------------- | ------------------------------------------- |
| `copilot/copilot-instructions.md`         | `.github/copilot-instructions.md`           |
| `copilot/wikidown.chatmode.md`            | `.github/chatmodes/wikidown.chatmode.md`    |
| `copilot/mcp.json`                        | `.vscode/mcp.json`                          |

The instructions file is loaded automatically into every Copilot chat in the
repo. The chat mode adds a `@wikidown` mode you can switch to. The MCP config
exposes the same `wikidown_*` tools to Copilot.

## Both

Both agents need the `wikidown-mcp` binary on `PATH`:

```bash
dotnet tool install -g Wikidown.Mcp
```

Or run from source via the `dotnet run` form shown in the sample MCP configs.
