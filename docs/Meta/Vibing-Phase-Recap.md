# Wikidown — "Vibing Phase" Recap

A build log / field notes of getting the basics standing up, from nothing to a deployed WASM editor with a working Functions API, a marketing site on its own domain, and a CLI + MCP tool pair.

## The shape of the thing

**Wikidown** is an Azure-DevOps-Wiki-compatible markdown wiki that lives in `/docs` of any git repo — no service to run, tokens never leave the browser. One codebase ships four surfaces:

- `Wikidown.Core` — the page model: `.order` files, title-form links (`/Parent/Child`), on-disk read/write.
- `Wikidown.Cli` — a `dotnet tool` called `wikidown` for list / read / write / move / search.
- `Wikidown.Mcp` — an MCP stdio server exposing the same surface to Claude Code, Claude Desktop, and VS Code Copilot.
- `Wikidown.Web` — a Blazor WASM + MudBlazor PWA editor.
- `Wikidown.Site` — a static marketing page.
- `Wikidown.Api` — a .NET isolated Azure Functions app for the OAuth token exchange.

The wiki eats its own dogfood: this repo's own `/docs` is a Wikidown wiki, edited through the `wikidown-editor` subagent and the `wikidown` skill.

## Deployment topology we landed on

After one wrong turn (more below), we split the surfaces cleanly:

- **`wikidown.org`** — GitHub Pages, marketing only. Plain HTML/CSS, zero backend, Porkbun DNS with four A records to GitHub's 185.199.108-111.153.
- **`victorious-wave-03164381e.7.azurestaticapps.net`** (future `wikidown.app`) — Azure Static Web Apps. Blazor WASM editor at `/`, managed Functions at `/api/*`.

GitHub Pages is the wrong host for OAuth (no runtime), so anything that needs a server — token exchange, config endpoints — lives on ASWA. Marketing-only stays on Pages because it's free, cached globally, and wiring a .NET WASM app into it gets you nothing.

## GitHub OAuth, the real way

The first cut of `/connect` was PAT-only with a frozen "GitHub OAuth Device Flow needs a small proxy…" caption. That caption was still true — a proxy was needed — so we built one.

