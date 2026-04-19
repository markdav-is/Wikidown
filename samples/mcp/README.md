# Wikidown MCP — sample configurations

The Wikidown MCP server exposes the wiki as MCP tools so any MCP-compatible
agent (Claude Code, Claude Desktop, GitHub Copilot, Cursor, …) can read and
write `/docs` pages on behalf of the user.

## Install

```bash
dotnet tool install -g Wikidown.Mcp
# binary is then available as:
wikidown-mcp
```

Or run from source without installing:

```bash
dotnet run --project src/Wikidown.Mcp
```

## Configuration

The server picks the wiki root in this order:

1. `--root <path>` argument
2. `WIKIDOWN_ROOT` environment variable
3. `./docs` (current working directory)

## Sample configs

- `claude-code.mcp.json` — drop into your repo as `.mcp.json` for Claude Code.
- `claude-desktop.json` — merge into Claude Desktop's `claude_desktop_config.json`.

## Tools exposed

| Tool           | Purpose                                       |
| -------------- | --------------------------------------------- |
| `wiki_list`    | List children of a page or root               |
| `wiki_read`    | Read a page's markdown                        |
| `wiki_write`   | Create/overwrite a page (auto-updates .order) |
| `wiki_new`     | Create a new page (fails if exists)           |
| `wiki_move`    | Rename or move a page (and its subpages)      |
| `wiki_delete`  | Delete a page (optionally recursive)          |
| `wiki_reorder` | Rewrite a folder's `.order`                   |
| `wiki_search`  | Substring search across page bodies           |
| `wiki_walk`    | Depth-first list of every page                |
