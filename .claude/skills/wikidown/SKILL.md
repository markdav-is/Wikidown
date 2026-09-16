---
name: wikidown
description: Use whenever the user asks to read, write, search, rename, or reorganize pages in the project's Wikidown wiki at /docs. Triggers include phrases like "add a wiki page", "update the docs", "what does the wiki say about X", and any task that touches /docs/*.md.
---

# Wikidown skill

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — a
structured folder of markdown pages with `.order` navigation files. Edit it
through the `wikidown` CLI, never by writing files directly. If you cannot
run a shell at all, follow "No shell: suggest commands" below; if the CLI
cannot run either, follow "Last resort: no CLI".

## Format rules

- **Link path** — title form, hyphens for spaces: `/Getting-Started/Format`.
  The leading `/` is optional on the command line (`Getting-Started/Format`
  addresses the same page). Prefer the form without it when running under
  Git Bash on Windows, which rewrites `/Foo` arguments into `C:/Program
  Files/Git/Foo` before the CLI sees them (or set `MSYS_NO_PATHCONV=1`).
- **File on disk** — `Getting-Started/Format.md`. Subpages of `/Parent` live
  in a `Parent/` folder beside `Parent.md`.
- **Order** — each folder's `.order` file controls navigation order. Page
  writes update it automatically; rewrite explicitly with `wikidown reorder`.
- **Internal links (body)** — relative, with `.md`, adjusted for depth:
  `[Format](../Getting-Started/Format.md)`. Images: `![map](../.attachments/map.png)`.
  GitHub renders `/docs/*.md` directly and resolves an absolute path like
  `/Getting-Started/Format` against the repo root, not the wiki root — those
  links 404 on github.com. This is only about links written inside page
  bodies; command *addressing* (`--path Getting-Started/Format`) still uses
  the title-path form.
- **Page structure** — start with `# Title`, then a one-sentence summary,
  then content under H2/H3 headings.

## Command cheat sheet

Install once (either form; the second needs no .NET):

```sh
dotnet tool install -g Wikidown.Cli
curl -fsSL https://wikidown.org/install.sh | sh   # Windows: irm https://wikidown.org/install.ps1 | iex
```

Default root is `./docs`; add `--root <path>` to every command otherwise.

| Intent                  | Command                                                                  |
| ----------------------- | ------------------------------------------------------------------------ |
| What pages exist?       | `wikidown walk` (everything) or `wikidown list [--path P]`               |
| Read a page             | `wikidown read --path P`                                                 |
| Read one section        | `wikidown read --path P --section "Heading"`                             |
| Create a page           | `wikidown new --path P [--title T] [--file F \| --stdin]`                |
| Change part of a page   | `wikidown edit --path P --old <text> --new <text> [--all]`               |
|                         | multi-line: `--old-file F --new-file F`                                  |
| Rewrite one section     | `wikidown write-section --path P --section "Heading" (--file F \| --stdin) [--create]` |
| Add to the end          | `wikidown append --path P [--after "Heading"] (--file F \| --stdin)`     |
| Rewrite a whole page    | `wikidown write --path P (--file F \| --stdin)`                          |
| Find a topic            | `wikidown search --query <text>`                                         |
| Rename or move          | `wikidown move --from A --to B [--dry-run]`                              |
| Delete (with subpages)  | `wikidown delete --path P [--recursive]`                                 |
| Re-sort a folder        | `wikidown reorder --folder P --names a,b,c`                              |
| Audit links and `.order`| `wikidown check-links`                                                   |

Every command prints `--help`. Multi-line bodies go through `--stdin` (a
heredoc) or `--file`; write the text to a temp file when quoting gets
awkward.

## Exporting

- `wikidown export-pdf --output <path> [--from P] [--title T]` combines the
  whole wiki (or a subtree, with `--from`) into one linked PDF — cover page,
  table of contents, per-page bookmarks matching the nav hierarchy, and
  in-PDF jumps for internal links. Use it whenever asked for a PDF, a
  printable copy, or "the whole wiki as one document."