- **`/api/config/github`** — returns the public `clientId` (empty string means the SWA app setting isn't wired).
- **`/api/auth/github/callback`** — exchanges `code` for a token against `https://github.com/login/oauth/access_token` using the server-side `GITHUB_CLIENT_SECRET`, then 302s back to `/connect#gh_token=…&gh_state=…`.
- **`/connect` in the WASM app** — generates a CSRF `state`, stashes pending form + state in sessionStorage, redirects to `github.com/login/oauth/authorize`, receives the fragment on return, validates state, stores the access token in localStorage, and scrubs the fragment via `history.replaceState` so no token is ever left in the address bar. A "Use a PAT instead" toggle keeps the old path available.

Device Flow was consciously deferred. It's the right answer for the CLI / MCP surfaces, not for a browser.

## Things that went sideways, and what they taught us

### 1. .NET 10 vs the world

The repo is on .NET 10 (pinned `10.0.100` in `global.json`) because it's fun and we can. Azure's Oryx builder — the image that powers `Azure/static-web-apps-deploy` — doesn't have a .NET 10 SDK yet. First ASWA run exploded with *"Requested SDK version 10.0.100 … Installed SDKs: [none match]"*.

Pattern that worked:

- **Pre-build the Blazor WASM editor on the GitHub runner** where `actions/setup-dotnet@v4` can install any SDK, then hand the published `wwwroot` to the deploy action with `skip_app_build: true`.
- **Let Oryx build the Functions API**, but drop a scoped `src/Wikidown.Api/global.json` pinning `9.0.x` with `rollForward: "latestMinor"` so Oryx picks its bundled 9.0 SDK instead of tripping on the root 10.0 pin. Letting Oryx do the API build also means it writes the `functions.metadata` file the deploy step needs to register functions.

Lesson: when you're ahead of the hosted build image, do the build on the runner and make the deploy action a pure uploader.

### 2. Functions language version

Oryx's first API-side error was unusually helpful: *"dotnetisolated versions are valid: 8.0, 9.0"*. We'd targeted net10. Retargeted `Wikidown.Api` alone to net9, everything else stayed on net10. Production net9 artifacts on a net9 isolated worker, no drift.

We also briefly had the API global.json on `latestMajor` — Oryx dutifully downloaded SDK 10.0.200 to build a net9 project. That's the drift we didn't want; tightening to `latestMinor` kept SDK and TFM aligned.

### 3. The service worker ate our API

Most satisfying bug of the phase. After deploy, every API function registered cleanly — the portal showed all five. But hitting `/api/ping` in a browser tab loaded the Blazor shell and rendered Blazor's own "Not Found" page. The `staticwebapp.config.json` correctly excluded `/api/*` from navigation fallback, so SWA was blameless.

The Blazor PWA service worker — the one template ships with — intercepts `mode === 'navigate'` fetches, finds the URL isn't in the asset manifest, and cheerfully serves cached `index.html`. The request never left the browser. Our `/api/*` route excludes were configured on the wrong layer.

Fix was four lines: in `service-worker.published.js`, detect same-origin `/api/*` and `return` from the fetch handler *before* calling `event.respondWith`, so the browser makes a plain network request SWA can route.

Lesson: in a Blazor PWA + co-located API setup, the service worker is the *first* router the request hits. Its exclude list is more important than any `staticwebapp.config.json` rule, because SWA never even sees the request if the SW shortcuts it.

### 4. The Porkbun ALIAS detour

`wikidown.org` briefly broke because Porkbun was serving an `ALIAS → markdav-is.github.io` record. GitHub Pages apex domains want four plain A records (185.199.108.153 / .109.153 / .110.153 / .111.153). Swapping them in made `NotServedByPagesError` go away and Let's Encrypt issued the cert within ~15 minutes.

### 5. The "(managed)" red herring

Azure Portal's SWA → APIs blade shows a blue banner: *"Bring your own API backends are not supported in the Free hosting plan. Click to upgrade."* Right below it, Production → Function App → `(managed)`. Those two things look contradictory but aren't — the banner only applies to BYO backends (external Function App, Container App, API Management). The managed Functions backend that ships with Free tier is right there, working, no upgrade needed. Worth calling out because the framing is actively misleading.

## Working state as of the handoff

- `https://wikidown.org/` — marketing site, live, HTTPS.
- `https://victorious-wave-03164381e.7.azurestaticapps.net/` — editor PWA, live, OAuth-aware Connect page shipping.
- All 5 Functions registered on the managed backend: `Ping`, `GitHubConfig`, `AdoConfig`, `GitHubCallback`, `AdoCallback`.
- `ci.yml` builds + tests + packs on every PR; `pages.yml` deploys the marketing site; `azure-static-web-apps-*.yml` pre-builds the editor on the runner and hands it + the API source to Oryx.
- Browser test plan in `/docs/Testing/Browser-Test-Plan.md` covers the two surfaces, the API smoke tests, and the OAuth flow.

## Known loose ends going into the inner loop

- Stale service worker needs evicting on the dev's machine before `/api/ping` is reachable from their regular browser.
- `GITHUB_CLIENT_ID` / `GITHUB_CLIENT_SECRET` need to be confirmed set on the SWA's Configuration → Application settings.
- `wikidown.app` custom domain not yet bound; when it lands, the OAuth App needs a second callback URL registered.
- ADO OAuth is stubbed — `/api/config/ado` returns empty `clientId` on purpose; ADO tab still uses PAT.
- Device flow (for CLI / MCP) is deferred until there's a real reason to ship it.

## The vibe, honestly

This phase was a lot of "the docs say X; the platform does Y; the build runs but the thing 404s anyway." The pattern that paid off over and over: when the hosted builder can't do it, move the work onto the GitHub runner. When the service worker is in the way, read the service worker. When the Azure Portal banner contradicts the row below it, read the row below it.

What's in place is the boring-but-load-bearing foundation — infra, auth, deploy, two live origins, a dogfed wiki, a CLI, an MCP server, a PWA. Next loop gets to actually build features on top.
