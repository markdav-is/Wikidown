# CLI

The `wikidown` dotnet tool reads, writes, moves, reorders, searches, and
link-checks pages in a Wikidown wiki. It keeps `.order` files (and, on
`move`, links) in sync automatically.

## Install

```sh
dotnet tool install -g Wikidown.Cli
```

## Wiki root

The CLI defaults to `./docs`. Override with `--root <path>`:

```sh
wikidown --root ./my-wiki list
```

## Commands

- `init [--root docs] [--agents claude|copilot|all|none] [--force]` — seed an
  empty wiki with a `/Home` page and install the AI agent configs (Claude Code
  + GitHub Copilot) into the folder containing the wiki root. See
  [Agents](Agents.md).
- `list [--path /P]` — list children of a page (or root). `wikidown list`
- `read --path /P` — print page markdown to stdout. `wikidown read --path /Getting-Started`
- `write --path /P [--file F | --stdin]` — overwrite a page.
- `new --path /P [--title T] [--file F | --stdin]` — create a new page.
- `move --from /A --to /B [--dry-run]` — rename or move a page (subpages
  travel with it). Rewrites inbound links from every other page that pointed
  at the old path, and rewrites the moved page's own relative links and
  images if the move changed its folder depth (e.g. moving
  `/Encounters/Foo` to `/Adventures/Bar/Foo` turns `../.attachments/x.png`
  into `../../.attachments/x.png`). Reports a count and a per-link list of
  what changed. `--dry-run` previews the rewrite without touching any files.
- `delete --path /P [--recursive]` — delete a page (and optionally its subpages).
- `reorder --folder /P --names a,b,c` — rewrite `.order` for a folder.
- `search --query <text>` — full-text search across page bodies.
- `check-links [--no-absolute-check]` — walk every page and validate that
  relative markdown links (`[x](../Foo/Bar.md)`) and image references
  (`![x](../.attachments/pic.png)`) resolve to real files relative to the
  linking page's folder. By default also flags absolute title-path links in
  page bodies (`[x](/Foo/Bar)`), since GitHub resolves those against the repo
  root and they 404 when the wiki is browsed on github.com. Prints one line
  per issue as `page:line -> target  (reason)` and exits non-zero if any
  issues are found, so it's CI-friendly. Pass `--no-absolute-check` to skip
  the absolute-path check.

See [Format](Getting-Started/Format.md) for the on-disk format the CLI
maintains, including the relative-link convention that `check-links`
enforces.
