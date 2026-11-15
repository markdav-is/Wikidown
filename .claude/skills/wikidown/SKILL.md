---
name: wikidown
description: Use for anything involving the project's Wikidown wiki at /docs or the website published from it. Covers reading, writing, searching, moving, and reorganizing pages; kanban boards; PDF export; publishing the site (wikidown pages, export-html, GitHub Pages or GitLab Pages workflows); and site templates for docs or marketing sites (_layouts, _includes, _data files, _config.yml, landing pages, wiki_body, clickable image maps). Triggers include "add a wiki page", "update the docs", "what does the wiki say about X", "publish the wiki", "change the home page / landing page", "add this month's events", "update the pricing", and any task that touches /docs.
---

# Wikidown skill

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — a
structured folder of markdown pages with `.order` navigation files. The same
folder can be published as a website: a docs site with a sidebar, or a
marketing site with a landing page and sections built from data files.

- **Pages** (`*.md`, `.order`): edit through the `wikidown` CLI, never by
  writing files directly. If you cannot run a shell at all, follow "No
  shell: suggest commands" below; if the CLI cannot run either, follow
  "Last resort: no CLI".
- **Site files** (`_config.yml`, `_layouts/`, `_includes/`, `_data/`,
  `assets/`, `index.html`): ordinary files. Edit them with your normal file
  tools. See "Publishing the site" and "Site templates and data".

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
  then content under H2/H3 headings. The first `# Heading` is the page's
  title everywhere (nav, PDF, site).
- **No front matter, no Liquid in pages.** A `---` block at the top of a
  page, or `{{ }}` / `{% %}` in its body, shows up as literal text on the
  site. Page layout and dynamic content belong in templates (below).
- **Raw HTML and inline SVG** are allowed in pages. Their `href`s follow the
  same relative-`.md` rule and are rewritten, checked, and moved like
  markdown links. Keep each `href` on the same line as its opening `<a` or
  `<image` tag, and avoid blank lines inside an `<svg>` (GitHub ends the
  block there).

## Command cheat sheet

Install once — as a .NET tool on any platform, or with the standalone
installer (no .NET needed):

```
dotnet tool install -g Wikidown.Cli
irm https://wikidown.org/install.ps1 | iex          # Windows (PowerShell)
curl -fsSL https://wikidown.org/install.sh | sh     # macOS / Linux
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
| Fix outside images/files| `wikidown check-links --fix` — copies them into `.attachments/from-repo` and relinks |
| Start a kanban          | `wikidown kanban init [--path P]` (see "Kanban" below)                   |
| One PDF of the wiki     | `wikidown export-pdf --output wiki.pdf [--from P] [--title T]`           |
| Build the site locally  | `wikidown export-html --output _site --clean`                            |
| Set up site publishing  | `wikidown pages [--title T] [--theme]` (see "Publishing the site")       |

Every command prints `--help`. For multi-line bodies, write the text to a
temp file and pass `--file` — it works the same in every shell. `--stdin`
also works (pipe a PowerShell here-string, or use a bash heredoc).

## PDF export

`wikidown export-pdf --output <path> [--from P] [--title T]` combines the
whole wiki (or a subtree, with `--from`) into one linked PDF — cover page,
table of contents, per-page bookmarks matching the nav hierarchy, and
in-PDF jumps for internal links. Use it whenever asked for a PDF, a
printable copy, or "the whole wiki as one document." `--no-toc` and
`--no-cover` drop those pages.

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

## Kanban

A kanban is ordinary pages, nothing more:

- `/Kanban` is the dashboard, with one `##` section per sub-page, and each
  section heading links that sub-page: `## [To Do](Kanban/To-Do.md)`.
- `/Kanban/To-Do`, `/Kanban/Doing` and `/Kanban/Done` are the sub-pages.
- Every item is a sub-page of one of them, e.g. `/Kanban/To-Do/Buy-Paint`.

`wikidown kanban init` creates the dashboard and the three sub-pages
(`--path` puts it somewhere other than `/Kanban`). Everything after that
uses the normal commands. An item is listed twice: once under its section on
the dashboard, and once on its sub-page, just as every page links its
children. The examples below use `/Kanban`; for another `--path`, adjust the
link prefixes to match.

