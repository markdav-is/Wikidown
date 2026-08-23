[Home](Home.md) / CLI <!-- wikidown:breadcrumb -->

# CLI

The `wikidown` dotnet tool reads, writes, moves, reorders, searches, and
link-checks pages in a Wikidown wiki. It keeps `.order` files, breadcrumb
navigation, and (on `move`) links in sync automatically.

## Install

```sh
dotnet tool install -g Wikidown.Cli
```

To update an existing install, see
[Updating](Getting-Started/Updating.md) — a plain push to `main` doesn't
always mean a new NuGet version is available.

### Quick install (no .NET required)

```sh
curl -fsSL https://wikidown.org/install.sh | sh
```

```powershell
irm https://wikidown.org/install.ps1 | iex
```

Both scripts auto-detect: if `dotnet` is on `PATH` they run
`dotnet tool install -g Wikidown.Cli` exactly as above; otherwise they
download a self-contained single-file binary for your platform
(win-x64/win-arm64/linux-x64/linux-arm64/osx-x64/osx-arm64) from this repo's
GitHub Releases (tagged `cli-v*`), extract it, and add it to `PATH`. Either
way nothing else needs installing — the binaries bundle their own .NET
runtime.

The binaries aren't code-signed or notarized yet, so the scripts work around
the resulting OS friction themselves: `install.ps1` runs `Unblock-File` on
the downloaded exe so Windows SmartScreen doesn't flag it, and `install.sh`
strips the `com.apple.quarantine` attribute on macOS so Gatekeeper doesn't
block it. This is a known gap, not a bug — signing/notarization is planned
but not done.

Either install path, the next step is the same: `wikidown init`.

## Wiki root

The CLI defaults to `./docs`. Override with `--root <path>`:

```sh
wikidown --root ./my-wiki list
```

## Commands

- `init [--root docs] [--agents claude|copilot|all|none] [--force]` — seed an
  empty wiki with a `/Home` page and install the AI agent configs (Claude Code
  + GitHub Copilot) into the folder containing the wiki root. See
  [Agents](Agents.md). Re-run with `--force` to pick up updated agent configs
  in an existing repo — see [Updating](Getting-Started/Updating.md).
- `list [--path /P]` — list children of a page (or root). `wikidown list`
- `read --path /P` — print page markdown to stdout. `wikidown read --path /Getting-Started`
- `write --path /P [--file F | --stdin]` — overwrite a page. Auto-injects or
  refreshes the page's breadcrumb line — see
  [Format § Breadcrumb Navigation](Getting-Started/Format.md).
- `new --path /P [--title T] [--file F | --stdin]` — create a new page.
- `move --from /A --to /B [--dry-run]` — rename or move a page (subpages
  travel with it). Rewrites inbound links from every other page that pointed
  at the old path, rewrites the moved page's own relative links and images
  if the move changed its folder depth (e.g. moving `/Encounters/Foo` to
  `/Adventures/Bar/Foo` turns `../.attachments/x.png` into
  `../../.attachments/x.png`), and regenerates the breadcrumb for the moved
  page and every moved descendant. Reports a count and a per-link list of
  what changed. `--dry-run` previews the rewrite without touching any files.
- `delete --path /P [--recursive]` — delete a page (and optionally its subpages).
- `reorder --folder /P --names a,b,c` — rewrite `.order` for a folder.
- `search --query <text>` — full-text search across page bodies.
- `check-links [--no-absolute-check] [--no-index-check]` — walk every page
  and validate that relative markdown links (`[x](../Foo/Bar.md)`) and image
  references (`![x](../.attachments/pic.png)`) resolve to real files
  relative to the linking page's folder. By default also:
  - flags absolute title-path links in page bodies (`[x](/Foo/Bar)`), since
    GitHub resolves those against the repo root and they 404 when the wiki
    is browsed on github.com (`--no-absolute-check` to skip);
  - audits that every subpage folder has an index page and that the index
    page links every child in its body, since `WikiRepository.Write` can
    create a grandchild page without its parent ever existing, silently
    orphaning the subtree from `wikidown list` / `wiki_search` / the rest
    of `check-links` itself (`--no-index-check` to skip). See
    [Format § Index Pages](Getting-Started/Format.md).

  Prints one line per issue as `page:line -> target  (reason)` (link
  issues) or `folder -> detail  (reason)` (index issues), and exits
  non-zero if any issues are found, so it's CI-friendly. See
  [Format § Fixing check-links failures](Getting-Started/Format.md) for how
  to resolve each kind of reported issue.
