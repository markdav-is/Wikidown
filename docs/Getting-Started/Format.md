# Format Specification

Wikidown's on-disk format is a strict subset of the Azure DevOps Wiki format. The goal of this specification is to ensure that a repository's documentation is equally readable by humans browsing the file system, AI agents using the MCP server, and web-based renderers.

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

Internal links between wiki pages must use the **title path**, not the relative file path. This abstracts away the `.md` extension and the hyphenated filenames, making the raw markdown much easier to read.

*   **Correct:** `[Read the Data Model](/Architecture/Data-Model)`
*   **Incorrect:** `[Read the Data Model](./Architecture/Data-Model.md)`

The leading slash `/` indicates the root of the wiki (typically the `/docs` folder). Relative title paths (e.g., `[Data Model](Data-Model)`) are also supported for linking to sibling pages.

## 5. Markdown Dialect

Wikidown relies on standard CommonMark. There are no proprietary macros or shortcodes required to render the core text. An MVP renderer only needs a standard markdown parser plus the filename↔title mapping logic described above.
