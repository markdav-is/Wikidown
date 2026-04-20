# CLI

The `wikidown` dotnet tool reads, writes, moves, reorders, and searches pages
in a Wikidown wiki. It keeps `.order` files in sync automatically.

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

- `list [--path /P]` — list children of a page (or root). `wikidown list`
- `read --path /P` — print page markdown to stdout. `wikidown read --path /Getting-Started`
- `write --path /P [--file F | --stdin]` — overwrite a page.
- `new --path /P [--title T] [--file F | --stdin]` — create a new page.
- `move --from /A --to /B` — rename a page (subpages travel with it).
- `delete --path /P [--recursive]` — delete a page (and optionally its subpages).
- `reorder --folder /P --names a,b,c` — rewrite `.order` for a folder.
- `search --query <text>` — full-text search across page bodies.

See [Format](/Getting-Started/Format) for the on-disk format the CLI maintains.
