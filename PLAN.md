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
  Wikidown.Core/       shared lib: page model, filename<->title, .order, links, md I/O,
                        markdown->PDF intermediate representation (PdfExport/)
  Wikidown.Cli/        dotnet tool: list/read/write/move/reorder/new/search/export-pdf
  Wikidown.Mcp/        MCP stdio server wrapping Core
  Wikidown.Pdf/        renders Wikidown.Core's PDF IR to an actual PDF via PDFsharp/MigraDoc
  Wikidown.Html/       Jekyll-compatible starter theme + static HTML export (Markdig + Fluid)
  Wikidown.Web/        Blazor WASM PWA editor
  (marketing site)     merged into /docs — wikidown.org is the wiki + a hand-authored
                        index.html, published by pages.yml via `wikidown export-html`
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

12. **Index-page auditing.** *(shipped)*
    - `Wikidown.Core.IndexChecker` audits the "every folder needs a linked
      index page" invariant from issue #16, folded into
      `check-links [--no-index-check]`. Found a real, previously invisible
      gap: `WikiRepository.Write` can create `/A/B` without `/A` ever
      existing — repo.Walk() (and everything built on it — `list`,
      `search`, check-links' own link scan) can't see the orphaned subtree,
      since it only descends into a page's subpage folder once that page
      has itself been discovered. `IndexChecker` walks the raw filesystem
      instead, specifically to find folders Walk() can't reach.
    - Two issue kinds: `MissingParentPage` (folder exists, `<Folder>.md`
      doesn't) and `ChildNotLinked` (parent exists but its body has no
      link — relative or absolute — resolving to that child; `.order`
      doesn't count, since it's invisible on GitHub's raw file view).
    - Directly relevant to breadcrumbs (chunk 11): a breadcrumb link
      always points at an ancestor's `.md` file, but never verified that
      file exists — `IndexChecker` is what actually catches a dangling
      breadcrumb caused by a missing index page, plus the child-side
      "you can't get there from the parent" case breadcrumbs don't touch
      at all (they only link upward).
    - Dogfooding this against `/docs` found and fixed a real instance:
      `/Meta.md` never linked its own `/Meta/Vibing-Phase-Recap` child.
    - `Getting-Started/Format` gets a new § Index Pages section.

13. **`backfill-breadcrumbs` command.** *(shipped)*
    - One-time catch-up for wikis that predate breadcrumb navigation
      (chunk 11) — dnd-lost-ship's pages, for instance, were all written
      before that shipped, so they have no breadcrumb line and won't get
      one until re-saved. `wikidown backfill-breadcrumbs [--dry-run]`
      walks every page, and for each one whose current content differs
      from what `Breadcrumb.Inject` would produce, re-`Write`s it (which
      performs the actual injection) and reports it; already-backfilled
      pages are a no-op, so it's safe to re-run.
    - CLI-only for now, consistent with `check-links` (no MCP counterpart
      yet). Going forward, `write`/`new`/`move` (CLI and MCP) all maintain
      breadcrumbs automatically — this command only exists to catch up
      pages written before that logic existed, or by a tool version that
      predates it.

14. **Breadcrumb always leads back to `/Home`.** *(shipped)*
    - User feedback on chunk 11: top-level pages got no breadcrumb at
      all, and nested pages' breadcrumbs stopped at their nearest
      ancestor rather than reaching all the way back to the wiki's own
      entry point — "we should always be able to get home."
    - `Breadcrumb.Render` now takes the `WikiRepository` and checks
      whether `/Home` exists (the page `wikidown init` seeds). If it
      does, every page's breadcrumb leads with a link to it — including
      top-level pages, which previously got no breadcrumb line at all —
      except `/Home` itself (no self-link) and pages already rooted under
      `/Home` (no duplicate segment). If a wiki has no `/Home` page,
      behavior is unchanged from chunk 11: no fabricated link to a page
      that isn't there.
    - `backfill-breadcrumbs` needed a matching fix: it used to skip
      top-level pages outright (nothing to backfill, before `/Home`
      support existed) — now it considers every page, since a top-level
      page can newly need a `Home` link once `/Home` is created after
      the fact.
    - `Breadcrumb.Inject`/`Render` signatures changed to take the repo;
      updated at both call sites (`WikiRepository.Write` and `.Move`'s
      breadcrumb-refresh step) and in tests.

15. **`/docs` gets a `/Home` page.** *(shipped)*
    - This repo's own `/docs` predated the `/Home` convention (`wikidown
      init` seeds it for new wikis, but this wiki wasn't scaffolded that
      way), so chunk 14's Home-anchored breadcrumb had nothing to anchor
      to here — the one wiki that should be dogfooding it hardest was
      exempt. Added `/Home` as a real landing page linking all seven
      top-level sections (including Testing and Meta, which
      `Getting-Started`'s own "Next" list had omitted — fixed there too),
      reordered root `.order` to put it first, then ran
      `backfill-breadcrumbs` to refresh all twelve existing pages with
      the new `[Home](...) / ...` lead-in.

16. **`export-pdf` — combine the wiki into one linked PDF.** *(shipped)*
    - `wikidown export-pdf --output <path> [--from /Link/Path] [--title T]
      [--no-cover] [--no-toc] [--allow-html-skip]` renders the whole wiki
      (or a subtree) into one PDF: an optional cover page, an in-document
      table of contents with page numbers, then one section per page with
      the sidebar bookmark/outline panel nested to match the wiki's nav
      hierarchy. Internal wiki links (relative `.md` links, legacy absolute
      title-paths, and same-page `#fragment` links) become real in-PDF
      jumps instead of dead hrefs.
    - Split into two new pieces to keep the PDF dependency out of
      `Wikidown.Web`'s WASM bundle and `Wikidown.Mcp`: `Wikidown.Core`
      gains a `PdfExport/` namespace (`MarkdownIrBuilder`, `PdfAnchors`,
      `WikiPdfContent`) that turns a page's markdown (via Markdig) into a
      plain, PDF-library-agnostic block/run IR — no MigraDoc types, so
      it's usable anywhere Core already is. A new project,
      `Wikidown.Pdf`, is the only place touching MigraDoc/PDFsharp
      (`MigraDocRenderer`), consumed only by `Wikidown.Cli` today; its
      `Render(content, Stream, options)` shape doesn't assume a
      console/file host, so a future non-CLI caller (e.g. `Wikidown.Api`
      streaming a browser download) can reuse it without rework.
    - Library pivot mid-build: started with QuestPDF, dropped it once its
      own maintainers confirmed there's no API to draw a real PDF
      outline/bookmark panel (GitHub discussion #181) — shipping a
      TOC-only substitute would have silently undersold "matches the nav
      hierarchy." Landed on `PDFsharp-MigraDoc` (MIT, no revenue-based
      license restriction), whose `Heading1`-`Heading9` styles map
      directly to real `OutlineLevel`s, so the per-page heading depth
      (`NavTree`-derived) drives a genuine sidebar outline for free.
    - Font resolution is cross-platform: the `PDFsharp-MigraDoc` package
      ships with no font resolver by default on any platform. The initial
      `GlobalFontSettings.UseWindowsFontsUnderWindows` shortcut worked
      locally but silently broke both GitHub Actions CI (`ubuntu-latest`)
      and any non-Windows host — discovered only once a version bump
      triggered a NuGet release and the workflow failed. Replaced with
      `EmbeddedFontResolver`, a custom `IFontResolver` serving DejaVu
      Sans/DejaVu Sans Mono TTFs embedded as resources in `Wikidown.Pdf`
      (Bitstream Vera License, redistribution explicitly permitted) — so
      rendering no longer depends on what fonts the host OS has installed.
    - Broken images/links degrade instead of failing the export: a missing
      relative image target renders a visible placeholder and is reported
      as `warning: {page}: image not found: {target}` with exit code 1 —
      the same non-fatal, exit-code-reflects-issues contract `check-links`
      already uses. Raw HTML blocks are the one thing that fails loudly by
      default (`--allow-html-skip` to degrade instead), consistent with
      "no silent drops."
    - Verified end to end against this repo's own `/docs` wiki (19 real
      pages, code fences, a table, a genuine raw-HTML page that correctly
      tripped the fail-loud path) by reading the rendered PDF back, not
      just by passing the test suite.
    - Not done here, deliberately: a `/docs` page documenting `export-pdf`
      (follow-up via the `wikidown-editor` subagent, never a direct edit
      per `CLAUDE.md`), and multi-targeting `Wikidown.Core`/`Wikidown.Pdf`
      to `net472` so `Wikidown.Vs` could add an "Export to PDF" command —
      `Wikidown.Vs` doesn't reference `Wikidown.Core` at all today (it
      hand-duplicates `.order`/page-listing logic), so that's a separate,
      real migration affecting every existing Core consumer, not something
      to take on silently as a side effect of this chunk.
    - Follow-up fix, found by asking "does it support block quotes?": it
      didn't, and worse than a graceful degrade — `MarkdownIrBuilder`'s
      block-type switch had no `QuoteBlock` case, so a `>` anywhere in a
      page aborted the *entire* export with `NotSupportedException`, no
      PDF written at all. Added `IrBlockQuote` (any nested block content,
      not just paragraphs, matching Markdig's own `QuoteBlock` shape) and
      MigraDoc rendering — left border, italic, indented, deeper for each
      level of `>>` nesting. Verified by reading back a rendered PDF with
      nested quotes containing bold/link runs, not just the test suite.

17. **Web editor: export the wiki to PDF via browser print.** *(shipped)*
    - `Wikidown.Web`'s `/export` page (linked from a new toolbar button on
      `/browse`) assembles the whole connected wiki into one print-friendly
      HTML document — `PdfExportHtmlBuilder` walks the `.order`-respecting
      `NavTree`, strips each page's breadcrumb, rewrites internal wiki
      links to in-page `#page-...` anchors, and renders with the same
      Markdig pipeline (`UsePipeTables`/`UseAutoIdentifiers`) the CLI's IR
      builder uses — then hands off to `window.print()` for the browser's
      own Save-as-PDF.
    - Deliberately not CLI parity: MigraDoc can't run in Blazor WASM, and
      routing through `Wikidown.Api` to render server-side would mean
      sending repo contents to the server for the first time — a real
      change to `docs/Editor.md`'s "the Functions app never sees your repo
      contents" privacy claim, not something to do silently. This path
      never leaves the browser, so nothing about that claim changes. No
      real embedded PDF outline/bookmarks or MigraDoc typography — "good
      enough browser print," not a second renderer to keep in sync with
      the CLI's.
    - Verified via the browser preview against this repo's own `/docs`
      wiki: nav-ordered TOC, breadcrumb stripped, and internal links
      (absolute, same-level, and multi-level `../`) all resolve to the
      right in-page anchor — checked with a standalone harness exercising
      `PdfExportHtmlBuilder` directly, since GitHub's Contents API doesn't
      allow a truly anonymous (empty-token) read against a public repo
      from this backend.