- `backfill-breadcrumbs [--dry-run]` — one-time catch-up for a wiki that
  predates breadcrumb navigation (chunk 11): re-saves every page that has
  an ancestor but is missing its breadcrumb line, so it picks one up. Only
  needed once per existing wiki — `write`, `new`, and `move` all maintain
  breadcrumbs automatically going forward, so a wiki that's always been
  edited through this CLI (or the MCP tools) never needs it. `--dry-run`
  lists which pages would change without writing anything. Prints a count.
- `export-pdf --output <path> [--from /Link/Path] [--title T] [--no-cover] [--no-toc] [--allow-html-skip]` —
  combine the whole wiki (or a subtree, with `--from`) into a single linked
  PDF:
  - an optional cover page (title defaults to the repo's folder name,
    override with `--title`, skip with `--no-cover`);
  - an optional in-document table of contents page with page numbers and
    clickable entries, indented to match the nav hierarchy (skip with
    `--no-toc`);
  - one section per wiki page, with each page's own leading `# Title`
    heading placed at its nav depth, so the PDF's bookmark/outline panel
    mirrors the wiki's folder structure instead of listing every page flat;
  - internal wiki links — relative `.md` links, legacy absolute
    title-paths, and same-page `#fragment` links to headings — become real
    in-PDF jumps, not dead hrefs;
  - full markdown fidelity: headings, bold/italic, inline code, nested
    bullet/numbered lists, fenced code blocks, block quotes, pipe tables,
    and images (embedded when the relative path resolves).

  A missing/broken image renders a visible placeholder and prints
  `warning: {page}: image not found: {target}` (mirroring `check-links`);
  the export still completes, but the command exits 1 if there were any
  warnings (0 otherwise). Raw HTML in a page's markdown (e.g. a `<div>`)
  is unsupported and fails the whole export by default — pass
  `--allow-html-skip` to instead render a
  `[unsupported HTML block omitted]` placeholder and continue.
  For example: `wikidown export-pdf --output wiki.pdf`.

  PDF font resolution is cross-platform: an embedded `EmbeddedFontResolver`
  ships DejaVu Sans and DejaVu Sans Mono TrueType fonts inside the
  Wikidown.Pdf assembly (Bitstream Vera License, redistribution permitted)
  instead of resolving fonts from the host OS. `export-pdf` works
  identically on Windows, Linux, and macOS.
- `pages [--title T] [--force]` — scaffold everything GitHub Pages' built-in
  Jekyll needs to publish the wiki as a static site, straight from the repo
  with no build pipeline: `_config.yml`, a starter theme (top bar, collapsible
  left nav that follows `.order`, content column), `assets/wikidown.css`, and
  a root `index.html` redirect. Nothing to install locally — GitHub runs
  Jekyll for you. The nav
  tree lives in `_data/navigation.yml`, which the CLI and MCP server
  regenerate on every write/move/delete/reorder once it exists. `--title`
  sets the site title (defaults to the repo folder name); `--force`
  overwrites theme files you've edited. Commit the result, point
  **Settings → Pages** at the `/docs` folder, and the wiki is live. See
  [Publishing to GitHub Pages](Getting-Started/Publishing-to-GitHub-Pages.md)
  for setup steps, what each scaffolded file does, and gotchas.

See [Format](Getting-Started/Format.md) for the on-disk format the CLI
maintains, including the relative-link convention that `check-links`
enforces and the breadcrumb navigation it injects.
