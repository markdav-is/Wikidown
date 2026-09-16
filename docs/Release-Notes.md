[Home](Home.md) / Release Notes <!-- wikidown:breadcrumb -->

# Release Notes

What changed in each Wikidown release, and where to get it. Newest first.
For how to update an existing install, see
[Updating](Getting-Started/Updating.md).

## 0.8.0 — 15 September 2026

The MCP server is gone: the `wikidown` CLI is now the only surface AI
agents use. `Wikidown.Mcp` is no longer published, the `wikidown-mcp`
executable is retired, and the `wiki_*` tools go with it.

### Where to get it

| Component | Link |
|---|---|
| `wikidown` CLI (NuGet global tool) | [Wikidown.Cli 0.8.0](https://www.nuget.org/packages/Wikidown.Cli/0.8.0) — `dotnet tool update -g Wikidown.Cli` |
| `Wikidown.Core` library | [Wikidown.Core 0.8.0](https://www.nuget.org/packages/Wikidown.Core/0.8.0) |
| Self-contained CLI binaries (no .NET needed) | [GitHub Release cli-v0.8.0](https://github.com/markdav-is/Wikidown/releases/tag/cli-v0.8.0) — win/linux/osx, x64 and arm64; or re-run the [install script](CLI.md) |
| Source | [markdav-is/Wikidown](https://github.com/markdav-is/Wikidown) |

The Visual Studio extension and the browser editor are unchanged in this
release.

### Why

Every `wiki_*` MCP tool was a thin wrapper over the same `wikidown` verb,
so the CLI has had full parity since 0.7.0. Shipping two surfaces meant two
things to keep correct, two things to document, and two version numbers that
had to move together. One surface is easier to keep correct, and an agent
that can run a shell loses nothing.

### What to do

1. Uninstall the old server: `dotnet tool uninstall -g Wikidown.Mcp`.
2. Delete `.mcp.json` and `.vscode/mcp.json` from downstream repos — they
   only wired up `wikidown-mcp`.
3. Re-run `wikidown init --agents all --force` to refresh the agent
   configs. The skill, the Claude Code subagent, and the Copilot agent and
   chat mode now instruct agents to run the CLI — see [Agents](Agents.md).

The tool names map one-to-one onto CLI verbs, so prompts, notes, or
scripts that mention them translate directly:

| MCP tool | CLI verb |
|---|---|
| `wiki_list` | `wikidown list` |
| `wiki_read` (with `section`) | `wikidown read` (with `--section`) |
| `wiki_write` | `wikidown write` |
| `wiki_edit` | `wikidown edit` |
| `wiki_write_section` | `wikidown write-section` |
| `wiki_append` | `wikidown append` |
| `wiki_new` | `wikidown new` |
| `wiki_move` | `wikidown move` |
| `wiki_delete` | `wikidown delete` |
| `wiki_reorder` | `wikidown reorder` |
| `wiki_search` | `wikidown search` |
| `wiki_walk` | `wikidown walk` |

Chat-only hosts that can't run a shell (Claude Desktop, a VS Code chat mode
without a terminal) can no longer edit a wiki directly. The skill gives them
the command list, so they can suggest the exact `wikidown` commands for you
to run instead.

## 0.7.0 — 12 September 2026

The small-change toolkit for agents. Until now the only way to change a
page through the MCP server or CLI was to rewrite all of it; most of a long
editing session on a large wiki was whole-page round-trips for one-line
changes. This release adds four patching tools so an agent pays only for
the text it changes.

### Where to get it

| Component | Link |
|---|---|
| `wikidown` CLI (NuGet global tool) | [Wikidown.Cli 0.7.0](https://www.nuget.org/packages/Wikidown.Cli/0.7.0) — `dotnet tool update -g Wikidown.Cli` |
| `wikidown-mcp` MCP server (NuGet global tool) | [Wikidown.Mcp 0.7.0](https://www.nuget.org/packages/Wikidown.Mcp/0.7.0) — `dotnet tool update -g Wikidown.Mcp` |
| `Wikidown.Core` library | [Wikidown.Core 0.7.0](https://www.nuget.org/packages/Wikidown.Core/0.7.0) |
| Self-contained CLI binaries (no .NET needed) | [GitHub Release cli-v0.7.0](https://github.com/markdav-is/Wikidown/releases/tag/cli-v0.7.0) — win/linux/osx, x64 and arm64; or re-run the [install script](CLI.md) |
| Source | [markdav-is/Wikidown](https://github.com/markdav-is/Wikidown) |

The Visual Studio extension and the browser editor are unchanged in this
release.

### New: patching tools

All four are in the MCP server and the CLI. They match the raw markdown
exactly, detect and preserve the file's line endings (CRLF or LF), never
touch the breadcrumb line or `.order`, never create a page, and fail with
a message that says what to do next — the page's headings on a section
miss, the match count on an ambiguous edit. See [CLI](CLI.md) § Commands
(the MCP Server page was retired with 0.8.0).

- **`wiki_edit` / `wikidown edit`** — replace an exact substring in place,
  with the same contract as Claude Code's `Edit` tool: `old`, `new`,
  optional `replaceAll`. Returns the changed line numbers with two lines
  of context so the agent needn't re-read the page.
- **`wiki_read` with `section` / `wikidown read --section`** — return one
  heading plus everything under it up to the next heading of the same or
  higher level, so a `##` section includes its `###` children. Heading
  match is case-insensitive and ignores `#`s and whitespace; a miss lists
  every heading on the page.
- **`wiki_write_section` / `wikidown write-section`** — replace the body
  under one heading, keeping the heading line and the rest of the page.
  `###` children inside the section are replaced too. Exactly one blank
  line is kept around the new body, so repeated writes are idempotent.
  `createIfMissing` appends a new `##` section instead of failing.
- **`wiki_append` / `wikidown append`** — add a block at the end of a page,
  or at the end of one section with `afterSection`, always separated by
  exactly one blank line. Repeated appends never stack blank lines.

### Agent guidance

The shared skill, the Claude Code subagent, the Copilot agent and chat
mode, and the CLAUDE.md snippet now say: prefer `wiki_edit` for small
changes, `wiki_write_section` for one section, `wiki_append` to add at
the end, and `wiki_write` only for new pages or full rewrites. Re-run
`wikidown init --agents all --force` in a downstream repo to pick these
up — see [Agents](Agents.md).

### Fixes

- **MCP errors now say what went wrong.** The MCP SDK reported every
  failure as a bare `An error occurred invoking 'wiki_x'`, which hid
  messages such as `page already exists` from `wiki_new`. Every tool now
  returns its real message.
- **Concurrent tool calls no longer collide.** Hosts such as Claude Code
  batch independent tool calls, and two edits to the same page in one
  batch raced on the file. Tool calls are now serialized.
- **`wikidown --help` / `-h`** no longer demands a value or valid command
  arguments.

### Upgrading

Follow [Updating](Getting-Started/Updating.md). One gotcha specific to the
MCP server: `dotnet tool update -g Wikidown.Mcp` fails with
`Access to the path ... is denied` while any Claude Code, Claude Desktop,
or VS Code session has `wikidown-mcp` running. Close those sessions (or
stop every `wikidown-mcp` process) first, update, then reopen them. To
confirm you are on the new version, `dotnet tool list -g` must show
`0.7.0` for both tools, `wikidown edit --help` must print a usage block,
and the MCP tool list must show 12 tools including `wiki_edit`.

### Pull requests

[#23](https://github.com/markdav-is/Wikidown/pull/23) `wiki_edit` ·
[#24](https://github.com/markdav-is/Wikidown/pull/24) `wiki_read(section)` ·
[#25](https://github.com/markdav-is/Wikidown/pull/25) `wiki_write_section` ·
[#26](https://github.com/markdav-is/Wikidown/pull/26) `wiki_append` ·
[#27](https://github.com/markdav-is/Wikidown/pull/27) landing + version bump.
Issues [#19](https://github.com/markdav-is/Wikidown/issues/19),
[#20](https://github.com/markdav-is/Wikidown/issues/20),
[#21](https://github.com/markdav-is/Wikidown/issues/21),
[#22](https://github.com/markdav-is/Wikidown/issues/22).

## 0.6.0 — 2 September 2026

- `wikidown export-html`: render the wiki to a static site with the
  starter theme in .NET (Markdig + Fluid), no Jekyll or Ruby — the path
  for GitLab Pages, Azure Static Web Apps, and local preview.
- wikidown.org merged into `/docs`: the product site is now this wiki,
  published with `export-html`.
- Editor: GitLab provider.
- Fixes: `pages` and `export-html` nav linked `/Home` to `Home.html`
  instead of the site root.

Binaries: [cli-v0.6.0](https://github.com/markdav-is/Wikidown/releases/tag/cli-v0.6.0).
