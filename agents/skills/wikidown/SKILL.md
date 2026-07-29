---
name: wikidown
description: Use whenever the user asks to read, write, search, rename, or reorganize pages in the project's Wikidown wiki at /docs. Triggers include phrases like "add a wiki page", "update the docs", "what does the wiki say about X", and any task that touches /docs/*.md.
---

# Wikidown skill

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — a
structured folder of markdown pages with `.order` navigation files. Edit it
through the wiki MCP tools, never by writing files directly. If MCP tools are unavailable,
fall back to the `wikidown` CLI.

Tool names below use the bare form (`wiki_write`). Your host may prefix them
with the server name — e.g. `wikidown_wiki_write` in VS Code / GitHub Copilot.

## Format rules

- **Link path** — title form, hyphens for spaces: `/Getting-Started/Format`.
- **File on disk** — `Getting-Started/Format.md`. Subpages of `/Parent` live
  in a `Parent/` folder beside `Parent.md`.
- **Order** — each folder's `.order` file controls navigation order. Page
  writes update it automatically; rewrite explicitly with `wiki_reorder`.
- **Internal links** — use the title path: `[Format](/Getting-Started/Format)`.
- **Page structure** — start with `# Title`, then a one-sentence summary,
  then content under H2/H3 headings.

## Tool cheat sheet

| Intent                  | Tool                                         |
| ----------------------- | -------------------------------------------- |
| What pages exist?       | `wiki_walk` (everything) or `wiki_list`      |
| Read a page             | `wiki_read` path=/Some/Page                  |
| Create a page           | `wiki_new` path=/Some/Page (+ optional body) |
| Update a page           | `wiki_write` path=/Some/Page markdown=…      |
| Find a topic            | `wiki_search` query=…                        |
| Rename or move          | `wiki_move` from=/Old to=/New                |
| Delete (with subpages)  | `wiki_delete` path=/X recursive=true         |
| Re-sort a folder        | `wiki_reorder` folder=/X names=[a,b,c]       |

## CLI fallback

```sh
# Install
dotnet tool install -g Wikidown.Cli

# Commands (default root is ./docs; override with --root <path>)
wikidown list [--path /P]
wikidown read --path /P
wikidown write --path /P [--file F | --stdin]
wikidown new --path /P [--title T] [--file F | --stdin]
wikidown move --from /A --to /B
wikidown delete --path /P [--recursive]
wikidown reorder --folder /P --names a,b,c
wikidown search --query <text>
```

## Workflow

1. **Orient.** Call `wiki_walk` once at the start so you know what exists.
2. **Search first.** `wiki_search` before creating a page — you may just need
   to update an existing one.
3. **Read before overwriting.** `wiki_read` first; preserve voice and
   structure.
4. **Cross-link.** When you create or rename a page, update inbound links on
   sibling pages.
5. **Order intentionally.** When adding a top-level concept, `wiki_reorder`
   so the new page lands where it makes sense in navigation.
6. **Moves don't rewrite links.** After `wiki_move`, `wiki_search` for the
   old path and fix references.

## Don'ts

- Don't write `/docs/*.md` with file-edit tools — bypasses `.order`
  bookkeeping and breaks navigation.
- Don't link to GitHub blob URLs from inside the wiki — use `/Title/Path`.
- Don't rename without checking inbound references first.
- Don't write one-off chat notes into the wiki.
