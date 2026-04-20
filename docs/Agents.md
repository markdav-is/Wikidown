# Agents

Wikidown ships drop-in configs so AI coding assistants can maintain your wiki
through the [MCP Server](/MCP-Server) without you wiring anything up by hand.

## Claude Code

In [`agents/claude/`](https://github.com/markdav-is/Wikidown/tree/main/agents/claude):

- `wikidown-editor.md` — a subagent definition that owns `/docs/*.md` reads
  and writes.
- `wikidown/SKILL.md` — a skill the main agent can call for ad-hoc wiki work.
- `CLAUDE.md` snippet — copy into your project's root `CLAUDE.md` so every
  Claude session knows to delegate wiki edits.

## Copilot

In [`agents/copilot/`](https://github.com/markdav-is/Wikidown/tree/main/agents/copilot):

- `.github/copilot-instructions.md` — repo-wide guidance.
- `wikidown.chatmode.md` — a custom chat mode focused on wiki editing.
- `.vscode/mcp.json` — wires `wikidown-mcp` into VS Code.

## Install

Copy each file to the matching path in your repo. The
[`agents/README.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/README.md)
maps each source file to its destination. Once installed, the agents call the
`wiki_*` MCP tools so `.order` files and link targets stay consistent.
