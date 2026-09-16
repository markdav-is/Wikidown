# Wikidown agent configs

Drop-in configs that teach AI coding assistants how to maintain a Wikidown
wiki through the `wikidown` CLI.

The fastest install is the CLI scaffolder — from your repo root:

```bash
dotnet tool install -g Wikidown.Cli
wikidown init --agents all
```

No .NET on the machine? The wikidown.org install script drops a
self-contained `wikidown` binary instead — `init` works identically:

```bash
curl -fsSL https://wikidown.org/install.sh | sh   # Windows: irm https://wikidown.org/install.ps1 | iex
wikidown init --agents all
```

`init` writes every file below to its destination (skipping anything that
already exists; `--force` overwrites, `--agents claude` / `--agents copilot`
narrows the set). The tables that follow are the manual-copy equivalent.

## Shared skill (Agent Skills standard)

One skill definition serves both assistants — Claude Code and GitHub Copilot
both support the Agent Skills `SKILL.md` format,
they just load it from different folders:

| File                          | Claude Code destination            | Copilot destination                |
| ----------------------------- | ---------------------------------- | ---------------------------------- |
| `skills/wikidown/SKILL.md`    | `.claude/skills/wikidown/SKILL.md` | `.github/skills/wikidown/SKILL.md` |

The skill carries the format rules, the command cheat sheet, the workflow,
and two fallbacks: what a chat-only host with no shell should do (suggest
the commands), and how to edit by hand if the CLI cannot run at all. The
per-agent files below are thin wiring on top of it.

## Claude Code

| File                          | Where to put it in your repo        |
| ----------------------------- | ----------------------------------- |
| `claude/wikidown.subagent.md` | `.claude/agents/wikidown-editor.md` |
| `claude/CLAUDE.md`            | append to your `CLAUDE.md`          |

The subagent owns `/docs` edits and has Bash for the CLI; the CLAUDE.md
snippet tells the main agent to delegate to it.

## GitHub Copilot

| File                              | Where to put it in your repo             |
| --------------------------------- | ---------------------------------------- |
| `copilot/copilot-instructions.md` | `.github/copilot-instructions.md`        |
| `copilot/wikidown.agent.md`       | `.github/agents/wikidown.agent.md`       |
| `copilot/wikidown.chatmode.md`    | `.github/chatmodes/wikidown.chatmode.md` |

The instructions file is loaded automatically into every Copilot chat in the
repo. The custom agent handles delegated wiki work (including the Copilot
coding agent on github.com); the chat mode adds a `wikidown` mode in VS Code.
Both run the CLI through the terminal tool.

## Both

Both agents need `wikidown` on `PATH`: the NuGet global tool when .NET is
present, or the self-contained binary from the install script when it is
not. There is no separate server to install or wire up.
