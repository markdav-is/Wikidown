# MCP Server

`wikidown-mcp` is a stdio MCP server that exposes the Wikidown CLI surface to
Claude Code, Claude Desktop, VS Code (Copilot), or any other MCP host.

## Install

```sh
dotnet tool install -g Wikidown.Mcp
```

## Wiki root

Selected in this order:

1. `--root <path>` flag
2. `WIKIDOWN_ROOT` environment variable
3. Default `./docs`

## Tools

- `wiki_list` — list children of a page or the root
- `wiki_read` — read a page
- `wiki_write` — overwrite a page
- `wiki_new` — create a new page
- `wiki_move` — rename or move a page (with subpages)
- `wiki_delete` — delete a page (optionally recursive)
- `wiki_reorder` — rewrite a folder's `.order`
- `wiki_search` — search page bodies
- `wiki_walk` — depth-first walk of every page

## Wiring it in

Sample configs for Claude Code (`.mcp.json`) and Claude Desktop live in
[`samples/mcp/`](https://github.com/markdav-is/Wikidown/tree/main/samples/mcp)
in the repo. AI agents should prefer these tools over raw file edits so
`.order` files stay consistent.
