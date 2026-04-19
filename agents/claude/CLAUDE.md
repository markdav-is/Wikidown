<!--
Append this section to your repo's root CLAUDE.md. It tells the main Claude
agent that the wiki exists and to delegate wiki work to the wikidown-editor
subagent (or invoke the `wikidown` skill).
-->

## Documentation lives in `/docs` (Wikidown wiki)

- The `/docs` folder is a Wikidown wiki — Azure-DevOps-Wiki-compatible
  markdown. Page links use title form: `/Getting-Started/Format`.
- A `wikidown-editor` subagent and a `wikidown` skill are configured for this
  repo. Use them for ANY read/write of `/docs/*.md`.
- Never edit `/docs/*.md` directly with `Write`/`Edit`. Use the `wiki_*` MCP
  tools so `.order` files stay consistent.
- When you ship a feature that changes user-visible behavior, ask whether the
  wiki should be updated, and (if yes) delegate to `wikidown-editor`.
