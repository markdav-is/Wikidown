[Getting Started](../Getting-Started.md) / Updating <!-- wikidown:breadcrumb -->

# Updating

How a downstream repo picks up new Wikidown releases — CLI/MCP tool
versions, agent config files, and the VS extension each update differently.

## CLI and MCP server (NuGet global tools)

`Wikidown.Cli` and `Wikidown.Mcp` are published to NuGet as global .NET
tools. Update them with:

```sh
dotnet tool update -g Wikidown.Cli
dotnet tool update -g Wikidown.Mcp
```

**A push to `main` doesn't always mean a new version is available.** The
[`release.yml`](https://github.com/markdav-is/Wikidown/blob/main/.github/workflows/release.yml)
workflow runs on every push to `main` that touches `Directory.Build.props`,
`src/Wikidown.Cli/**`, `src/Wikidown.Mcp/**`, `src/Wikidown.Core/**`,
`assets/**`, or the workflow file itself, and it packs + pushes to NuGet with
`--skip-duplicate`. The package version comes from `<VersionPrefix>` in
`Directory.Build.props` (currently `0.2.1`) — and that value is **not**
auto-incremented per push. `--skip-duplicate` means a push that doesn't bump
`VersionPrefix` re-packs and tries to push the *same* version, which NuGet
rejects as a duplicate and the step just no-ops. So a plain code-fix push
only becomes something `dotnet tool update` can see once a maintainer bumps
`VersionPrefix` in the same push (or a later one).

If `dotnet tool update` reports you're already on the latest version but you
expected a fix, check whether `VersionPrefix` has actually moved since your
last update — the fix may be merged but not yet released.

## Agent configs (`.claude/`, `.github/`, `.vscode/mcp.json`, `CLAUDE.md`)

These files are copied into a downstream repo once, by `wikidown init` (see
[CLI](../CLI.md) and [Agents](../Agents.md)) — they don't auto-update. To
pick up changes to the shipped configs (wording tweaks, new instructions,
new tool wiring), re-run `init` with `--force`:

```sh
wikidown init --agents all --force
```

`--force` is required because `init` otherwise skips any file that already
exists in the target repo. Review the diff before committing — `--force`
overwrites local edits to those files too.

## VS extension (VSIX)

The Visual Studio extension auto-updates through the **Visual Studio
Marketplace**'s normal extension update mechanism once a new version is
published. Publishing is triggered by pushing a `vsix-v*` tag (see
[`vsix.yml`](https://github.com/markdav-is/Wikidown/blob/main/.github/workflows/vsix.yml)),
which builds the VSIX, runs `VsixPublisher.exe` against the Marketplace, and
attaches the `.vsix` to the GitHub Release. No action is needed downstream
beyond letting Visual Studio check for extension updates as usual — see
[Visual Studio Extension](../Getting-Started/Visual-Studio-Extension.md).

## Summary

| Component | Trigger to publish | How consumers update |
|---|---|---|
| `Wikidown.Cli` / `Wikidown.Mcp` (NuGet) | Push to `main` touching Core/CLI/MCP/`Directory.Build.props`, **and** a `VersionPrefix` bump | `dotnet tool update -g Wikidown.Cli` / `Wikidown.Mcp` |
| Agent configs | N/A — copied at `init` time | `wikidown init --agents all --force` |
| VS extension (VSIX) | Push of a `vsix-v*` tag | Automatic via VS Marketplace update check |
