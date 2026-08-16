[Home](../Home.md) / [Meta](../Meta.md) / Release Quirks <!-- wikidown:breadcrumb -->

# Release Quirks

Maintainer-facing notes on this repo's two release pipelines — NuGet
packages and the VS Marketplace VSIX — covering things that aren't obvious
from reading the workflow YAML alone. For the consumer-facing side of the
same pipelines (how a downstream repo picks up a release once it ships),
see [Updating](../Getting-Started/Updating.md).

## NuGet (`Wikidown.Core` / `Wikidown.Cli` / `Wikidown.Mcp`)

Driven by
[`release.yml`](https://github.com/markdav-is/Wikidown/blob/main/.github/workflows/release.yml).

- **Trigger is a path filter, and it's narrower than the test matrix.** The
  workflow only fires on a push to `main` touching `Directory.Build.props`,
  `src/Wikidown.Cli/**`, `src/Wikidown.Mcp/**`, `src/Wikidown.Core/**`,
  `assets/**`, or the workflow file itself. It does **not** include
  `src/Wikidown.Pdf/**` — even though the workflow's own
  `dotnet test Wikidown.slnf` step exercises `Wikidown.Pdf` code, because
  `Wikidown.Cli` references it. A `Wikidown.Pdf`-only change won't
  auto-trigger a release, even when it plausibly should. Trigger one
  manually instead:

  ```sh
  gh workflow run release.yml --ref main
  ```

  (`workflow_dispatch` is enabled for exactly this case.)

- **Version comes from `<VersionPrefix>` in `Directory.Build.props`, and
  nothing bumps it for you.** The publish step uses `--skip-duplicate`, so
  pushing without bumping `VersionPrefix` doesn't fail — it silently
  no-ops: the packages get repacked at the same version, `dotnet nuget
  push` tries to publish, NuGet.org rejects it as a duplicate, and the step
  reports success anyway. A green workflow run is not proof a new version
  shipped; check whether `VersionPrefix` actually moved in the commit that
  triggered it.

- **NuGet.org indexing lags behind a green run.** After the workflow
  finishes, `https://api.nuget.org/v3-flatcontainer/<package-id-lowercase>/index.json`
  can take several minutes to reflect the new version. Don't treat "not
  there yet" a minute after a green run as a sign something failed —
  give it time before investigating further.

## VS Marketplace VSIX (`Wikidown.Vs`)

Driven by
[`vsix.yml`](https://github.com/markdav-is/Wikidown/blob/main/.github/workflows/vsix.yml).

- **Its version number is completely separate from the NuGet packages.**
  It comes from `<Identity Version="...">` in
  `src/Wikidown.Vs/source.extension.vsixmanifest`. Bumping `Directory.Build.props`'s
  `VersionPrefix` has zero effect here — these are two unrelated semver
  numbers that happen to look alike.

- **A plain push to `main` never publishes anything, even one that changes
  `Wikidown.Vs` source.** The workflow triggers on every push to `main`
  touching `src/Wikidown.Vs/**`, `Directory.Build.props`, or the workflow
  file itself, but that only runs the build + verify steps as a CI check.
  The two steps that actually ship a release — "Publish to Visual Studio
  Marketplace" and "Attach VSIX to GitHub Release" — are both gated on
  `if: startsWith(github.ref, 'refs/tags/vsix-v')`. Shipping a new VSIX
  version is a two-part action:

  1. Bump the manifest version, commit, push to `main`.
  2. Separately create and push a matching tag:

     ```sh
     git tag vsix-v1.5.0
     git push origin vsix-v1.5.0
     ```

  Only the tag push triggers the Marketplace publish and the GitHub
  Release attach — pushing the version-bump commit alone does not.

- **Publisher identity** lives in `src/Wikidown.Vs/publish.json`
  (publisher `MarkDavis`, internal name `wikidown`) — this is what
  `VsixPublisher.exe` reads when it authenticates and uploads.

- **Requires the `VSIX_PAT` repo secret** (a Marketplace publish PAT). It's
  an Azure DevOps PAT (scope: All accessible organizations, Marketplace →
  Manage), created 2026-07-30 and expiring around late July 2027 — once it
  expires, publish runs start failing with auth errors. Renew by
  generating a fresh token at aex.dev.azure.com and running
  `gh secret set VSIX_PAT`.

- **The bundled-CLI build step needs its own restore, and leaks stray
  output folders.** `Wikidown.Vs.csproj` has a `PublishBundledCli` MSBuild
  target that bundles a framework-dependent copy of `Wikidown.Cli` into the
  VSIX (under `Tools\cli\`, used by the extension's "Export to PDF"
  command to shell out via `dotnet exec`). That target's nested
  `<MSBuild Projects="...Wikidown.Cli.csproj" Targets="Restore;Publish">`
  call needs its own explicit `Restore` — `vsix.yml`'s "Restore NuGet
  packages" step only restores `Wikidown.Vs.csproj` itself, not
  `Wikidown.Cli`, so a plain `Targets="Publish"` fails on a clean CI
  checkout with `project.assets.json not found`. Separately: the outer
  `msbuild` invocation in `vsix.yml`'s "Build VSIX" step passes
  `/p:OutDir=...`, and that property leaks into the nested
  `Wikidown.Cli` / `Wikidown.Core` / `Wikidown.Pdf` builds too, leaving
  stray `artifacts/vsix/` folders in their own project directories.
  Harmless — it doesn't affect what ships in the VSIX — but confusing if
  you reproduce the exact CI command locally and go looking for where
  those folders came from.

## See also

- [Updating](../Getting-Started/Updating.md) — how a downstream repo picks
  up new releases from both pipelines.
- [Vibing Phase Recap](Vibing-Phase-Recap.md) — build-log notes on getting
  the rest of Wikidown's infra (WASM editor, Functions API, marketing
  site) standing up.
