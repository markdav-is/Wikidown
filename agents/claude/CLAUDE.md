## Documentation lives in `/docs` (Wikidown wiki)

- The `/docs` folder is a Wikidown wiki — structured markdown with `.order`
  navigation files. CLI commands (`wikidown read`, `wikidown write`, ...)
  address pages by title path: `/Getting-Started/Format`. Links written
  **inside page bodies** are different — see the wiki skill/subagent for the
  relative-link rule; do not use title-path links in markdown bodies, they
  404 on GitHub.
- A `wikidown-editor` subagent and a `wikidown` skill are configured for this
  repo. Use them for ANY read/write of `/docs/*.md`.
- Never edit `/docs/*.md` directly with `Write`/`Edit`. Use the `wikidown`
  CLI so `.order` files stay consistent.
- Prefer `wikidown edit` for small changes, `wikidown write-section` for one
  section, and `wikidown append` to add at the end; `wikidown write` is for
  full rewrites only and `wikidown new` for new pages.
- When you ship a feature that changes user-visible behavior, ask whether the
  wiki should be updated, and (if yes) delegate to `wikidown-editor`.
