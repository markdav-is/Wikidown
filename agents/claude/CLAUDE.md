## Documentation lives in `/docs` (Wikidown wiki)

- The `/docs` folder is a Wikidown wiki — structured markdown with `.order`
  navigation files. Tool calls (`wiki_read`, `wiki_write`, ...) address pages
  by title path: `/Getting-Started/Format`. Links written **inside page
  bodies** are different — see the wiki skill/subagent for the relative-link
  rule; do not use title-path links in markdown bodies, they 404 on GitHub.
- A `wikidown-editor` subagent and a `wikidown` skill are configured for this
  repo. Use them for ANY read/write of `/docs/*.md`.
- Never edit `/docs/*.md` directly with `Write`/`Edit`. Use the `wiki_*` MCP
  tools so `.order` files stay consistent.
- Prefer `wiki_edit` for small changes, `wiki_write_section` for one
  section, and `wiki_append` to add at the end; `wiki_write` is for new
  pages or full rewrites only.
- When you ship a feature that changes user-visible behavior, ask whether the
  wiki should be updated, and (if yes) delegate to `wikidown-editor`.
