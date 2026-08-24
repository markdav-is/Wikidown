# Wikidown — agent guidance

This repo builds Wikidown itself: a `/docs` wiki + CLI + MCP server + WASM
editor + product site. See [`PLAN.md`](./PLAN.md) for the build plan and
chunk-by-chunk progress.

## Build, test, package

```bash
dotnet build Wikidown.slnx -c Release
dotnet test Wikidown.slnx -c Release
```

Targets .NET 10 (`global.json` pinned to 10.0.302, rolls forward within the
feature band). Solution is `Wikidown.slnx` (new XML format); `Wikidown.slnf`
filters out the VSIX for dotnet-CLI builds. Package versions are centralized
in `Directory.Packages.props` (the VSIX project opts out). CI runs build + test +
pack on every push/PR.

## Project map

- `src/Wikidown.Core/` — page model, `.order`, repo, search.
- `src/Wikidown.Cli/` — `wikidown` command-line tool.
- `src/Wikidown.Mcp/` — `wikidown-mcp` stdio MCP server.
- `src/Wikidown.Html/` — Jekyll-compatible starter theme + `export-html` (Markdig + Fluid).
- `src/Wikidown.Web/` — Blazor WASM editor PWA *(coming in chunk 4)*.
- *(no separate marketing site)* — wikidown.org is `/docs` + its `index.html`, published by `pages.yml` via `wikidown export-html`.
- `tests/Wikidown.Core.Tests/` — xUnit tests.
- `agents/` — drop-in agent configs for downstream repos.
- `samples/mcp/` — sample MCP configs.
- `docs/` — this repo's own Wikidown wiki (dogfood).

## Conventions

- Keep changes scoped to a single chunk per commit. Update `PLAN.md` when a
  chunk ships.
- No comments unless the *why* is non-obvious.
- Don't add backwards-compat shims — this is pre-1.0.

## Documentation lives in `/docs` (Wikidown wiki)

- `/docs` is a Wikidown wiki — structured markdown with `.order` navigation
  files. Page links use title form: `/Getting-Started/Format`.
- A `wikidown-editor` subagent and a `wikidown` skill are configured in
  `.claude/`. Use them for ANY read/write of `/docs/*.md`.
- Never edit `/docs/*.md` directly with `Write`/`Edit`. Use the `wiki_*` MCP
  tools so `.order` files stay consistent.
- When you ship a feature that changes user-visible behavior, ask whether the
  wiki should be updated, and (if yes) delegate to `wikidown-editor`.
