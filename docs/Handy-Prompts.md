[Home](Home.md) / Handy Prompts <!-- wikidown:breadcrumb -->

# Handy Prompts

Once your AI agent is wired up (see [Agents](Agents.md)), you don't need to
remember `wikidown` CLI verbs or flags — just describe what you
want in plain language. These are example prompts for the everyday wiki
work that comes up after initial setup.

## Adding a page

- "Add a new page called Troubleshooting under Getting Started, with a
  section on common install errors."
- "Create a page at /CLI/Advanced-Usage documenting the `--root` flag and
  the `WIKIDOWN_ROOT` environment variable."
- "I want a new top-level page called FAQ. Seed it with three placeholder
  questions I can fill in later."

## Changing the content of a page

- "Update the Format page to add a note about how image paths resolve
  after a move."
- "Rewrite the intro paragraph on Home to mention the new `export-pdf`
  command."
- "Find every page that mentions Jekyll and add a short note that
  `export-html` doesn't need Ruby."
- "Read the CLI page back to me — I want to check the `check-links`
  section is still accurate before I edit it."

## Reordering pages

- "Move Updating so it's the second page under Getting Started, right
  after Format."
- "Reorder the top-level nav so Agents comes before Editor."
- "List the pages under /Getting-Started in their current order."

## Moving or renaming a page

- "Rename /Testing/Browser-Test-Plan to /Testing/Manual-Test-Plan and fix
  any links that point at it."
- "Move /Meta/Release-Quirks under /Getting-Started since it's really a
  setup gotcha."

## Committing and pushing changes

- "Commit the wiki changes with a message summarizing what changed, then
  push."
- "Show me a diff of what you changed in /docs before committing."
- "Commit just the Handy Prompts page for now — I'm not ready to push the
  rest."

## Other useful prompts

- "Search the wiki for every mention of 'breadcrumb' and summarize what's
  documented."
- "Run check-links and fix anything it flags."
- "Walk the whole wiki and tell me if any page looks out of date."
