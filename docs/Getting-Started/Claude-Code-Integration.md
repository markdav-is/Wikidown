[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Claude Code Integration <!-- wikidown:breadcrumb -->

# Claude Code Integration

Set up Claude Code to keep your repo's `/docs` wiki current as it works. It
edits pages through the `wikidown` CLI, so `.order` files, breadcrumbs and
links stay consistent.

## Set it up

In Claude Code, at the root of your repo:

> Install Wikidown from wikidown.org and run `wikidown init --agents claude`
> in this repo.

Or from a terminal:

```sh
curl -fsSL https://wikidown.org/install.sh | sh     # Windows: irm https://wikidown.org/install.ps1 | iex
wikidown init --agents claude
```

`init` seeds `docs/Home.md` if the wiki is empty and installs the files
below. Commit them, so everyone who opens the repo in Claude Code gets the
same behavior.

## What gets installed

| File | What it does |
|---|---|
| `.claude/skills/wikidown/SKILL.md` | The `wikidown` skill: format rules, a command cheat sheet, and the editing workflow. Claude loads it for any task that touches `/docs`. |
| `.claude/agents/wikidown-editor.md` | The `wikidown-editor` subagent (Bash, Read, Grep and Glob tools). It owns wiki reads and writes, and Claude hands it documentation work on its own. |
| `CLAUDE.md` | A "Documentation lives in `/docs`" section, appended to your existing file or written fresh. It tells every session to use the skill and subagent, never to write `/docs/*.md` directly, and to ask whether the wiki needs updating when a change alters user-visible behavior. |

## Everyday use

Ask in plain words; Claude picks the right `wikidown` command:

- "Document the new `--retry` flag in the wiki."
- "What does the wiki say about deploying to staging?"
- "Rename the Setup page to Installation and fix every link to it."
- "Walk the wiki and list pages that no longer match the code."

For a small change Claude uses `wikidown edit`, `write-section` or `append`
rather than rewriting the whole page, which keeps diffs small and reviews
easy. After shipping a feature, expect it to ask whether the wiki should be
updated.

## Windows and Git Bash

On Windows, Claude Code runs shell commands in Git Bash, which rewrites
arguments that start with `/` into Windows paths (`/Setup` becomes
`C:/Program Files/Git/Setup`). The skill already tells Claude to pass page
paths without the leading slash (`--path Getting-Started/Format`) or to set
`MSYS_NO_PATHCONV=1`. Do the same when you run `wikidown` from Git Bash
yourself.

## Check links in CI

Catch broken links and pages missing from their parent before they merge.
Add a step like this to a GitHub Actions workflow:

```yaml
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: 10.0.x
- run: |
    dotnet tool install -g Wikidown.Cli
    echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"
- run: wikidown check-links
```

`check-links` exits non-zero on any problem and skips example links inside
code blocks.

## Keeping the configs current

The installed files are copies; they don't update themselves. After
upgrading Wikidown, run `wikidown init --agents claude --force` and review
the diff before committing. `--force` overwrites the skill and subagent,
including any local edits to them. `CLAUDE.md` is only touched when it
doesn't already mention the wiki. See [Updating](Updating.md) and
[Agents](../Agents.md) for more.