18. **VS extension: "Export to PDF..." context menu, on any node.** *(shipped)*
    - Right-clicking the project root, a folder, or a page in Solution
      Explorer now shows "Export to PDF...", scoped to that node and all of
      its descendants (root exports the whole wiki). Prompts for a save
      location, then shells out to the same `export-pdf` the CLI ships,
      with a `Yes/No` "open the PDF?" prompt on completion (or the CLI's
      own stderr on failure).
    - Real bug fixed along the way, not VS-specific: `export-pdf --from
      /Path` (`WikiPdfContent.BuildAll`) walked `WikiRepository.Walk(from)`,
      which yields `from`'s *descendants* only — the page at `from` itself
      was silently dropped from every scoped export, CLI included, contrary
      to `docs/CLI.md`'s own "a subtree, with `--from`" description. Fixed
      by prepending `from` to the walked paths when it's itself a real
      page; a bare folder (no paired page) still scopes to descendants only,
      unchanged. `WikiPdfContentTests` covers both cases now.
    - No in-process render: `Wikidown.Core`/`Wikidown.Pdf` target net10.0,
      `Wikidown.Vs` targets net472 (a VS SDK requirement), and MigraDoc's
      dependency surface makes a net472 multi-target a real migration, not
      a one-command fix — deferred, not attempted here (see the standing
      parking-lot item). Instead, `Wikidown.Vs.csproj` gains a
      `PublishBundledCli` build target that `dotnet publish`es
      `Wikidown.Cli` (framework-dependent, no RID) straight into the VSIX
      under `Tools\cli\`; the command shells out to it via `dotnet exec`.
      Reuses the exact same tested renderer (embedded DejaVu fonts
      included) with no separate install step for VSIX users, at the cost
      of VSIX size and a `dotnet` on `PATH` runtime dependency — both
      judged acceptable for a tool whose whole ecosystem already assumes
      the .NET 10 runtime is present.
    - Verified end to end: built the VSIX, confirmed `Tools\cli\` contains
      `wikidown.dll` + its dependencies, then invoked the published CLI
      exactly as the extension does (`dotnet exec wikidown.dll export-pdf
      --from /Getting-Started ...`) against this repo's own `/docs` and
      read the resulting PDF back — correct cover, TOC, and outline scoped
      to `Getting-Started` and its three children.

19. **Self-contained native CLI binaries + a smart install script.** *(shipped)*
    - Goal: someone tells an LLM coding agent "install wikidown from
      wikidown.org and init" in an empty repo and it just works — no
      manual .NET setup. Before this, the CLI was NuGet-tool-only
      (`dotnet tool install -g Wikidown.Cli`), which fails outright with
      no `dotnet` on `PATH`.
    - New workflow `.github/workflows/cli-native.yml`, triggered by a
      `cli-v*` tag (mirrors the existing `vsix-v*` convention, kept as a
      separate track from the NuGet release so cutting a NuGet version
      doesn't force a 6-way native build every time): matrix-publishes
      `dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true`
      for win-x64/win-arm64/linux-x64/linux-arm64/osx-x64/osx-arm64,
      attaches all six archives to the GitHub Release.
    - Deliberately **no trimming/AOT** — verified locally (win-x64,
      linux-x64, osx-arm64 all self-contained-publish cleanly with zero
      code changes) that plain self-contained publish already works,
      including `export-pdf`'s reflection-heavy MigraDoc/PDFsharp font
      path; trimming that same reflection-based code is a real risk not
      worth taking for a ~80MB-per-platform size win.
    - New `src/Wikidown.Site/install.sh` / `install.ps1` (served at
      `wikidown.org/install.sh` for free via the existing `pages.yml`
      deploy — no new hosting). Both are a **single smart entry point**,
      not two options for the LLM to pick between: `dotnet tool install`
      if `dotnet` is already on `PATH`, otherwise resolve the RID and pull
      the matching binary from the newest `cli-v*` GitHub Release. Query
      the releases API and filter by tag prefix rather than trusting
      `/releases/latest` — this repo now has two independent release
      tracks (`vsix-v*` and `cli-v*`) sharing one Releases list, so
      "latest" doesn't mean what it sounds like.
    - Unsigned-binary handling is built into the scripts, not left as a
      caveat: `install.ps1` runs `Unblock-File` (strips the
      mark-of-the-web that triggers SmartScreen) and `install.sh`
      defensively strips `com.apple.quarantine` on macOS. No code
      signing/notarization pipeline in this pass — a real follow-up if it
      becomes a problem, not attempted here.
    - Real bug caught by actually running the script, not just reading
      it: `install.ps1`'s first draft used `try { dotnet tool install }
      catch { dotnet tool update }` to handle "already installed" —
      PowerShell's `try/catch` doesn't react to a native command's
      non-zero exit code (only real .NET exceptions), so that fallback
      would never have fired. Turned out not to matter: `dotnet tool
      install` already exits 0 and no-ops cleanly when the tool is
      already present, so the whole fallback was solving a problem that
      didn't exist — simplified to a plain install call with an explicit
      `$LASTEXITCODE` check for genuine failures.
    - `Wikidown.Mcp` self-contained binaries and code signing are
      explicitly out of scope here — the user asked for "the CLI and the
      skills" specifically; MCP stays NuGet-tool-only for now.
    - Follow-up: `wikidown.org`'s "Getting started" now leads with the
      agent-first flow — a literal copy-pasteable prompt ("Install
      Wikidown from wikidown.org and run `wikidown init` in this repo.")
      in a visually distinct lane (accent border, quote-styled prompt
      block, not a shell `pre`) before the terminal one-liners, since
      those are two different audiences reading the same section: a human
      typing commands vs. a human handing an instruction to their agent.
      New section placed right after the hero — the most prominent
      position on the page — rather than folded into the existing "Built
      for humans and agents" deep-dive section further down, which stays
      as the CLI/MCP/agent-configs reference material it already was.

20. **`wikidown pages` — publish the wiki with GitHub Pages + Jekyll.** *(shipped)*
    - Goal: a Wikidown user flips **Settings → Pages → main, /docs** and
      gets a real site with a left-nav tree, using only GitHub's built-in
      Jekyll builder — no Actions workflow, no custom plugins (the Pages
      builder only runs its whitelisted set).
    - `wikidown pages [--title T] [--force]` scaffolds into the wiki
      root: `_config.yml` (GFM, `jekyll-relative-links` so the format's
      relative `.md` links and breadcrumbs resolve, `titles-from-headings`
      so pages need no front matter, `include: [.attachments]` because
      Jekyll skips dot-folders, default layout), `index.html` (Liquid redirect to `/Home.html` or the
      first top-level page), and the starter theme —
      `_layouts/wikidown.html`, `_includes/nav-tree.html` (recursive
      include), `assets/wikidown.css` (wikidown.org palette; collapsible
      `<details>` tree, active page highlighted, ancestors auto-expanded,
      slide-in drawer under 60rem). Theme files are never overwritten
      without `--force`; embedded as resources in `Wikidown.Cli` from
      `src/Wikidown.Cli/PagesTheme/`.
    - `.order` → nav: Jekyll can't read `.order`, so `Core.JekyllNavigation`
      renders `NavTree` to `_data/navigation.yml` (title/url/prefix/children).
      `WikiRepository.Write/Delete/Move/WriteOrder` call
      `RefreshIfEnabled`, so once the file exists every CLI *and* MCP edit
      regenerates it — the sidebar can't drift from the wiki. Never
      created for wikis that didn't opt in. The layout falls back to a
      flat `site.pages` list if the data file is missing.
    - `IndexChecker` now skips `_`-prefixed folders and folders with no
      markdown beneath them (`_layouts`, `_data`, `assets`), so the
      scaffold doesn't trip `check-links`' "no index page" audit.
    - Verified against a scratch copy of this repo's own `/docs`:
      `pages` + `check-links` reports exactly the 11 pre-existing
      example-link issues and nothing new; one page hand-rendered through
      the layout with the generated nav and checked in a browser (tree
      order, active/open state, responsive drawer). No Ruby/Jekyll on the
      dev box — deliberately: `pages` adds no Ruby/Python/Node
      dependency, GitHub's builder is the only Jekyll in the story — so the
      Liquid itself hasn't been executed locally — first
      real Pages deploy by a user is the remaining proof point.
    - Not dogfooded on this repo's Pages: wikidown.org already occupies the
      repo's one Pages site. Docs: `/Getting-Started/Publishing-to-GitHub-Pages`.

21. **`wikidown export-html` — the same theme, rendered in .NET.** *(shipped)*
    - Goal: publish on hosts that don't run Jekyll for you (GitLab Pages,
      Azure Static Web Apps, Netlify, a file share) and preview locally,
      with no Ruby anywhere. Chose Markdig + Fluid (Sébastien Ros's
      Liquid engine) over Pretzel (dormant Jekyll clone) and Statiq
      (different template language): it renders the *same* theme files
      GitHub's Jekyll uses, so one theme serves both paths.
    - New `src/Wikidown.Html/`: owns the embedded theme (moved from
      `Wikidown.Cli/PagesTheme/`; `ThemeResources` serves both `pages` and
      the exporter), `HtmlExporter`, `MarkdownPageRenderer` (Markdig with
      GitHub auto-ids; rewrites relative `.md[#frag]` links to `.html`
      exactly like `jekyll-relative-links`, title from first `#` heading
      like `jekyll-titles-from-headings`), `SiteConfig` (reads the handful
      of top-level scalars from `_config.yml` — deliberately not a YAML
      parser), and `ThemeFiles` (wiki-root theme files layered over the
      embedded defaults, so export works on unscaffolded *and* customized
      wikis; also an `IFileProvider` for Fluid includes).
    - Jekyll-dialect compatibility: Fluid's `include` is
      `{% include 'f', a: b %}` / `a`, Jekyll's is `{% include f a=b %}` /
      `include.a`. `ThemeFiles.ToFluidSyntax` rewrites the former from the
      latter at load time, so theme authors write plain Jekyll. The
      layout's no-nav fallback dropped `where_exp` (Jekyll-only filter)
      for an `if` inside the loop. `relative_url` is a registered filter
      that prefixes `--base-url` / config `baseurl`.
    - CLI: `export-html --output <dir> [--base-url /p] [--title T]
      [--clean]`. Copies `assets/` and `.attachments/`, renders
      `index.html` (front matter stripped, placeholders filled). `ci.yml`
      now exports `/docs` on every push and asserts a page exists.
    - Verified on this repo's `/docs` (15 pages) served statically in a
      browser: nav order/active/open state, breadcrumb + heading anchors,
      tables, code blocks, zero leftover Liquid. 16 new tests.
    - Docs: GitLab section now a one-job pipeline on the .NET SDK image;
      local preview is `export-html` + any static server.

