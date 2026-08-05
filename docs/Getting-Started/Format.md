[Getting Started](../Getting-Started.md) / Format <!-- wikidown:breadcrumb -->

# Format Specification

Wikidown's on-disk format is deliberately minimal: markdown pages, folder-based hierarchy, and `.order` navigation files. The goal of this specification is to ensure that a repository's documentation is equally readable by humans browsing the file system, AI agents using the MCP server, and web-based renderers.

By enforcing these rules, Wikidown prevents the link rot and structural drift that typically plagues flat-file documentation.

## 1. Page Files and Titles

Every page in the wiki is a standard Markdown file (`.md`). The title of the page is derived directly from its filename by replacing hyphens with spaces.

*   **File on disk:** `Release-Notes.md`
*   **Rendered title:** `Release Notes`

The reverse is also true: when creating a page titled "Getting Started", the file must be named `Getting-Started.md`.

## 2. Subpages and Hierarchy

Wikidown supports infinite nesting of pages. To create subpages for a given parent page, you must create a folder with the exact same base name as the parent page's file, located in the same directory.

*   **Parent page:** `/docs/Architecture.md`
*   **Subpage folder:** `/docs/Architecture/`
*   **Child page:** `/docs/Architecture/Data-Model.md`

This sibling-folder structure ensures that deleting or moving a parent page can easily include all of its children.

Every subpage folder's parent page should exist and should **link every
child in its body** — see § Index pages below. `WikiRepository.Write` will
happily create `/Architecture/Data-Model` even if `/Architecture` doesn't
exist yet, which silently orphans the whole subtree: `wikidown list` /
`wiki_search` / `wikidown check-links`' normal link scan all walk the wiki
by descending from already-discovered pages, so a page whose parent was
never created is invisible to all of them. `check-links` catches this
specific case — see below.

## 3. Ordering

Alphabetical sorting is rarely the correct way to read documentation. Wikidown uses `.order` files to explicitly define the navigation hierarchy.

Every folder in the wiki (including the root `/docs` folder) should contain an `.order` file. This is a plain text file that lists the base names of the pages in that folder, one per line, from top to bottom.

**Example `.order` file:**
```text
Getting-Started
Architecture
API-Reference
```

If a page exists in the folder but is not listed in the `.order` file, it is typically appended to the end of the list alphabetically by the renderer.

`.order` controls navigation-widget ordering only — it has no effect on
GitHub's raw file view, so it's never a substitute for real body links. See
§ Index pages.

## 4. Internal Links

Internal links between wiki pages must be **relative file paths**, not
absolute title paths. GitHub resolves an absolute path like
`/Architecture/Data-Model` against the *repository* root, not the wiki root,
so a link written that way 404s when the page is viewed directly on
github.com. A relative path resolves correctly both on GitHub and in
Wikidown-aware renderers.

Write the link relative to the **linking page's own folder**, adjusted for
depth, and include the `.md` extension:

*   **Correct (sibling page):** `[Read the Data Model](Data-Model.md)`
*   **Correct (page in a different folder):** `[Read the Data Model](../Architecture/Data-Model.md)`
*   **Incorrect:** `[Read the Data Model](/Architecture/Data-Model)`

Images and other repo assets follow the same rule, e.g.
`![map](../.attachments/map.png)`.

Run `wikidown check-links` (see [CLI](../CLI.md)) to walk every page and
verify that relative links and image references resolve to real files; by
default it also flags any absolute title-path links left in page bodies,
and audits that every folder has a linked index page (§ Index pages).

This rule only applies to links **inside page bodies**. Addressing a page
through a tool or the CLI — e.g. `wikidown read --path /Getting-Started/Format`,
`wiki_read --path /Getting-Started/Format` — still uses the absolute title
path, since that's a tool argument rather than a rendered link.

### Fixing `check-links` failures

`check-links` prints one line per issue:

```text
page:line -> target  (reason)
```

What to do depends on the `(reason)`:

*   **`(absolute title-path link (404s on GitHub))`** — the link uses a
    `/Title/Path` form instead of a relative `.md` path. Rewrite it relative
    to the *linking page's own folder*, with the `.md` extension. For
    example, if `/Foo.md` (at the wiki root) contains
    `[Bar](/Bar)` and `Bar.md` is also at the wiki root, change it to
    `[Bar](Bar.md)`. If `/Sub/Foo.md` links to root-level `/Bar`, it becomes
    `[Bar](../Bar.md)`. Count the folder hops between the linking page and
    the target page to get the right number of `../`.

*   **`(broken link)`** — a relative link/image that doesn't resolve to a
    real file. This is either a typo in the relative path or a stale link
    left over from a page that moved or was deleted. Open the linking
    page's folder and confirm the target filename, hop count, and
    `.md`/`.attachments` spelling; fix the path to match the real file. If
    the target page genuinely no longer exists, either remove the link or
    point it at wherever that content now lives.

*   **A link that broke because a page moved** — `wikidown move` /
    `wiki_move` (see [CLI](../CLI.md) and
    [MCP Server](../MCP-Server.md)) automatically rewrite inbound links and
    the moved page's own relative links when they change a page's path or
    folder depth. So a `check-links` failure pointing at a page that was
    clearly moved usually means one of two things: the move happened before
    that link-rewriting behavior shipped, or the link was added by hand
    (e.g. typed into a body) after the move rather than being created
    through `move`/`wiki_move`. Either way, fix it the same way as a
    broken relative link above — retarget it at the page's current path and
    depth.

