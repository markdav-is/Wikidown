# Wikidown — Build Plan

Living document. Updated at the end of each chunk.

## Goal
An Azure DevOps Wiki–compatible wiki that lives in `/docs` of any code repo,
with a C# CLI, an MCP server, AI agent configs (Claude + Copilot), and a
Blazor WASM PWA editor + marketing site hosted on GitHub Pages.

## Confirmed scope (from user)
- **ADO-wiki compat:** MVP — pages + `.order` + links. Parking lot: `[[_TOC_]]`,
  mermaid, `:::` callouts, `/.attachments`, page-move history.
- **Online editing:** browser commits via GitHub/ADO REST using user's token.
  No backend.
- **Auth:** GitHub Device Flow, ADO PAT (fallback PAT for GitHub too).
- **Delivery:** ship in chunks, one commit per chunk, keep this plan updated.

## Architecture
```
/src
  Wikidown.Core/       shared lib: page model, filename<->title, .order, links, md I/O
  Wikidown.Cli/        dotnet tool: list/read/write/move/reorder/new/search
  Wikidown.Mcp/        MCP stdio server wrapping Core
  Wikidown.Web/        Blazor WASM PWA editor
  Wikidown.Site/       Blazor static marketing site
/tests
  Wikidown.Core.Tests/
/agents
  claude/              Claude Code agent/skill definitions
  copilot/             Copilot chat mode + instructions
/docs                  self-hosted wiki demo (also exercises the format)
.github/workflows/     CI + GH Pages deploy
```

## ADO-wiki MVP format rules
- Page file: `My-Page.md` on disk, title is `My Page` (hyphen ↔ space).
- Subpages: folder named same as parent page, containing child `.md` files.
- Ordering: `.order` file per folder — one page base-name per line, top→bottom.
- Links: `[text](/Parent/Child)` resolves via title, ignoring `.md` and hyphens.
- Root page is `/docs` (no single "home" file required for MVP).

## Chunks
1. **Core + CLI** — solution scaffold, model, CLI commands, unit tests. *(shipped)*
   - `Wikidown.Core`: `PageName`, `PagePath`, `OrderFile`, `WikiPage`,
     `WikiRepository` (list/read/write/move/delete/reorder/walk), `PageSearch`.
   - `wikidown` CLI: list / read / write / new / move / delete / reorder / search.
   - xUnit tests for PageName / PagePath / OrderFile / WikiRepository.
   - CI workflow: restore/build/test/pack.
   - Seed `/docs` with Getting-Started + Format pages and `.order` files.
2. **MCP server** — expose Core tools over stdio MCP. *(shipped)*
   - `wikidown-mcp` dotnet tool, stdio transport, `ModelContextProtocol` 1.2.0.
   - 9 tools: `wiki_list/read/write/new/move/delete/reorder/search/walk`.
   - Wiki root via `--root`, `WIKIDOWN_ROOT` env, or default `./docs`.
   - Sample configs for Claude Code (`.mcp.json`) and Claude Desktop.
   - Verified by stdio smoke test (initialize → tools/list → tools/call).
3. **Agents** — Claude + Copilot configs that use the MCP + CLI. *(shipped)*
   - `agents/claude/` — subagent (`wikidown-editor`), skill, CLAUDE.md snippet.
   - `agents/copilot/` — `.github/copilot-instructions.md`, `wikidown.chatmode.md`,
     and `.vscode/mcp.json`.
   - `agents/README.md` documents where each file goes in a downstream repo.
   - In-repo dogfood: installed all configs at `.claude/`, `.github/`,
     `.vscode/`, `.mcp.json`, plus a root `CLAUDE.md`.
4. **WASM editor PWA** — editor, Device Flow, PAT, REST commits. *(in progress)*
   - 4a: `Wikidown.Web` scaffold + PWA manifest + shell/routing. *(shipped)*
   - 4b: GitHub provider (PAT), read-only browse. *(shipped, Device Flow
     deferred — GitHub `/login/device/code` lacks CORS, so pure-browser flow
     needs a proxy; will revisit in 4f.)*
   - 4c: ADO provider (PAT), read-only browse. *(shipped)*
   - 4d: MudBlazor migration + markdown editor + CommonMark preview. *(shipped)*
     UI uses MudBlazor 8.15. Markdown rendered with
     `RamType0.Markdig.Renderers.MudBlazor` (no JS DOM manipulation, KaTeX-ready).
     Browse page has read mode (rendered) and edit mode (side-by-side editor +
     live preview). Save button is staged disabled — wired in 4e.
   - 4e: REST commits (GitHub Contents API + ADO Push) + conflict detection.
   - 4f: Offline/PWA cache polish + draft persistence.
5. **Marketing site + GH Pages deploy** — landing page + workflow.
6. **Self-hosted /docs demo + CI polish** — dogfood + green builds.

## Open questions / parking lot
- `[[_TOC_]]`, mermaid, `:::` callouts rendering in WASM preview.
- `/.attachments` upload from browser (REST base64 -> Contents API).
- Conflict resolution UX when remote HEAD moves during edit.
- ADO OAuth (vs PAT) — requires proxy; defer.
