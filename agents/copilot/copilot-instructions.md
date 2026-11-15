# Project context

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — a
structured folder of markdown pages with `.order` navigation files.

## When you touch the wiki

- A `wikidown` Agent Skill is installed at `.github/skills/wikidown/SKILL.md`.
  Follow it for ANY read/write of `/docs`, and for publishing the site or
  changing its templates (`_layouts/`, `_includes/`), data (`_data/`), or
  `_config.yml`.
- Use the `wikidown` CLI in the terminal (`wikidown <command> --root docs ...`).
  Do **not** edit `/docs/*.md` files directly — that bypasses `.order`
  bookkeeping and breaks navigation. Site files (`_config.yml`, `_layouts/`,
  `_includes/`, `_data/`, `assets/`) are ordinary files; edit them normally.
- If you cannot run commands, show the user the exact `wikidown` commands
  to run instead of editing files.

## When you write code

- Standard repo conventions apply (see other docs / project files).
- If a code change adds or alters user-visible behavior, propose a wiki update
  using the skill above.
