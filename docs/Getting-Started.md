[Home](Home.md) / Getting Started <!-- wikidown:breadcrumb -->

# Getting Started

Welcome to Wikidown. This folder is itself a Wikidown-format wiki.

## Format quick reference

- Page file on disk: `Getting-Started.md`; rendered title: `Getting Started`,
  from the page's first `# Heading` (the file name is the fallback).
- Subpages go in a folder with the same base name next to the page file.
- Ordering lives in `.order` — one page base-name per line.
- Body links are relative file paths (with the `.md` extension), not
  "/"-rooted title paths — see [Format](Getting-Started/Format.md) for the
  full rule.

## Next

- [Format](Getting-Started/Format.md) — full on-disk format reference
- [Muse Integration](Getting-Started/Muse-Integration.md) — connect two Muse accounts to one shared wiki so they remember the same things
- [Claude Code Integration](Getting-Started/Claude-Code-Integration.md) — the skill, subagent and `CLAUDE.md` section `wikidown init` installs, and how to use them
- [GitHub Copilot Integration](Getting-Started/GitHub-Copilot-Integration.md) — repo instructions, custom agent and chat mode, plus the coding agent on github.com
- [Visual Studio Extension](Getting-Started/Visual-Studio-Extension.md) — add a Wikidown project in VS 2022+
- [Updating](Getting-Started/Updating.md) — how downstream repos pick up new CLI releases, agent config changes, and VS extension updates
- [Publishing to GitHub Pages](Getting-Started/Publishing-to-GitHub-Pages.md) — publish the wiki as a static site: `wikidown pages` adds a GitHub Actions workflow that runs `wikidown export-html`
- [Migrating from Jekyll Publishing](Getting-Started/Migrating-from-Jekyll-Publishing.md) — moving a wiki from GitHub's built-in Jekyll ("Deploy from a branch") to the pipeline, required as of 0.14.0
- [Publishing to GitLab Pages](Getting-Started/Publishing-to-GitLab-Pages.md) — one CI job running `wikidown export-html`
- [Site Templates](Site-Templates.md) — publish the wiki as a docs site or a marketing site: custom layouts, landing pages, and data-driven templates
- [Kanban](Getting-Started/Kanban.md) — keep a simple To Do / Doing / Done board in your wiki with `wikidown kanban init`
- [Clickable Maps](Getting-Started/Clickable-Maps.md) — turn an image into a map whose areas link to pages, with inline SVG and no JavaScript
- [CLI](CLI.md) — `wikidown` dotnet tool
- [Editor](Editor.md) — Blazor WASM browser editor
- [Agents](Agents.md) — drop-in Claude + Copilot configs
- [Handy Prompts](Handy-Prompts.md) — example prompts for everyday wiki work: adding pages, editing content, reordering, and committing changes
