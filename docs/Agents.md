[Home](Home.md) / Agents <!-- wikidown:breadcrumb -->

# Agents

Wikidown ships drop-in configs so AI coding assistants can maintain your wiki
through the [MCP Server](MCP-Server.md) without you wiring anything up by hand.

## Shared skill

Claude and Copilot share one skill file in the Agent Skills standard format:
[`agents/skills/wikidown/SKILL.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/skills/wikidown/SKILL.md).
It carries the format rules, tool cheat sheet, CLI fallback, workflow, and a
manual last-resort protocol for locked-down machines where neither the MCP
tools nor the CLI can run (no .NET plus an execution policy blocking the
self-contained binary) — breadcrumb format, `.order` bookkeeping, and the
move/delete invariants, so an agent can still edit correctly and flag the
edits for later `wikidown check-links` verification.

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

### Without .NET

The install scripts on [wikidown.org](https://wikidown.org) fall back to a
self-contained `wikidown` binary when `dotnet` isn't on `PATH` — nothing
else to install:

```sh
curl -fsSL https://wikidown.org/install.sh | sh
wikidown init --agents all
```

```powershell
irm https://wikidown.org/install.ps1 | iex
wikidown init --agents all
```

`init` scaffolds the same configs either way. One difference downstream:
the MCP server itself is NuGet-only (see [MCP Server](MCP-Server.md)), so
without .NET the `wiki_*` tools won't start. The shared skill already
covers this — it tells agents to fall back to the equivalent `wikidown` CLI
commands, which the self-contained binary provides (installed to
`~/.wikidown/bin`, or `%USERPROFILE%\.wikidown\bin` on Windows). If a local
execution policy blocks even that binary, the skill's last-resort protocol
keeps wiki maintenance possible with plain file edits.

Manual copying still works: the
[`agents/README.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/README.md)
maps each source file to its destination. Once installed, the agents call the
`wiki_*` MCP tools so `.order` files and link targets stay consistent.

## Keeping configs current

These files are copied once, not auto-updated. See
[Updating](Getting-Started/Updating.md) for how to re-run `wikidown init
--agents all --force` to pick up config changes shipped in newer Wikidown
releases.
