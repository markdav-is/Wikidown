# Wikidown — Build Plan

Living document. Updated at the end of each chunk.

## Goal
A structured markdown wiki that lives in `/docs` of any code repo,
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

## Wiki MVP format rules
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
     *(shipped)* GitHub uses PUT contents with file blob `sha`; ADO push uses
     branch `oldObjectId` plus a pre-flight item read to detect file-level
     drift. Conflicts surface a "Reload remote" banner in the editor.
   - 4f: Draft persistence. *(shipped)*
     `DraftStore` keeps in-progress page edits in localStorage per
     `(provider, owner, project, repo, branch, page)`; the editor restores
     them on reload (with a "use remote" escape hatch) and clears them on
     successful commit. The published service worker already caches
     MudBlazor + manifest assets, so the app boots offline and a failed
     Save naturally surfaces as a snackbar error.
5. **Marketing site + GH Pages deploy** — landing page + workflow. *(shipped)*
   - `src/Wikidown.Site/` — static HTML/CSS landing page (no WASM cost on
     first paint). Topbar, hero, "How it works" cards, "For agents" cards
     (CLI / MCP / drop-in configs), Format reference, footer.
   - `.github/workflows/pages.yml` — publishes `Wikidown.Web` with
     `StaticWebAssetBasePath=app`, stages the marketing site at the Pages
     root, copies `wwwroot/app/.` to `publish/site/app/`, sed-rewrites
     `<base href="/" />` to `/Wikidown/app/`, copies `index.html` to
     `404.html` for SPA fallback, and touches `.nojekyll`.
   - Marketing site links to `app/` for the editor; editor base href is
     correct under both root and project-pages deployments.
6. **Self-hosted /docs demo + CI polish** — dogfood + green builds. *(shipped)*
   - `/docs` expanded to five top-level pages (`Getting-Started`, `CLI`,
     `MCP-Server`, `Editor`, `Agents`) so the wiki documents every public
     surface of the project. All pages authored via the CLI so `.order`
     stays consistent.
   - `ci.yml`: added a "Publish editor (smoke)" step so a broken Blazor
     publish fails PRs (previously it only failed `pages.yml` on main),
     plus a "Walk /docs with the CLI" step that runs `wikidown list` and
     `wikidown search` against the in-repo wiki. Dogfoods the CLI on
     every push/PR.

7. **VS Extension** — Visual Studio 2022+ VSIX that adds a Wikidown project type. *(shipped)*
   - `src/Wikidown.Vs/` — net472 VSIX project (MS.VisualStudio.SDK 17.x).
   - Project type GUID `{6a9c3f4b-d5e8-4f0a-b1c2-345678901bcd}` registered via
     `[ProvideProjectFactory]` in `WikidownPackage` (AsyncPackage).
   - `WikidownProjectFactory` creates `WikidownProject` instances for `.wikidownproj` files.
   - `WikidownProject` implements `IVsHierarchy`/`IVsProject`/`IVsUIHierarchy`:
     reads `<WikiRoot>` from the `.wikidownproj` XML (defaults to `docs/`),
     recursively populates Solution Explorer with `.md` and `.order` files,
     opens files via `IVsUIShellOpenDocument`, never implements build interfaces.
   - Project template (`ProjectTemplate/`) wired into VSIX as a
     `Microsoft.VisualStudio.ProjectTemplate` asset — surfaces in
     **Add → New Project → Wikidown Wiki**.
   - `publish.json` + `README.marketplace.md` for VS Marketplace listing.
   - `.github/workflows/vsix.yml` — `windows-latest` runner, MSBuild, NuGet
     restore, VSIX build; publishes to Marketplace on version tags via
     `VsixPublisher.exe` when `VSIX_PAT` secret is set; attaches `.vsix` to
     the GitHub Release.

8. **Agents rework — dual-target Claude + Copilot.** *(shipped)*
   - `agents/skills/wikidown/SKILL.md` — one shared skill in the Agent Skills
     standard (`SKILL.md`) format, consumed by Claude Code
     (`.claude/skills/`) and GitHub Copilot (`.github/skills/`). Carries the
     format rules, tool cheat sheet, CLI fallback, and workflow; the removed
     `agents/claude/wikidown.skill.md` was its Claude-only predecessor.
   - Per-agent wiring stays thin: Claude subagent + CLAUDE.md snippet;
     Copilot instructions + custom agent (`wikidown.agent.md`, promoted from
     dogfood into `agents/copilot/`) + chat mode + `.vscode/mcp.json`.
   - `wikidown init [--root docs] [--agents claude|copilot|all|none] [--force]`
     — seeds an empty wiki with `/Home` and installs the agent configs from
     embedded resources (single-sourced from `agents/` via csproj links).
     Existing files are skipped unless `--force`; an existing `CLAUDE.md`
     gets the wiki section appended when it lacks one.
   - Dogfood configs refreshed; `.github/skills/wikidown/SKILL.md` added.
   - `InitCommandTests` cover scaffolding, filtering, skip/force, and
     CLAUDE.md append behavior.