- `wikidown export-html --output <dir>` renders the wiki with the starter
  theme for any static host.

## Workflow

1. **Orient.** Run `wikidown walk` once at the start so you know what exists.
2. **Search first.** `wikidown search` before creating a page — you may just
   need to update an existing one.
3. **Read before overwriting.** `wikidown read` first; preserve voice and
   structure. On a long page, read just the section you need with
   `--section "Heading"` (case-insensitive, `#`s optional; a `##` section
   includes its `###` children). A miss lists the page's headings.
4. **Patch, don't rewrite.** Prefer `wikidown edit` for any change smaller
   than a full rewrite — one line, one bullet, one table row, a renamed
   heading. Pass `--old` exactly as it appears on the page (it may span
   lines via `--old-file`; line endings don't matter) and enough of it to
   be unique — the command refuses ambiguous matches and tells you how many
   times the text matched. To rewrite one whole section,
   `wikidown write-section` — pass the new body without the heading line;
   the heading stays and everything under it (including `###` children) is
   replaced. To add a bullet, paragraph, row, or new section at the end of
   a page or of one section, `wikidown append` (with `--after` for the
   latter) — no need to know the last line. Use `wikidown write` only for
   deliberate full rewrites.
5. **Cross-link.** When you create or rename a page, update inbound links on
   sibling pages (`wikidown edit` on each — no need to rewrite them).
6. **Order intentionally.** When adding a top-level concept,
   `wikidown reorder` so the new page lands where it makes sense in
   navigation.
7. **Moves rewrite links automatically.** `wikidown move` rewrites inbound
   links across the wiki and the moved page's own relative links/images for
   their new depth, and reports what it changed. Run `wikidown search`
   afterwards only if you suspect a link the tool couldn't resolve (e.g. one
   already broken).

## No shell: suggest commands

If your host cannot run commands (a chat-only assistant), do not edit
`/docs/*.md` directly. Work out the exact `wikidown` commands from the
cheat sheet above, show them to the user to run, and put any multi-line
body in a fenced block they can save to a file for `--file`. Say plainly
that the wiki has not been changed yet.

## Last resort: no CLI

If a shell is available but the CLI cannot run (no .NET and an execution
policy blocks the self-contained binary), wiki edits may be made with plain
file edits — but only by maintaining every invariant the CLI normally
handles. Work through this checklist for each change:

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

4. **Move/rename.** The CLI rewrites links automatically; by hand you must:
   move the `.md` file *and* its same-named subpage folder; search every
   page for inbound links to the old path and rewrite them; re-adjust the
   moved page's own relative links/images if its folder depth changed
   (count the `../` hops); regenerate the breadcrumb of the moved page and
   every descendant; update both `.order` files (step 2).

5. **Delete.** Remove the page, its subpage folder (if intended), its
   `.order` entry, and every inbound link to it from other pages.

6. **Published wikis.** If `_data/navigation.yml` exists, it is generated —
   the CLI regenerates it on every structural change, but raw edits leave
   it stale. Don't hand-edit it; tell the user to re-run `wikidown pages`
   (any machine that can execute it) so the published site's nav catches
   up.

7. **Verify later.** Recommend running `wikidown check-links` from an
   environment that can execute the CLI — it audits exactly the invariants
   above.

Treat this as a degraded mode: prefer the CLI whenever it works, and say in
your summary that edits were made manually so the user knows to verify.

## Don'ts

- Don't write `/docs/*.md` with file-edit tools while the CLI is available —
  it does `.order`, breadcrumb, and link bookkeeping for you. Manual edits
  are a last resort only (see above).
- Don't `wikidown write` a whole page to change one line — that's
  `wikidown edit`.
- Don't link to GitHub blob URLs from inside the wiki, and don't use
  absolute `/Title/Path` links in page bodies — use relative `.md` links.
- Don't rename without checking inbound references first.
- Don't write one-off chat notes into the wiki.
