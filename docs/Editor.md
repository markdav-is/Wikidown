# Editor

Wikidown ships a Blazor WebAssembly PWA that lets you browse and edit any
Wikidown wiki straight from the browser. It's hosted on Azure Static Web Apps
at `https://wikidown.app/` (default hostname
`https://victorious-wave-03164381e.7.azurestaticapps.net/` still serves the
editor, but OAuth sign-in only completes on `wikidown.app`), with a small
Functions app at `/api/*` that only exists to complete OAuth token exchange —
page commits still go directly to your provider's REST API from the browser.

## Providers

- **GitHub** — Sign in with GitHub (OAuth) or paste a fine-grained Personal
  Access Token. Commits via the Contents API.
- **Azure DevOps** — Personal Access Token. Commits via the Pushes API.
  (OAuth is planned but not yet wired.)

Access tokens are kept in browser `localStorage`. The Functions app never
sees your repo contents and never stores your token — it only swaps an
OAuth `code` for an access token and redirects back.

## Sign-in flow (GitHub)

1. Click **Sign in with GitHub** on `/connect`.
2. Redirect to `github.com/login/oauth/authorize` for the `Wikidown` OAuth
   App.
3. Return to `/oauth/github/return.html` — a tiny static page served by ASWA.
   Its JS reads `code` + `state` from the query string and `fetch`-POSTs them
   as a JSON body to `/api/auth/github/exchange`.
4. The `GitHubExchange` Function swaps `code` + `client_secret` for an access
   token against `https://github.com/login/oauth/access_token` and returns
   `{"token":"…"}` (or `{"error":"…"}`).
5. The relay page redirects to `/connect#gh_token=...&gh_state=...`. The SPA
   validates the CSRF `state`, stores the token in `localStorage`, scrubs the
   hash, and navigates to `/browse`.

The two-piece relay exists because Azure Functions on ASWA managed backends
treats `?code=` as a reserved function-key candidate and rejects requests
that carry it with a pre-dispatch HTTP 500 — even on `AuthorizationLevel.Anonymous`
routes. Since GitHub's OAuth redirect always appends `?code=…`, the callback
URL has to land somewhere other than `/api/*`. See
[`/Meta/Vibing-Phase-Recap`](/Meta/Vibing-Phase-Recap) for the war story.

A "Use a PAT instead" toggle on the same page is kept as a fallback.

GitHub OAuth Apps support a single `Authorization callback URL`, so the
`Wikidown` app is registered against `https://wikidown.app/oauth/github/return.html`.
Sign-in on the ASWA default hostname will fail with a `redirect_uri`
mismatch — use `wikidown.app` for OAuth, or paste a PAT as a fallback.

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
