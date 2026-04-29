# Wikidown

An Azure DevOps–Wiki-compatible wiki that lives in `/docs` of any code repo.

Wikidown bundles:

- **`Wikidown.Core`** — library for reading/writing ADO-wiki-formatted pages.
- **`wikidown` CLI** — dotnet tool for AI agents and humans to maintain pages.
- **`wikidown-mcp`** — stdio MCP server so Claude / Copilot / other agents can
  edit the wiki. See [`samples/mcp/`](./samples/mcp/) for drop-in configs.
- **Agent configs** — drop-in Claude Code and GitHub Copilot setups; see
  [`agents/`](./agents/).
- **Wikidown for Visual Studio** — a VS 2022+ extension that adds a
  `.wikidownproj` project type so your `docs/` folder appears in Solution
  Explorer without affecting the build. Install from the
  [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MarkDavis.Wikidown)
  or see [`src/Wikidown.Vs/`](./src/Wikidown.Vs/).
- **Wikidown Web** — a Blazor WASM PWA editor that commits directly to GitHub
  or Azure DevOps via REST (no backend). Hosted on GitHub Pages.
- **Wikidown Site** — the product/marketing page (also on GitHub Pages).

See [`PLAN.md`](./PLAN.md) for the current build plan and scope.

## Format (MVP)

- Page file on disk: `My-Page.md`; rendered title: `My Page`.
- Subpages: a folder alongside the page file with the same base name.
- Ordering: `.order` file per folder lists page base-names, one per line.
- Links use titles: `[Release Notes](/Getting-Started/Release-Notes)`.

## Status

Work in progress. See `PLAN.md` for chunk-by-chunk progress.