*   **`(no index page <Folder>.md)`** — a subpage folder exists but its
    sibling parent page is missing. Create it (`wikidown new --path /Folder`
    or `wiki_new`), then link every child from its body — see § Index pages.

*   **`(not linked from parent)`** — the parent page exists but its body
    never links this child, so a reader browsing rendered markdown (or the
    raw file on GitHub, where `.order` means nothing) has no way to reach
    it. Add a relative link to the child in the parent's body — see
    § Index pages.

After editing, re-run `wikidown check-links` to confirm the line is gone.

## 5. Index Pages

A folder's parent page is that folder's **index** — the entry point a
reader lands on before descending into its children. Two invariants keep
that entry point real rather than aspirational, both audited by
`wikidown check-links` (pass `--no-index-check` to skip this pass):

*   **The index page must exist.** `/Architecture/Data-Model` can be
    created without `/Architecture` ever existing — nothing in
    `WikiRepository.Write` requires the parent first. When that happens the
    whole subtree becomes invisible to every wiki-model-based tool
    (`wikidown list`, `wiki_search`, and `check-links`' own link scan),
    since they all discover pages by descending from an already-discovered
    parent. `check-links` finds these orphans by walking the raw
    filesystem instead, specifically because it can't rely on the page
    model to see them.
*   **The index page must link every child.** `.order` decides navigation
    order in Wikidown-aware UIs, but it's invisible on GitHub's raw file
    view — the only way a reader following links on github.com can reach a
    child page is a real link in the parent's body. `check-links` verifies
    every `*.md` file directly inside a folder is the resolved target of at
    least one link (relative or absolute) in that folder's parent page.

A page's own [breadcrumb](#6-breadcrumb-navigation) links upward to its
ancestors, but that's a different direction from this check: the
breadcrumb doesn't help a reader on the *parent* page discover its
children, and it doesn't verify the ancestor pages it links to actually
exist — a folder missing its index page still gets a breadcrumb pointing
at a `.md` file that isn't there, which is exactly the case index-page
auditing is meant to catch.

## 6. Breadcrumb Navigation

Every page gets a one-line breadcrumb trail auto-injected as its **first
line**, always leading back to `/Home` when the wiki has one:

```text
[Home](../Home.md) / [Encounters](../Encounters.md) / The Sky Hunters <!-- wikidown:breadcrumb -->
```

This exists because GitHub's own file-path breadcrumb (shown above the raw
file view) reflects the *repository* path — `docs / Encounters / The-Sky-Hunters.md`
— not the wiki's page hierarchy or titles. Wikidown's breadcrumb is built
purely from the page's own title chain instead: `Home` (if it exists) leads,
then each further ancestor is a link (relative, per the convention above),
and the current page's title is the final, unlinked segment — the same
shape GitHub uses, just scoped to the wiki rather than the whole repo.

Key behavior:

*   **Automatic.** `wikidown write` / `wiki_write` (and `new` / `wiki_new`,
    which write through the same path) inject or refresh the breadcrumb on
    every save — there's nothing to opt into or maintain by hand. Write
    whatever body content you want; the first line is managed for you.
*   **Idempotent.** The line carries an HTML comment marker
    (`<!-- wikidown:breadcrumb -->`) that's invisible when rendered. On
    every write, whatever was on line one gets discarded if it carries that
    marker, then a fresh one is computed from the page's *current* path —
    so re-saving a page never accumulates duplicate breadcrumbs, and a
    round-tripped `read` → edit → `write` doesn't need to preserve the line
    itself.
*   **Always leads back to `/Home`, when the wiki has one.** `wikidown init`
    seeds a `/Home` page as the wiki's entry point; every other page's
    breadcrumb — including top-level pages, which otherwise have no
    ancestors — leads with a link to it, so there's always a way back. A
    wiki with no `/Home` page (this repo's own `/docs` predates that
    convention) never gets a fabricated link to a page that doesn't exist:
    top-level pages simply have no breadcrumb, and nested pages' breadcrumbs
    start from their nearest real ancestor, same as before `/Home` was
    introduced. `/Home` itself never links to itself.
*   **Moves regenerate it, not just patch it.** `wikidown move` / `wiki_move`
    fully regenerates the breadcrumb for the moved page and every moved
    descendant, rather than trying to edit the existing links in place —
    a move can change *which* ancestors a page has, not just how many
    `../` hops reach them, so a patch-in-place approach can leave a
    structurally wrong (if still technically valid) breadcrumb behind.
*   **Checked like any other link.** Breadcrumb links are ordinary
    relative markdown links, so `check-links` validates them the same way
    it validates everything else in the body — but only that they *resolve*.
    Whether the ancestor page they point at *should* exist and is properly
    indexed is § Index Pages' job, not this one.
*   **Wikis that predate `/Home`, or predate breadcrumbs entirely,** won't
    have this line until each page is re-saved. Run
    `wikidown backfill-breadcrumbs` once to catch every page up in one pass
    — see [CLI](../CLI.md).

## 7. Markdown Dialect

Wikidown relies on standard CommonMark. There are no proprietary macros or shortcodes required to render the core text. An MVP renderer only needs a standard markdown parser plus the filename↔title mapping logic described above.
