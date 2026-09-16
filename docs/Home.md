# Home

**Wikidown** is a structured markdown wiki that lives in `/docs` of any git
repo — a C# CLI (the surface AI agents use too), a Blazor WASM browser editor,
and a marketing site, all built on the same page model. This wiki (the one
you're reading) is itself a Wikidown wiki, dogfooding the format and tools
it documents — including this site, which is the wiki published with
`wikidown export-html`.

## Where to go

- [Getting Started](Getting-Started.md) — what a Wikidown wiki looks like on
  disk, the on-disk format spec, and how a wiki stays current
- [CLI](CLI.md) — the `wikidown` dotnet tool: list / read / write / move /
  reorder / search / check-links / backfill-breadcrumbs
- [Editor](Editor.md) — the browser-based Blazor WASM editor that commits
  straight to your repo
- [Agents](Agents.md) — drop-in Claude Code and GitHub Copilot configs for
  maintaining a Wikidown wiki with an AI agent
- [Handy Prompts](Handy-Prompts.md) — example prompts to give your AI agent
  for everyday wiki upkeep
- [Release Notes](Release-Notes.md) — what changed in each release and
  where to get it
