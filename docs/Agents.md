[Home](Home.md) / Agents <!-- wikidown:breadcrumb -->

# Agents

Wikidown ships drop-in configs so AI coding assistants can maintain your wiki
by running the [`wikidown` CLI](CLI.md) without you wiring anything up by hand.

## The CLI is the agent surface

There is no separate agent protocol: every config below tells the assistant
to shell out to `wikidown` — `list`, `read` (with `--section`), `write`,
`edit`, `write-section`, `append`, `new`, `move`, `delete`, `reorder`,
`search`, `walk`, plus `check-links` — instead of editing `/docs/*.md`
directly, so `.order` files, breadcrumbs, and link targets stay consistent.
Any host that can run a shell command (Claude Code, Copilot in VS Code or on
github.com, the Copilot coding agent) gets the full read/write surface.
Hosts that can't (Claude Desktop, a VS Code chat mode without terminal
access) can't edit the wiki directly; the skill still carries the command
list, so they can suggest the exact `wikidown` commands for you to run
instead.

## Shared skill

Claude and Copilot share one skill file in the Agent Skills standard format:
[`agents/skills/wikidown/SKILL.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/skills/wikidown/SKILL.md).
It carries the format rules, a `wikidown` command cheat sheet (which verb to
reach for and when), the editing workflow, and a manual last-resort protocol
for locked-down machines where the CLI can't run (no .NET plus an execution
policy blocking the self-contained binary) — breadcrumb format, `.order`
bookkeeping, and the move/delete invariants, so an agent can still edit
correctly and flag the edits for later `wikidown check-links` verification.
The same command list is what a shell-less host uses to suggest commands
rather than run them.

- Claude Code loads it from `.claude/skills/wikidown/SKILL.md`.
- GitHub Copilot loads it from `.github/skills/wikidown/SKILL.md`.

## Claude Code

In [`agents/claude/`](https://github.com/markdav-is/Wikidown/tree/main/agents/claude),
layered on top of the shared skill:

- `wikidown.subagent.md` → `.claude/agents/wikidown-editor.md` — a subagent
  definition that owns `/docs/*.md` reads and writes.
- `CLAUDE.md` snippet — append to your project's root `CLAUDE.md` so every
  Claude session knows to delegate wiki edits.

## Copilot

In [`agents/copilot/`](https://github.com/markdav-is/Wikidown/tree/main/agents/copilot),
layered on top of the shared skill:

- `copilot-instructions.md` → `.github/copilot-instructions.md` — repo-wide
  guidance.
- `wikidown.agent.md` → `.github/agents/wikidown.agent.md` — a custom agent,
  also used by the Copilot coding agent on github.com.
- `wikidown.chatmode.md` → `.github/chatmodes/wikidown.chatmode.md` — a custom
  chat mode focused on wiki editing. When the chat mode has no terminal it
  suggests the `wikidown` commands to run rather than running them.

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

`init` scaffolds the same configs either way, and the agents run the same
`wikidown` commands — the self-contained binary provides them (installed to
`~/.wikidown/bin`, or `%USERPROFILE%\.wikidown\bin` on Windows). If a local
execution policy blocks even that binary, the skill's last-resort protocol
keeps wiki maintenance possible with plain file edits.

Manual copying still works: the
[`agents/README.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/README.md)
maps each source file to its destination. Once installed, the agents run
`wikidown` commands rather than editing files by hand, so `.order` files,
breadcrumbs, and link targets stay consistent.

## Keeping configs current

These files are copied once, not auto-updated. See
[Updating](Getting-Started/Updating.md) for how to re-run `wikidown init
--agents all --force` to pick up config changes shipped in newer Wikidown
releases.