- **Add an item** — create it, then list it in both places:
  1. `wikidown new --path Kanban/To-Do/Buy-Paint --title "Buy paint"`
  2. `wikidown append --path Kanban --after "To Do"` with the line
     `- [Buy paint](Kanban/To-Do/Buy-Paint.md)`
  3. `wikidown append --path Kanban/To-Do` with the line
     `- [Buy paint](To-Do/Buy-Paint.md)`

  `--after` matches a linked heading by its text, so `"To Do"` finds
  `## [To Do](Kanban/To-Do.md)`.
- **Move an item** (e.g. To Do → Doing):
  1. `wikidown move --from Kanban/To-Do/Buy-Paint --to Kanban/Doing/Buy-Paint`.
     This rewrites both links, which now point at `Doing`, but leaves them
     where they were.
  2. On the dashboard, remove the line with `wikidown edit --delete` and
     `append --after "Doing"` the same line.
  3. Remove the line from `/Kanban/To-Do` and `append` the same line to
     `/Kanban/Doing`.
- **Update an item** — it is a normal page, so use `edit`, `write-section`
  or `append`.
- **Remove an item** — `wikidown delete`, then delete its two list lines.
- **Add a section** — `wikidown new --path Kanban/Waiting`, then
  `wikidown append --path Kanban` with `## [Waiting](Kanban/Waiting.md)`,
  and `wikidown reorder --folder Kanban` if it belongs between existing ones.
- **Keep the order in step** — list items on the dashboard in the same
  order as on their sub-page. Run `wikidown check-links` afterwards; it
  flags any item that isn't linked from its sub-page.

## Publishing the site

The wiki is published as a static site by `wikidown export-html`, run in a
pipeline. Nothing else builds it (GitHub's built-in Jekyll builder is not
supported).

- **Set up GitHub Pages:** `wikidown pages` writes
  `.github/workflows/pages.yml` at the root of the git checkout and a
  `_config.yml` in the wiki. The user then sets **Settings → Pages →
  Source: "GitHub Actions"** and pushes. Existing files are skipped unless
  `--force`. `--theme` also copies the starter theme into the wiki for
  editing; without it the built-in theme is used.
- **Other hosts** (GitLab Pages, Azure Static Web Apps, any CI): run
  `wikidown export-html --output <dir> --clean` and deploy the folder.
  `--base-url /prefix` when the site isn't at the domain root.
- **Preview / verify:** `wikidown export-html --output _site --clean`, then
  any static server (`dotnet serve -d _site`, `npx serve _site`). Read the
  command's output: warnings name missing pages and leftover files, and a
  template error stops the export and names the file. Don't commit `_site`.
- **Leave pages out of the site** (internal notes, plans) without removing
  them from the wiki:

  ```yaml
  wikidown:
    exclude_from_site:
      - /Meta
  ```

- **Only files inside the wiki folder publish.** Run `wikidown check-links`
  before publishing; `check-links --fix` copies images linked from outside
  the wiki into `.attachments/from-repo/`.
- **Upgrading a wiki that used "Deploy from a branch"** (Jekyll): run
  `wikidown pages`, switch the Pages source to GitHub Actions, delete
  `_data/navigation.yml` and Gemfiles, and remove `{% raw %}` wrappers and
  front matter from pages. Full guide:
  https://wikidown.org/Getting-Started/Migrating-from-Jekyll-Publishing.html

## Site templates and data

