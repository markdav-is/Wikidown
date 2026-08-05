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

## Wiki root

Selected in this order:

1. `--root <path>` flag
2. `WIKIDOWN_ROOT` environment variable
3. Default `./docs`

## Tools

- `wiki_list` — list children of a page or the root
- `wiki_read` — read a page
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

## Wiring it in

Sample configs for Claude Code (`.mcp.json`) and Claude Desktop live in
[`samples/mcp/`](https://github.com/markdav-is/Wikidown/tree/main/samples/mcp)
in the repo. AI agents should prefer these tools over raw file edits so
`.order` files and internal links stay consistent.
