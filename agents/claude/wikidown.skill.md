---
name: wikidown
description: Use whenever the user asks to read, write, search, rename, or reorganize pages in the project's Wikidown wiki at /docs. Triggers include phrases like "add a wiki page", "update the docs", "what does the wiki say about X", and any task that touches /docs/*.md.
---

# Wikidown skill

This repo has a Wikidown wiki at `/docs` — Azure-DevOps-Wiki-compatible
markdown. Edit it through the `wiki_*` MCP tools, never by writing files
directly. (If those tools aren't available, fall back to the `wikidown` CLI.)

## Quick reference

- **Link path** = title form, hyphens for spaces: `/Getting-Started/Format`.
- **File path** = `Getting-Started/Format.md`. Subpages of `/Getting-Started`
  live in `Getting-Started/`.
- **Order** comes from each folder's `.order` file. Auto-updated on write;
  rewrite manually with `wiki_reorder`.
- Links between pages: `[Format](/Getting-Started/Format)`.

## Tool cheat sheet

| Intent                       | Tool                                        |
| ---------------------------- | ------------------------------------------- |
| What pages exist?            | `wiki_walk` (everything) or `wiki_list`     |
| Read a page                  | `wiki_read path=/Some/Page`                 |
| New page                     | `wiki_new path=/Some/Page` (+ optional body) |
| Update a page                | `wiki_write path=/Some/Page markdown=...`   |
| Find a topic                 | `wiki_search query=...`                     |
| Rename or move               | `wiki_move from=/Old to=/New`               |
| Delete (with subpages)       | `wiki_delete path=/X recursive=true`        |
| Re-sort a folder             | `wiki_reorder folder=/X names=[a,b,c]`      |

## Workflow

1. Run `wiki_walk` once to orient.
2. `wiki_search` before creating — avoid duplicates.
3. Read existing content before overwriting.
4. After `wiki_move`, search for the old path and fix inbound links.
5. Pages start with `# Title` then a one-line summary.

## Don'ts

- Don't write `/docs/*.md` files with `Write` / `Edit` — bypasses `.order`
  bookkeeping and breaks the agent's mental model of the wiki.
- Don't link to GitHub blob URLs from inside the wiki — use `/Title/Path` form.
- Don't rename without checking inbound references.
