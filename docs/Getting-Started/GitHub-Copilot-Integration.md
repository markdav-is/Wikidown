[Home](../Home.md) / [Getting Started](../Getting-Started.md) / GitHub Copilot Integration <!-- wikidown:breadcrumb -->

# GitHub Copilot Integration

Set up GitHub Copilot, in VS Code and as the coding agent on github.com, to
keep your repo's `/docs` wiki current. It edits pages through the `wikidown`
CLI, so `.order` files, breadcrumbs and links stay consistent.

## Set it up

In Copilot agent mode in VS Code, at the root of your repo:

> Install Wikidown from wikidown.org and run `wikidown init --agents copilot`
> in this repo.

Or from a terminal:

```sh
curl -fsSL https://wikidown.org/install.sh | sh     # Windows: irm https://wikidown.org/install.ps1 | iex
wikidown init --agents copilot
```

`init` seeds `docs/Home.md` if the wiki is empty and installs the files
below. Commit them, so everyone on the team, and the coding agent, gets the
same behavior.

## What gets installed

| File | What it does |
|---|---|
| `.github/copilot-instructions.md` | Repo-wide instructions: use the skill for anything in `/docs`, go through the `wikidown` CLI rather than editing files, and propose a wiki update when a code change alters user-visible behavior. |
| `.github/skills/wikidown/SKILL.md` | The `wikidown` skill: format rules, a command cheat sheet, and the editing workflow. |
| `.github/agents/wikidown.agent.md` | The `wikidown` custom agent (run-commands and search tools). Pick it in the VS Code agent picker; the coding agent on github.com uses it too. |
| `.github/chatmodes/wikidown.chatmode.md` | A chat mode for wiki editing. Without terminal access it shows you the exact `wikidown` commands to run instead. |

If your repo already has a `.github/copilot-instructions.md`, `init` leaves
it alone. Copy the wiki section from
[`agents/copilot/copilot-instructions.md`](https://github.com/markdav-is/Wikidown/blob/main/agents/copilot/copilot-instructions.md)
into it by hand. Passing `--force` would replace your file instead.

## Everyday use

In VS Code, switch to the `wikidown` agent (or chat mode) and ask in plain
words:

- "Document the new `--retry` flag in the wiki."
- "What does the wiki say about deploying to staging?"
- "Rename the Setup page to Installation and fix every link to it."

With the default agent, the repo instructions still steer Copilot to the
skill whenever a task touches `/docs`.

## The coding agent on github.com

Assign an issue such as "Document the retry settings in the wiki" to
Copilot, and it follows the same instructions and opens a pull request. It
can only run `wikidown` if the CLI is installed in its environment, so add
`.github/workflows/copilot-setup-steps.yml`:

```yaml
name: Copilot setup steps
on: workflow_dispatch
jobs:
  copilot-setup-steps:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: |
          dotnet tool install -g Wikidown.Cli
          echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"
```

The job must be named `copilot-setup-steps`; GitHub runs it before the
agent starts work.

## Check links in CI

Catch broken links and pages missing from their parent before they merge.
Add these steps to a GitHub Actions workflow:

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
upgrading Wikidown, run `wikidown init --agents copilot --force` and review
the diff before committing. `--force` overwrites all four files, including
your own `copilot-instructions.md` and any local edits. See
[Updating](Updating.md) and [Agents](../Agents.md) for more.
