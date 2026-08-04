# Agents

Wikidown ships drop-in configs so AI coding assistants can maintain your wiki
through the [MCP Server](MCP-Server.md) without you wiring anything up by hand.

## Shared skill

Claude and Copilot share one skill file in the Agent Skills standard format:
[`agents/skills/wikidown/SKILL.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/skills/wikidown/SKILL.md).
It carries the format rules, tool cheat sheet, CLI fallback, and workflow.

- Claude Code loads it from `.claude/skills/wikidown/SKILL.md`.
- GitHub Copilot loads it from `.github/skills/wikidown/SKILL.md`.

## Claude Code

In [`agents/claude/`](https://github.com/markdav-is/Wikidown/tree/main/agents/claude),
layered on top of the shared skill:

- `wikidown.subagent.md` → `.claude/agents/wikidown-editor.md` — a subagent
  definition that owns `/docs/*.md` reads and writes.
- `CLAUDE.md` snippet — append to your project's root `CLAUDE.md` so every
  Claude session knows to delegate wiki edits.
- `samples/mcp/claude-code.mcp.json` → `.mcp.json` — wires `wikidown-mcp` into
  Claude Code.

## Copilot

In [`agents/copilot/`](https://github.com/markdav-is/Wikidown/tree/main/agents/copilot),
layered on top of the shared skill:

- `copilot-instructions.md` → `.github/copilot-instructions.md` — repo-wide
  guidance.
- `wikidown.agent.md` → `.github/agents/wikidown.agent.md` — a custom agent,
  also used by the Copilot coding agent on github.com.
- `wikidown.chatmode.md` → `.github/chatmodes/wikidown.chatmode.md` — a custom
  chat mode focused on wiki editing.
- `mcp.json` → `.vscode/mcp.json` — wires `wikidown-mcp` into VS Code.

## Install

The recommended install is the [CLI](CLI.md):

```sh
dotnet tool install -g Wikidown.Cli
wikidown init --agents all
```

`--agents` accepts `claude`, `copilot`, `all`, or `none`. Existing files are
skipped unless you pass `--force`; an existing `CLAUDE.md` gets the wiki
section appended if it doesn't mention the wiki already.

Manual copying still works: the
[`agents/README.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/README.md)
maps each source file to its destination. Once installed, the agents call the
`wiki_*` MCP tools so `.order` files and link targets stay consistent.