9. **.NET 10 modernization — file formats + startup.** *(shipped)*
   - `Wikidown.sln` → `Wikidown.slnx` (new XML solution format);
     `Wikidown.slnf` retargeted at the `.slnx`; `vsix.yml` restores via
     `msbuild /t:Restore` instead of `nuget restore`.
   - Central package management: `Directory.Packages.props` owns all
     versions; csprojs carry version-less `PackageReference`s. `Wikidown.Vs`
     opts out (`ManagePackageVersionsCentrally=false`) to keep VSSDK pins
     local.
   - `global.json` → SDK 10.0.302 (rollForward latestFeature).
   - `Wikidown.Api`: net9.0 → net10.0 (inherits from
     `Directory.Build.props`), startup rewritten from `new HostBuilder()` to
     `FunctionsApplication.CreateBuilder`, Functions worker + App Insights
     packages bumped to latest.
   - Package bumps: ModelContextProtocol 1.2.0 → 2.0.0 (stdio smoke-tested:
     initialize + tools/list return all nine `wiki_*` tools),
     Microsoft.Extensions.Hosting / AspNetCore WASM → 10.0.10, test stack →
     Microsoft.NET.Test.Sdk 18.8.1 / xunit 2.9.3 / runner 3.1.5. MudBlazor
     stays 8.15.0 (9.x is a breaking UI migration; the Markdig renderer
     hasn't moved past 0.15.0).
   - `Wikidown.Web` csproj slimmed — TargetFramework/Nullable/ImplicitUsings
     now inherited from `Directory.Build.props`.

10. **Link integrity — relative body links, `check-links`, link-aware move.** *(shipped)*
    - Agent configs (`SKILL.md`, `CLAUDE.md`, subagent, Copilot agent/chatmode)
      now prescribe relative `.md` body links (`../Parent/Child.md`) and
      relative `.attachments` image paths instead of absolute title paths,
      which 404 when a page is browsed directly on github.com. Tool
      *addressing* (`wiki_read path=...`) is unaffected — still title form.
    - `Wikidown.Core.LinkChecker` + `wikidown check-links [--no-absolute-check]`
      walk every page, resolve each relative link/image against the linking
      page's folder, and report broken targets plus (by default) absolute
      title-path body links. Non-zero exit on any issue, for CI.
    - `Wikidown.Core.MoveLinkRewriter` + link-aware `move`/`wiki_move`: plans
      the rewrite from pre-move content (inbound links to the moved
      subtree, and the moved pages' own relative links/images re-depthed),
      moves the files, then applies it. CLI `move` gains `--dry-run`;
      both CLI and `wiki_move` report what was rewritten.
    - Fixes #15, #13, #14.

11. **Breadcrumb navigation.** *(shipped)*
    - `Wikidown.Core.Breadcrumb` computes a one-line ancestor trail purely
      from a page's path segments (no ancestor page needs to be read),
      e.g. `[Encounters](../Encounters.md) / The Sky Hunters`, using the
      same relative-link convention as body links. Null for top-level
      pages (nothing to show).
    - `WikiRepository.Write` injects/refreshes it as the page's first line
      on every save — idempotent via an HTML comment marker
      (`<!-- wikidown:breadcrumb -->`) that's stripped and regenerated
      each time, so it never accumulates and a caller never has to
      preserve it through a read-edit-write round trip. `wikidown new` /
      `wiki_new` get it for free since they write through the same path.
    - `WikiRepository.Move` fully regenerates the breadcrumb for the moved
      page and every moved descendant, rather than relying on
      `MoveLinkRewriter`'s generic link-target patching — a move can
      change *which* ancestors a page has, not just the hop count to
      reach them, so patching in place can leave a structurally wrong
      breadcrumb whose link still happens to resolve. `MoveLinkRewriter`
      skips breadcrumb lines entirely during a move's link-rewrite pass
      to avoid double-work and a misleading "rewritten" count.
    - Breadcrumb links are ordinary relative markdown links, so
      `check-links` validates them like any other link with no special
      casing.
    - `/docs` backfilled (all five existing subpages) and
      `Getting-Started/Format` documents the behavior.

## Open questions / parking lot
- `[[_TOC_]]`, mermaid, `:::` callouts rendering in WASM preview.
- `/.attachments` upload from browser (REST base64 -> Contents API).
- Conflict resolution UX when remote HEAD moves during edit.
- ADO OAuth (vs PAT) — requires proxy; defer.