22. **Merge the marketing site into `/docs` — dogfood the theme as wikidown.org.** *(shipped)*
    - `src/Wikidown.Site/` is gone: its `index.html`, `site.css`, `images/`,
      favicons, and `install.sh`/`install.ps1` moved into `docs/`, and
      `wikidown pages` scaffolded the theme there (`_config.yml`,
      `_layouts`, `_includes`, `assets`, `_data/navigation.yml` — which now
      auto-regenerates on every wiki edit made with current tooling). The
      marketing page stays hand-authored HTML as the wiki root's
      `index.html` — the scaffold's no-overwrite rule means `pages` leaves
      it alone (only `--force` would clobber it; known foot-gun).
    - `pages.yml` now builds with `wikidown export-html` (setup-dotnet →
      export → `.nojekyll` + CNAME → deploy-pages), triggered by `docs/**`
      and the exporter's source. Every wiki edit redeploys wikidown.org;
      CI's export step gates broken exports before they reach main.
    - Three product features the merge forced, all shipped here:
      - **`wikidown: exclude_from_site:` in `_config.yml`** — subtrees
        omitted from the published site (pages *and* nav) by `export-html`
        and from `_data/navigation.yml` by `JekyllNavigation`. Parsed by
        `Core.PublishExclusions` (shared by both). Publishing-only:
        excluded pages stay first-class for the CLI/MCP/editor/check-links.
        wikidown.org excludes `/Meta` and `/Testing`; their bullets were
        also dropped from `/Home` and `/Getting-Started` so the site has
        no dead links (deliberate trade: they're no longer body-linked on
        github.com either, just file-browsable).
      - **Root static passthrough in `export-html`** — everything in the
        wiki root ships verbatim (images, install scripts, favicons,
        `site.css`, `.attachments`) except wiki sources (`.md`, `.order`),
        `_`-prefixed Jekyll machinery, other dot-files, Gemfiles, and the
        separately-rendered root `index.html`. Mirrors Jekyll's
        copy-through; keeps `wikidown.org/install.sh` load-bearing URLs
        working with no extra hosting.
      - **`favicon:` in `_config.yml`** — rendered into the stock layout's
        head via `relative_url`.
    - The docs' own `_layouts/wikidown.html` + `assets/wikidown.css` carry
      a small local customization (About/GitHub/Open-editor top bar) —
      deliberate dogfood of the "edit the scaffolded theme freely" story.
    - Caveat: the *globally installed* MCP server predates
      `JekyllNavigation`, so wiki edits made through it don't refresh
      `_data/navigation.yml`; content-only edits don't need it, and
      `export-html` builds its nav live so wikidown.org can't go stale.
      Structural edits should re-run `wikidown pages` (or use current
      tooling) until the MCP tool is updated from NuGet.

