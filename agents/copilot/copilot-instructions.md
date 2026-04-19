<!--
Place at .github/copilot-instructions.md. VS Code Copilot loads this file
automatically for every chat session in the repo.
-->

# Project context

This repo's documentation lives in `/docs` and is a **Wikidown** wiki — an
Azure-DevOps-Wiki-compatible folder of markdown.

## When you edit the wiki

- Use the `wikidown` MCP server's tools (`wiki_list`, `wiki_read`,
  `wiki_write`, `wiki_new`, `wiki_move`, `wiki_delete`, `wiki_reorder`,
  `wiki_search`, `wiki_walk`). They appear in Copilot as `wikidown_*` tools.
- Do **not** edit `/docs/*.md` files directly — that bypasses `.order`
  bookkeeping and breaks navigation.
- If MCP isn't available, fall back to the `wikidown` CLI:
  `wikidown <command> --root docs ...`.

## Wikidown format rules

- Page link path uses title form, hyphens for spaces:
  `/Getting-Started/Release-Notes`.
- The page on disk is `Getting-Started/Release-Notes.md`. Subpages of
  `/Parent` live in a `Parent/` folder beside `Parent.md`.
- Each folder's `.order` file controls navigation order. `wiki_write` keeps it
  consistent automatically.
- Internal links use the title path: `[Format](/Getting-Started/Format)`.

## When you write code

- Standard repo conventions apply (see other docs / project files).
- If a code change adds or alters user-visible behavior, propose a wiki update
  using the rules above.
