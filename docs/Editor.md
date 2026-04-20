# Editor

Wikidown ships a Blazor WebAssembly PWA that lets you browse and edit any
Wikidown wiki straight from the browser. It's hosted on GitHub Pages at
`/Wikidown/app/` and has no backend — commits go directly to your provider's
REST API.

## Providers

- **GitHub** — Personal Access Token. Commits via the Contents API.
- **Azure DevOps** — Personal Access Token. Commits via the Pushes API.

Tokens are kept in browser `localStorage`. Nothing is uploaded anywhere except
to the provider you connect.

## Browse and edit

- Read mode renders pages with MudMarkdown (CommonMark + KaTeX-ready).
- Edit mode is side-by-side: textarea on the left, live preview on the right.
- Save commits the change with a configurable message.

## Conflict handling

The editor records the file SHA (GitHub) or the commit OID of the branch HEAD
(ADO) before you edit. If the remote has moved when you save, the commit fails
with a "Reload remote" banner so you don't clobber someone else's change.

## Drafts

In-progress edits are kept per `(provider, owner, project, repo, branch, page)`
in `localStorage`. They survive page reloads and crashes, and clear on a
successful commit.

## PWA

The editor is installable. The service worker caches the MudBlazor assets and
the Blazor framework files, so it boots offline once you've visited it.