23. **Editor: GitLab provider.** *(shipped)*
    - `WikiProvider.GitLab` + `GitLabBackend` in `Wikidown.Web`: REST API
      v4, project addressed as URL-encoded `namespace/project`, PAT via
      `PRIVATE-TOKEN` header (`api` scope). Reads: Repository Tree
      (header-paginated via `x-next-page`) + Files endpoints; writes: the
      Commits API with per-action `last_commit_id` as the conflict
      primitive (the new commit's own id becomes the next expected sha —
      no re-read needed). gitlab.com serves
      `Access-Control-Allow-Origin: *` on `/api/v4` (verified), so the
      no-backend model holds. Empty token sends no auth header, which
      makes public projects readable anonymously.
    - `WikiConnection` gains an optional `Host` for self-managed
      instances (blank = gitlab.com; old stored connections deserialize
      fine since the param has a default). Connect page gets a GitLab
      tab (group/namespace, project, branch, docs path, instance URL,
      PAT) with an inline tanuki SVG — MudBlazor ships no GitLab brand
      icon.
    - Verified live against public `gitlab-org/gitlab-docs` from the
      running editor: anonymous tree walk of `content/` (real folders
      rendered in `.order`-less nav), page click fetched and
      base64-decoded `archives/index.md` via the Files API. **Write path
      is untested** — needs a real GitLab account/PAT; the request shape
      follows the documented Commits API.
    - Real pre-existing bug found and fixed while verifying:
      `ConnectionStore.LoadAsync` set `_loaded = true` *before* awaiting
      the localStorage read, so when two components raced it on a fresh
      load (Browse + the drafts menu both initialize on `/browse`), the
      second caller got a permanently null connection — fresh-loading
      `/browse` showed "No wiki connected" despite a stored connection.
      Now caches the in-flight `Task` (`_load ??= LoadCoreAsync()`), so
      racing callers share one read. This affected GitHub/ADO too, in
      production, on any deep-link into `/browse`.

## Open questions / parking lot
- `[[_TOC_]]`, mermaid, `:::` callouts rendering in WASM preview.
- `/.attachments` upload from browser (REST base64 -> Contents API).
- Conflict resolution UX when remote HEAD moves during edit.
- ADO OAuth (vs PAT) — requires proxy; defer.
- `export-pdf`: multi-target `Wikidown.Core`/`Wikidown.Pdf` to `net472` if
  an "Export to PDF" VS command is ever wanted — `Wikidown.Vs` can't
  reference either project until then.
- `export-pdf`: a `/docs` page documenting the command (via `wikidown-editor`).