Templates are [Liquid](https://shopify.github.io/liquid/), with site
conventions borrowed from Jekyll. Where things go:

| What | Where |
|---|---|
| Text people edit (intro, about, product and venue descriptions) | Wiki pages, via the CLI |
| Lists and settings that change (events, prices, nav links, contact info) | `_data/*.json` or `*.yml` |
| Page structure and look | `_layouts/<name>.html`, `_includes/*.html`, `assets/` |
| Which page uses which layout; the site root | `_config.yml` → `wikidown:` |
| Site title, description, `url`, `baseurl`, `favicon`, `image` | `_config.yml` top level |

Keep copy in pages and lists in data, so routine updates never touch a
template. **Don't edit templates unless the user asks for a design or
structure change**; a content request ("add a venue", "change the price")
is a page or data edit.

`_config.yml`:

```yaml
wikidown:
  root_page: /Home        # serve this page as the site's index.html (URL "/")
  layouts:
    /Home: landing        # _layouts/landing.html
    /Venues/*: venue      # every page under /Venues
```

Every other page uses `_layouts/wikidown.html` (built in unless copied
with `wikidown pages --theme`). A layout can sit inside another by naming
it in front matter (`layout: base`). Without `root_page`, the wiki root's
`index.html` is the site root (a redirect to Home by default).

What templates can use:

- `{{ content }}`: the current page's rendered markdown (in a layout).
- `page.title`, `page.url`, `page.path` (`Venues/Pinebox.md`),
  `page.dir`, `page.layout`.
- `site.title` and every `_config.yml` key; `site.data.<file>` for each
  `_data` file (subfolders nest: `_data/hops/2026-11.json` is
  `site.data.hops["2026-11"]`); `site.data.navigation` (the `.order` tree);
  `site.pages` (every published page, in `.order` order).
- `{{ "Venues/Pinebox.md" | wiki_body: "strip_title" }}` shows another
  page's body (links fixed to work anywhere); `wiki_title` gives its
  title. Look a page up with
  `site.pages | where: "path", "Venues/Pinebox.md" | first`.
- Filters: standard Liquid plus `relative_url` (use it for every site
  path: `{{ '/assets/site.css' | relative_url }}`), `absolute_url`,
  `markdownify`, `jsonify`, `slugify`, `normalize_whitespace`,
  `xml_escape`, `cgi_escape`, `number_of_words`, and a numeric `sort`.
  `date` takes strftime formats (`"%A, %B %-d, %Y"`).
- `{% include nav.html title=x %}`, read back as `include.title`.
- Not available: `where_exp`, `group_by`, `{% link %}`, `{% post_url %}`,
  `{% highlight %}`, `site.posts`, collections.

Common tasks:

- **Monthly listing** (events, venues): one `_data/<thing>/<month>.json`
  per month listing page paths plus monthly details; a `current` key in
  `_data/site.json` picks the month; an include loops over it and shows
  each page's blurb with `wiki_body`. New month = new data file + change
  `current`.
- **Pricing / plans:** a list in `_data`, cards in an include,
  `| map: "price" | sort | first` for "from $N". Keep prices unquoted so
  they sort as numbers.
- **Shared nav and footer:** links and contact details in `_data/site.json`,
  rendered by `_includes/nav.html` and `footer.html` in every layout.
- **Extra widget on one page:** in the layout,
  `{% if page.path == "Membership.md" %}{% include membership.html %}{% endif %}`.
  After `wikidown move`, update such paths by hand; `move` rewrites links
  in pages, not in templates or data files.
- **Landing page:** `root_page` + a `landing` layout that pulls its text
  from the Home page with `wiki_body: "strip_title"`.

Check a template change with `export-html` (above), then look at the
generated HTML. Full reference with worked examples:
https://wikidown.org/Site-Templates.html ·
https://wikidown.org/Site-Templates/Template-Reference.html

## Clickable image maps

To make areas of an image link to pages, put an inline `<svg>` in the page:
an `<image>` of the picture plus `<a href="Page.md">` around shapes. No
JavaScript or plugins. Paths are relative to the page, like any link.

```html
<svg class="map" viewBox="0 0 1000 600" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="Map">
  <image href="../.attachments/region-map.png" width="1000" height="600"/>
  <a href="Places/Harbor.md" aria-label="Harbor"><circle cx="720" cy="430" r="40"/><title>Harbor</title></a>
</svg>
```

Style the shapes in the site's stylesheet (`assets/`). Follow the raw
HTML rules in "Format rules". Guide:
https://wikidown.org/Getting-Started/Clickable-Maps.html

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
   (no `.md`), one per line. Line endings don't matter: CRLF and LF both
   read fine and the CLI keeps whatever a file already uses — never warn
   about or "fix" them. On create: append the new page's
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

6. **Verify later.** Recommend running `wikidown check-links` from an
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
- Don't add front matter or Liquid to pages; use `_config.yml`, layouts,
  and `_data` instead.
- Don't hard-code site paths in templates; pass them through `relative_url`.
- Don't put content that changes monthly into a layout; put it in `_data`
  or a page.
- Don't create `_data/navigation.yml` or Jekyll settings (`plugins:`,
  `kramdown:`); the nav is built from `.order` at export time.
