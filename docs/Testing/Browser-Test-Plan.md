# Browser Test Plan

A handoff checklist for an AI agent (or human) driving a real browser — e.g.
via Playwright MCP — to exhaustively smoke-test Wikidown's public surfaces.

## How to use this page

Wikidown now has two distinct public surfaces:

1. **Marketing site** — `https://wikidown.org/` (static HTML + CSS, served by
   GitHub Pages; marketing only, no editor, no API).
2. **Editor PWA + API** — `https://wikidown.app/` (Blazor WASM + MudBlazor at
   `/`, .NET isolated Functions at `/api/*`, served by Azure Static Web
   Apps). The ASWA default hostname
   `https://victorious-wave-03164381e.7.azurestaticapps.net/` is still bound
   and should behave identically.

For every check, report **PASS**, **FAIL**, or **SKIPPED** with a one-line
reason. On any failure, attach:

- URL at time of failure
- Viewport size
- Browser + version
- Console errors (if any)
- Network errors (if any)
- Screenshot

Checks are ordered so you can stop early on a catastrophic failure
(e.g. site doesn't load).

## Marketing site — `https://wikidown.org/`

### Load and assets

- `GET /` returns HTTP 200 and renders without console errors.
- `<title>` is set and non-empty; favicon visible in the tab.
- Logo image loads and has **transparent corners** — no white square
  behind the rounded glyph on the page background. Sample pixels at the
  four corners of the logo element to confirm alpha = 0.
- All `<img>` / `<link rel="stylesheet">` resources return 200
  (Network panel has no reds).
- No mixed-content warnings; every asset is HTTPS.
- `og:title`, `og:description`, and `og:image` meta tags are present.

### Navigation

- **"Open the editor"** button → `https://wikidown.app/`.
- **"View on GitHub"** has `target="_blank"` and resolves to HTTP 200.
- Every nav / hero / footer link resolves to HTTP 200 — none are `#`
  or `javascript:void(0)`.
- `GET /app/` on the marketing origin returns 404 — the editor is **not**
  served from the marketing site anymore.

### Layout and readability

- The feature-card grids are fully visible — no horizontal overflow,
  no text clipped, no card overlaps another.
- Each card's kicker, heading, and body text are legible.
- The "Built for humans and agents" row has three equal-width cards
  on desktop with aligned code blocks at the bottom.

### Mobile breakpoint

- At viewport width ~480 px, grids stack into a single column
  without horizontal scroll.
- Hero stacks (logo above or below the copy; not overlapping).
- Hero text stays readable (no character clipped, no font <14 px).
- `document.documentElement.scrollWidth ==
  document.documentElement.clientWidth`.

## Editor PWA — `https://wikidown.app/`

### Shell and first paint

- `GET /` returns HTTP 200 and the Blazor WASM app reaches interactive
  state (loading spinner disappears within ~10 s on a fast connection).
- App bar shows the **W↓ logo** immediately to the left of the word
  **Wikidown**.
- The **Drafts** button is visible in the app bar. Clicking it on a
  clean profile shows the empty state (no drafts).
- Home page content renders — no perma-spinner, no red error banner.
- Browser console shows no unhandled exceptions during load.

### Static assets

- `GET /favicon.ico` → 200.
- `GET /apple-touch-icon.png` → 200.
- `GET /manifest.webmanifest` → 200. `Content-Type` is
  `application/manifest+json` (or `application/json`). Body is valid
  JSON and declares `name`, `short_name`, `start_url`, `display`, and
  at least one `icons` entry.

### Service worker

- DevTools → Application → Service Workers lists a registered SW for
  scope `/`, status **activated and is running**.
- DevTools → Application → Cache Storage shows at least one
  `offline-cache-*` entry populated after a reload.
- A hard reload (`Ctrl+Shift+R`) still produces a working app; a
  subsequent normal reload is served partly from cache (Network panel
  shows some requests flagged `(ServiceWorker)` or `(disk cache)`).

## API smoke tests — `/api/*`

The `/api/*` routes are served by the linked Azure Functions app. These
are quick unauthenticated probes — they don't touch real OAuth.

- `GET /api/config/github` → HTTP 200, `Content-Type: application/json`,
  body shape `{"clientId":"..."}`. If `clientId` is empty, the SWA's
  `GITHUB_CLIENT_ID` application setting isn't configured yet — mark
  **FAIL** and stop the OAuth section below.
- `GET /api/config/ado` → HTTP 200, body shape `{"clientId":"..."}`
  (empty string acceptable — ADO OAuth is not wired yet).
- `GET /api/auth/github/callback?code=not-a-real-code&state=xyz` → HTTP
  302 with `Location` containing `/connect#gh_error=` and
  `gh_state=xyz`. The exact error token should be `bad_verification_code`
  (GitHub's canonical response) or `token_exchange_failed`. Confirms
  the token-exchange round-trip is wired even without a valid code.
- `GET /api/auth/github/callback` (no `code`) → 302 to
  `/connect#gh_error=missing_code`.

## GitHub OAuth sign-in flow *(skip by default)*

Only run if a throwaway GitHub account is available to approve the
`Wikidown` OAuth App. Without one, mark every check in this section
**SKIPPED**.

With throwaway credentials:

- Navigate to `/connect`. The GitHub tab shows a **Sign in with GitHub**
  button as the primary action, *not* a PAT field by default. A
  "Use a PAT instead" toggle is visible.
- Fill Owner + Repo for a repo the throwaway account can access.
- Click **Sign in with GitHub**. Browser leaves the app and lands on
  `github.com/login/oauth/authorize?...` with the `Wikidown` app name
  visible on the consent screen.
- Approve. Browser returns through
  `/api/auth/github/callback?code=...&state=...` and lands on
  `/connect` with the hash immediately cleaned up by
  `history.replaceState` (inspect the address bar — no `#gh_token=` left
  behind after first paint).
- The app auto-navigates to `/browse` and the snackbar reads
  "Connected to <owner>/<repo>".
- DevTools → Application → Local Storage has key
  `wikidown.connection.v1` with a JSON value that includes `token`
  (the access token) and `provider: "GitHub"`.
- DevTools → Application → Session Storage is empty — `wikidown.gh_state`
  and `wikidown.gh_pending` have been cleared.
- Returning to `/connect` shows the "Connected: ..." banner with
  **Browse** and **Disconnect** buttons.
- Clicking **Disconnect** removes `wikidown.connection.v1` from Local
  Storage and the connected banner disappears.

State-mismatch negative test:

- Start a sign-in, then manually set `sessionStorage['wikidown.gh_state']`
  to a different value before returning from GitHub. The `/connect` page
  should show a "state mismatch" error and **not** store a connection.

## ASWA default hostname — `https://victorious-wave-03164381e.7.azurestaticapps.net/`

The vanity domain is `wikidown.app`, but the ASWA default hostname stays
bound as a safety net. Every user-facing check above should behave
identically here.

- `GET /` returns 200 and reaches interactive state.
- TLS certificate is valid (Azure-managed, no browser interstitial).
- `/api/config/github` and `/api/ping` respond the same as on
  `wikidown.app`.
- OAuth round-trip works — which requires the `Wikidown` OAuth App to
  list **both** `https://wikidown.app/api/auth/github/callback` **and**
  `https://victorious-wave-03164381e.7.azurestaticapps.net/api/auth/github/callback`
  as Authorization callback URLs.

## PWA install

Run in a Chromium-based browser (Chrome or Edge) where install prompts
are supported.

- At the editor origin, the install affordance appears — either a
  native install prompt fires, or the install icon shows in the address
  bar.
- Triggering install opens a standalone app window.
- The installed PWA's taskbar / dock / launchpad icon is the Wikidown
  W↓ icon, not a generic globe.
- Launching the installed PWA opens without browser chrome and lands
  on the editor Home page.
- Uninstalling cleanly removes the icon and leaves no zombie window.

## Responsive and theme

### Viewport widths

Test both live surfaces (marketing + editor) at:

- **360 px** (small phone) — no horizontal scroll, tap targets ≥ 40 px,
  all text readable.
- **768 px** (tablet) — layout adjusts, no awkward large gaps, nav still
  usable.
- **1280 px** (laptop) — full desktop layout, no stretched hero image,
  content max-width looks intentional.

### Landscape on mobile

- At 812×375 (iPhone landscape), the editor app bar stays visible and
  consumes ≤ ~15% of vertical space.
- Marketing hero does not push the CTA below the fold unreachably.

### Theme

- Toggle OS-level dark mode. The marketing site and editor either
  follow it or stay in their chosen theme — but never produce
  unreadable dark-on-dark or light-on-light text.
- No flash of wrong theme (FOUC) on reload.

## Regression checklist

A 10-item fast pass for any future deploy:

1. `https://wikidown.org/` returns 200 and renders.
2. Logo has transparent corners on the marketing site.
3. "Open the editor" button reaches `https://wikidown.app/`.
4. The editor origin returns 200 and reaches interactive state with no
   console errors.
5. App bar shows `W↓ Wikidown` with Drafts menu visible.
6. `manifest.webmanifest` returns 200 and is valid JSON.
7. Service worker is registered and activated.
8. `GET /api/config/github` returns 200 with a non-empty `clientId`.
9. `GET /api/auth/github/callback` (no code) returns 302 to
   `/connect#gh_error=missing_code`.
10. 480 px mobile layout stacks without horizontal scroll.

## Known gaps

- **GitHub OAuth flow requires throwaway credentials** and an approved
  consent on the `Wikidown` OAuth App — SKIPPED without them.
- **ADO OAuth is not wired yet.** `/api/config/ado` returns an empty
  `clientId` on purpose; ADO tab still uses PAT.
- **No device-flow test.** GitHub Device Flow isn't implemented yet;
  it's only useful for CLI / MCP, not for this browser test.
- **No accessibility audit here.** A11y (axe, Lighthouse a11y) should
  live in a dedicated page.
- **No performance budgets.** Lighthouse scores are informational;
  thresholds aren't defined yet.

## Reporting format

When you finish, produce a short summary like:

```text
Marketing site:     12/12 PASS
Editor PWA shell:   9/9 PASS
API smoke tests:    4/4 PASS
GitHub OAuth:       SKIPPED (no throwaway creds)
Custom domain:      SKIPPED (not live)
PWA install:        4/5 PASS  (1 failure, see screenshot)
Responsive/theme:   7/7 PASS
Regression (10):    10/10 PASS
```

Attach every failure screenshot and a combined HAR of both surfaces.
