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
default it also flags any absolute title-path links left in page bodies.

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

After editing, re-run `wikidown check-links` to confirm the line is gone.

## 5. Markdown Dialect

Wikidown relies on standard CommonMark. There are no proprietary macros or shortcodes required to render the core text. An MVP renderer only needs a standard markdown parser plus the filename↔title mapping logic described above.
