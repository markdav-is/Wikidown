# Home

**Wikidown** is a structured markdown wiki that lives in `/docs` of any git
repo — a C# CLI, an MCP server for AI agents, a Blazor WASM browser editor,
and a marketing site, all built on the same page model. This wiki (the one
you're reading) is itself a Wikidown wiki, dogfooding the format and tools
it documents.

## Where to go

- [Getting Started](Getting-Started.md) — what a Wikidown wiki looks like on
  disk, the on-disk format spec, and how a wiki stays current
- [CLI](CLI.md) — the `wikidown` dotnet tool: list / read / write / move /
  reorder / search / check-links / backfill-breadcrumbs
- [MCP Server](MCP-Server.md) — `wikidown-mcp`, the stdio MCP server AI
  agents use to read and edit a wiki
- [Editor](Editor.md) — the browser-based Blazor WASM editor that commits
  straight to your repo
- [Agents](Agents.md) — drop-in Claude Code and GitHub Copilot configs for
  maintaining a Wikidown wiki with an AI agent
- [Testing](Testing.md) — test plans and QA checklists for Wikidown's own
  public surfaces
- [Meta](Meta.md) — build-log notes on how Wikidown itself gets built
