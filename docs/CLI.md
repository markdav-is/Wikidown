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

See [Format](Getting-Started/Format.md) for the on-disk format the CLI
maintains, including the relative-link convention that `check-links`
enforces and the breadcrumb navigation it injects.
