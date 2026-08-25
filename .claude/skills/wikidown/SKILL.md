---
name: wikidown
description: Use whenever the user asks to read, write, search, rename, or reorganize pages in the project's Wikidown wiki at /docs. Triggers include phrases like "add a wiki page", "update the docs", "what does the wiki say about X", and any task that touches /docs/*.md.
---

# Wikidown skill

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — a
structured folder of markdown pages with `.order` navigation files. Edit it
through the wiki MCP tools, never by writing files directly. If MCP tools are unavailable,
fall back to the `wikidown` CLI; if that can't run either, follow
"Last resort: no tools, no CLI" below.

Tool names below use the bare form (`wiki_write`). Your host may prefix them
with the server name — e.g. `wikidown_wiki_write` in VS Code / GitHub Copilot.

## Format rules

- **Link path** — title form, hyphens for spaces: `/Getting-Started/Format`.
- **File on disk** — `Getting-Started/Format.md`. Subpages of `/Parent` live
  in a `Parent/` folder beside `Parent.md`.
- **Order** — each folder's `.order` file controls navigation order. Page
  writes update it automatically; rewrite explicitly with `wiki_reorder`.
- **Internal links (body)** — relative, with `.md`, adjusted for depth:
  `[Format](../Getting-Started/Format.md)`. Images: `![map](../.attachments/map.png)`.
  GitHub renders `/docs/*.md` directly and resolves an absolute path like
  `/Getting-Started/Format` against the repo root, not the wiki root — those
  links 404 on github.com. This is only about links written inside page
  bodies; tool *addressing* (`wiki_read path=/Getting-Started/Format`) still
  uses the title-path form.
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
# Install (either form; the second needs no .NET)
dotnet tool install -g Wikidown.Cli
curl -fsSL https://wikidown.org/install.sh | sh   # Windows: irm https://wikidown.org/install.ps1 | iex

# Commands (default root is ./docs; override with --root <path>)
wikidown list [--path /P]
wikidown read --path /P
wikidown write --path /P [--file F | --stdin]
wikidown new --path /P [--title T] [--file F | --stdin]
wikidown move --from /A --to /B [--dry-run]
wikidown delete --path /P [--recursive]
wikidown reorder --folder /P --names a,b,c
wikidown search --query <text>
```

## Exporting

- `wikidown export-pdf --output <path> [--from /P] [--title T]` combines the
  whole wiki (or a subtree, with `--from`) into one linked PDF — cover page,
  table of contents, per-page bookmarks matching the nav hierarchy, and
  in-PDF jumps for internal links. CLI-only, no MCP equivalent — use it
  whenever asked for a PDF, a printable copy, or "the whole wiki as one
  document."

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
6. **Moves rewrite links automatically.** `wiki_move` rewrites inbound links
   across the wiki and the moved page's own relative links/images for their
   new depth, and reports what it changed. Run `wiki_search` afterwards only
   if you suspect a link the tool couldn't resolve (e.g. one already broken).

## Last resort: no tools, no CLI

If the `wiki_*` MCP tools are unavailable **and** the CLI cannot run (no
.NET and an execution policy blocks the self-contained binary), wiki edits
may be made with plain file edits — but only by maintaining every invariant
the tools normally handle. Work through this checklist for each change:

1. **Breadcrumb line.** Every page's *first line* is a breadcrumb, marked
   with an HTML comment:

   ```markdown
   [Home](../Home.md) / [Parent Title](../Parent.md) / Page Title <!-- wikidown:breadcrumb -->
   ```

   Lead with a link to `/Home` if the wiki has one, then each ancestor as a
   relative `.md` link (depth-adjusted from the page's own folder), then the
   page's own title unlinked, then the marker comment. A page at the wiki
   root with no `/Home` has no breadcrumb line. When you create or move a
   page, write/regenerate this line; never duplicate it.

2. **`.order` bookkeeping.** Each folder's `.order` lists page base names
   (no `.md`), one per line, LF endings. On create: append the new page's
   base name to its folder's `.order` (create the file if missing). On
   delete: remove the entry. On move: remove from the old folder's file,
   add to the new one's. Unlisted pages sort last alphabetically, so a
   missing entry is drift, not breakage — fix it anyway.

3. **Create.** Parent page must exist: a page at `/A/B` needs `A.md` beside
   the `A/` folder, and `A.md`'s body must link the new child
   (`[B](A/B.md)`) — otherwise the subtree is invisible to readers and
   flagged by `check-links`.

4. **Move/rename.** The tools rewrite links automatically; by hand you must:
   move the `.md` file *and* its same-named subpage folder; search every
   page for inbound links to the old path and rewrite them; re-adjust the
   moved page's own relative links/images if its folder depth changed
   (count the `../` hops); regenerate the breadcrumb of the moved page and
   every descendant; update both `.order` files (step 2).

5. **Delete.** Remove the page, its subpage folder (if intended), its
   `.order` entry, and every inbound link to it from other pages.

6. **Published wikis.** If `_data/navigation.yml` exists, it is generated —
   the CLI/MCP server regenerate it on every structural change, but raw
   edits leave it stale. Don't hand-edit it; tell the user to re-run
   `wikidown pages` (any machine that can execute it) so the published
   site's nav catches up.

7. **Verify later.** Recommend running `wikidown check-links` from an
   environment that can execute the CLI — it audits exactly the invariants
   above.

Treat this as a degraded mode: prefer the tools whenever they work, and say
in your summary that edits were made manually so the user knows to verify.

## Don'ts

- Don't write `/docs/*.md` with file-edit tools while the `wiki_*` tools or
  the CLI are available — they do `.order`, breadcrumb, and link
  bookkeeping for you. Manual edits are a last resort only (see above).
- Don't link to GitHub blob URLs from inside the wiki, and don't use
  absolute `/Title/Path` links in page bodies — use relative `.md` links.
- Don't rename without checking inbound references first.
- Don't write one-off chat notes into the wiki.
