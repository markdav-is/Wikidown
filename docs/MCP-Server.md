[Home](Home.md) / MCP Server <!-- wikidown:breadcrumb -->

# MCP Server

`wikidown-mcp` is a stdio MCP server that exposes the Wikidown CLI surface to
Claude Code, Claude Desktop, VS Code (Copilot), or any other MCP host.

## Install

```sh
dotnet tool install -g Wikidown.Mcp
```

To update an existing install, see
[Updating](Getting-Started/Updating.md) — a plain push to `main` doesn't
always mean a new NuGet version is available.

### No .NET?

Unlike the CLI, the MCP server currently ships **only** as a NuGet global
tool, so it needs the .NET SDK or runtime on `PATH` — there is no
self-contained `wikidown-mcp` binary yet.

That's less limiting than it sounds: the CLI *does* ship self-contained,
and the shared agent skill tells agents to fall back to `wikidown` CLI
commands whenever the `wiki_*` MCP tools aren't available. So on a machine
without .NET:

```sh
curl -fsSL https://wikidown.org/install.sh | sh
```

```powershell
irm https://wikidown.org/install.ps1 | iex
```

installs a self-contained `wikidown` binary (see
[CLI](CLI.md) § Quick install), and agents keep maintaining the wiki
through the CLI fallback — only the MCP wiring (`.mcp.json`,
`.vscode/mcp.json`) stays dormant until .NET is installed.

## Wiki root

Selected in this order:

1. `--root <path>` flag
2. `WIKIDOWN_ROOT` environment variable
3. Default `./docs`

## Tools

- `wiki_list` — list children of a page or the root
- `wiki_read` — read a page, or just one section of it. Pass `section`
  (a heading's text, matched case-insensitively and ignoring leading `#`s
  and surrounding whitespace, e.g. `"Open concerns"` or
  `"## Open concerns"`) to get that heading line plus everything below it
  up to the next heading of the same or higher level — so a `##` section
  includes its `###` children. Only ATX (`#`) headings count, and
  headings inside fenced code blocks are ignored. A miss fails with the
  page's headings listed (`no section 'Progress' in /Path; headings:
  History · Voice · Notes`) so the next call can hit; if several headings
  match, the first is returned with a trailing
  `note: 2 headings matched; returned the first`.
- `wiki_edit` — replace an exact substring of a page in place, leaving the
  rest of the page untouched. Same contract as Claude Code's built-in
  `Edit` tool: `old` must match the page's raw markdown exactly (line
  endings are normalized to the file's, so CRLF vs LF never matters) and,
  unless `replaceAll=true`, exactly once — a miss or an ambiguous match
  fails with a message saying how many times the text matched. Never
  touches the breadcrumb line or `.order`, and never creates a page (a
  missing page is an error, not a create). Returns the changed line
  numbers with two lines of context so the agent can confirm the edit
  without re-reading the page.
- `wiki_write` — overwrite a page. Auto-injects or refreshes the page's
  breadcrumb navigation line — see
  [Format § Breadcrumb Navigation](Getting-Started/Format.md).
- `wiki_new` — create a new page
- `wiki_move` — rename or move a page (with subpages). Rewrites inbound links
  from every other page that pointed at the old path, rewrites the moved
  page's own relative links and images if the move changed its folder depth,
  regenerates the breadcrumb for the moved page and every moved descendant,
  and reports a count and a per-link list of what changed.
- `wiki_delete` — delete a page (optionally recursive)
- `wiki_reorder` — rewrite a folder's `.order`
- `wiki_search` — search page bodies
- `wiki_walk` — depth-first walk of every page

There's no `wiki_check_links` tool yet — run `wikidown check-links` from the
CLI (see [CLI](CLI.md)) to validate that relative links/images resolve and
that page bodies don't contain absolute title-path links, which 404 when a
page is viewed directly on github.com.

### Choosing how to change a page

Whole-page reads and rewrites are the dominant cost in a long agent
session on a large wiki: a one-sentence change to a 10 KB page costs the
full 10 KB round-trip twice and risks drifting paragraphs the agent never
meant to touch. Pick the smallest tool that fits:

- `wiki_read` with `section` when a long page has the one `##` you need
  to decide or to target an edit. Read the whole page only when you
  really need all of it.
- `wiki_edit` for anything smaller than a full rewrite — one line, one
  bullet, one table row, a renamed heading. It costs only the changed text
  and cannot alter anything outside the match.
- `wiki_write` only for new pages (or `wiki_new`) and deliberate full
  rewrites.

## Wiring it in

Sample configs for Claude Code (`.mcp.json`) and Claude Desktop live in
[`samples/mcp/`](https://github.com/markdav-is/Wikidown/tree/main/samples/mcp)
in the repo. AI agents should prefer these tools over raw file edits so
`.order` files and internal links stay consistent.
