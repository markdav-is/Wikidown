# Wikidown

A structured markdown wiki that lives in `/docs` of any code repo — plain
pages, folder-based hierarchy, and `.order` navigation files. The same format
Azure DevOps wikis use, readable by humans on disk, editable by AI agents
through MCP, and browsable in Visual Studio or the web editor.

**Site:** [wikidown.org](https://wikidown.org) · **Editor:** [wikidown.app](https://wikidown.app) · **Docs:** [`/docs`](./docs) (a Wikidown wiki, naturally)

## Quick start: add Wikidown to your repo

If you have .NET installed, install the CLI as a global tool:

```bash
dotnet tool install -g Wikidown.Cli
```

If not, these scripts install a self-contained binary — no .NET required:

```bash
curl -fsSL https://wikidown.org/install.sh | sh
```

```powershell
irm https://wikidown.org/install.ps1 | iex
```

Then set your repo up end to end:

```bash
wikidown init
```

Run from your repo root, `wikidown init`:

- seeds `docs/Home.md` if the wiki is empty;
- drops in **agent configs** so AI assistants edit the wiki through proper
  tools instead of raw file writes:
  - Claude Code: `.mcp.json`, a `wikidown` skill, and a `wikidown-editor`
    subagent, plus a `CLAUDE.md` section;
  - GitHub Copilot: `.vscode/mcp.json`, `.github/copilot-instructions.md`,
    an agent, a chat mode, and a skill.

Use `--agents claude|copilot|all|none` to pick, `--force` to overwrite, and
`--root <folder>` if your wiki isn't in `docs/`.

### Publish with GitHub Pages

```bash
wikidown pages
```

Scaffolds a Jekyll site into the wiki folder — `_config.yml` tuned for the
Wikidown format, a starter theme with a left-nav tree driven by your
`.order` files, and an `index.html` redirect to Home — then push and set
**Settings → Pages → Source: main, /docs**. The nav data
(`_data/navigation.yml`) is regenerated automatically by the CLI and MCP
server on every change.

For any other host — GitLab Pages, Azure Static Web Apps, Netlify, a file
share — or to preview locally, render the same theme in .NET instead; no
Jekyll or Ruby involved:

```bash
wikidown export-html --output public
```

Details for both in
[`/docs/Getting-Started/Publishing-to-GitHub-Pages`](./docs/Getting-Started/Publishing-to-GitHub-Pages.md).

### Give agents the MCP server

The configs above launch the Wikidown MCP server, so install it once globally:

```bash
dotnet tool install -g Wikidown.Mcp
```

The MCP server needs the .NET SDK/runtime (it has no self-contained binary
yet). On machines without .NET that used the install-script binary, agents
fall back to the equivalent `wikidown` CLI commands per the shared skill.

That's it — Claude Code / Copilot in that repo now have `wiki_list`,
`wiki_read`, `wiki_write`, `wiki_new`, `wiki_move`, `wiki_delete`,
`wiki_reorder`, `wiki_search`, and `wiki_walk` tools that keep `.order` files
consistent. Manual configs for other hosts are in [`samples/mcp/`](./samples/mcp/).

### See the wiki in Visual Studio

Install **Wikidown Wiki Project** from the
[Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MarkDavis.wikidown)
(VS 2022+), then right-click your solution → **Add → New Project → Wikidown
Wiki**. Your wiki appears in Solution Explorer the way Azure DevOps renders
it — page titles, `.order`-driven sorting, expandable subpage nodes — with
context-menu commands to add, reorder, and delete pages. The project never
participates in the build. Details: [`src/Wikidown.Vs/`](./src/Wikidown.Vs/)
and the [wiki page](./docs/Getting-Started/Visual-Studio-Extension.md).

### Edit in the browser

Open [wikidown.app](https://wikidown.app), connect your GitHub, GitLab, or
Azure DevOps repo, and edit pages directly — commits go through the provider's REST API, no
backend involved.

## Everyday CLI usage

```bash
wikidown list --path /                            # pages at the wiki root, in nav order
wikidown new --path /Guides/Getting-Started       # create a page (updates .order)
wikidown read --path /Guides/Getting-Started      # print a page
wikidown search --query "release notes"           # full-text search
wikidown move --from /Old-Name --to /New-Name     # rename/move, keeps .order consistent
wikidown reorder --folder / --names Home,Guides   # set explicit nav order
wikidown delete --path /Scratch --recursive       # remove a page (and its subpages)
```

All commands accept `--root <folder>` (default `docs`).

## What's in this repo

| Piece | What it is |
| --- | --- |
| [`src/Wikidown.Core`](./src/Wikidown.Core/) | Library: page model, `.order` handling, repo, search ([NuGet](https://www.nuget.org/packages/Wikidown.Core)) |
| [`src/Wikidown.Cli`](./src/Wikidown.Cli/) | `wikidown` dotnet tool ([NuGet](https://www.nuget.org/packages/Wikidown.Cli)) |
| [`src/Wikidown.Mcp`](./src/Wikidown.Mcp/) | `wikidown-mcp` stdio MCP server ([NuGet](https://www.nuget.org/packages/Wikidown.Mcp)) |
| [`src/Wikidown.Vs`](./src/Wikidown.Vs/) | Visual Studio extension ([Marketplace](https://marketplace.visualstudio.com/items?itemName=MarkDavis.wikidown)) |
| [`src/Wikidown.Web`](./src/Wikidown.Web/) | Blazor WASM editor PWA ([wikidown.app](https://wikidown.app)) |
| [`src/Wikidown.Html`](./src/Wikidown.Html/) | Starter theme + static HTML export (`wikidown export-html`, Markdig + Fluid) |
| [`agents/`](./agents/) | Drop-in agent configs (installed by `wikidown init`) |
| [`samples/mcp/`](./samples/mcp/) | Example MCP configs for various hosts |
| [`docs/`](./docs/) | This repo's own wiki — also the source of [wikidown.org](https://wikidown.org), published with `wikidown export-html` (dogfood twice over) |

## Format in one breath

- Page file on disk: `My-Page.md`; rendered title: `My Page`.
- Subpages: a folder alongside the page file with the same base name
  (`Architecture.md` + `Architecture/`).
- Ordering: `.order` file per folder lists page base-names, one per line;
  unlisted pages follow alphabetically.
- Links use title paths: `[Release Notes](/Getting-Started/Release-Notes)`.

Full spec: [`docs/Getting-Started/Format.md`](./docs/Getting-Started/Format.md).

## Status

Work in progress — see [`PLAN.md`](./PLAN.md) for chunk-by-chunk progress.
