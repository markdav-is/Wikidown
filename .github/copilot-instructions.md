# Project context

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — an
Azure-DevOps-Wiki-compatible folder of markdown.

## When you touch the wiki

- A `wikidown` Agent Skill is installed at `.github/skills/wikidown/SKILL.md`.
  Follow it for ANY read/write of `/docs`.
- Use the `wikidown` MCP server's tools (surfaced as `wikidown_wiki_*`).
  Do **not** edit `/docs/*.md` files directly — that bypasses `.order`
  bookkeeping and breaks navigation.
- If MCP isn't available, fall back to the `wikidown` CLI:
  `wikidown <command> --root docs ...`.

## When you write code

- Standard repo conventions apply (see other docs / project files).
- If a code change adds or alters user-visible behavior, propose a wiki update
  using the skill above.
