# Browser Test Plan

A handoff checklist for an AI agent (or human) driving a real browser — e.g.
via Playwright MCP — to exhaustively smoke-test Wikidown's public surfaces.

## How to use this page

Wikidown currently has three public web surfaces:

1. **Marketing site** — `https://wikidown.org/` (static HTML + CSS).
2. **Editor PWA** — `https://wikidown.org/app/` (Blazor WASM + MudBlazor; PAT-gated flows require a GitHub or Azure DevOps token).
3. **Editor at custom domain** — `https://wikidown.app/` (planned; may or may not be live at test time).

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

- **"Open the editor"** button → `https://wikidown.org/app/`
  (trailing slash preserved).
- **"View on GitHub"** has `target="_blank"` and resolves to HTTP 200.
- Every nav / hero / footer link resolves to HTTP 200 — none are `#`
  or `javascript:void(0)`.

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

## Editor PWA — `https://wikidown.org/app/`

### Shell and first paint

- `GET /app/` returns HTTP 200 and the Blazor WASM app reaches
  interactive state (loading spinner disappears within ~10 s on a
  fast connection).
- App bar shows the **W↓ logo** immediately to the left of the word
  **Wikidown**.
- The **Drafts** button is visible in the app bar. Clicking it on a
  clean profile shows the empty state (no drafts).
- Home page content renders — no perma-spinner, no red error banner.
- Browser console shows no unhandled exceptions during load.

### Static assets

- `GET /app/favicon.ico` → 200.
- `GET /app/apple-touch-icon.png` → 200.
- `GET /app/manifest.webmanifest` → 200. `Content-Type` is
  `application/manifest+json` (or `application/json`). Body is valid
  JSON and declares `name`, `short_name`, `start_url`, `display`, and
  at least one `icons` entry.

### Service worker

- DevTools → Application → Service Workers lists a registered SW for
  scope `/app/`, status **activated and is running**.
- DevTools → Application → Cache Storage shows at least one
  `offline-cache-*` entry populated after a reload.
- A hard reload (`Ctrl+Shift+R`) still produces a working app; a
  subsequent normal reload is served partly from cache (Network panel
  shows some requests flagged `(ServiceWorker)` or `(disk cache)`).

### PAT-gated flows *(skip by default)*

Only run if a throwaway GitHub or Azure DevOps PAT has been supplied.
Without one, mark every check in this section **SKIPPED**.

With a throwaway PAT:

- Connect / Sign-in flow accepts the PAT without a console error.
- After connecting, the repo / wiki picker populates.
- Opening a wiki loads the page tree; opening a page renders its
  markdown with working links.
- Editing a page and saving produces a commit / push visible in the
  backing repo.
- Signing out clears the token from storage
  (DevTools → Application → Local Storage / IndexedDB).

## Custom domain — `https://wikidown.app/`

This surface is **planned** and may not be live at test time.

- `GET /` returns 200. If it returns DNS error, connection refused,
  or a parking page, mark the entire section **SKIPPED — not yet live**
  and continue.
- If live, functionally identical to `https://wikidown.org/app/`
  (same shell, logo, Drafts menu).
- TLS certificate is valid and not self-signed; no browser
  interstitial.
- Service worker registers for scope `wikidown.app/`.

## PWA install

Run in a Chromium-based browser (Chrome or Edge) where install
prompts are supported.

- At `https://wikidown.org/app/`, the install affordance appears —
  either a native install prompt fires, or the install icon shows
  in the address bar.
- Triggering install opens a standalone app window.
- The installed PWA's taskbar / dock / launchpad icon is the
  Wikidown W↓ icon, not a generic globe.
- Launching the installed PWA opens without browser chrome and
  lands on the editor Home page.
- Uninstalling cleanly removes the icon and leaves no zombie window.

## Responsive and theme

### Viewport widths

Test all live surfaces at:

- **360 px** (small phone) — no horizontal scroll, tap targets
  ≥ 40 px, all text readable.
- **768 px** (tablet) — layout adjusts, no awkward large gaps, nav
  still usable.
- **1280 px** (laptop) — full desktop layout, no stretched hero image,
  content max-width looks intentional.

### Landscape on mobile

- At 812×375 (iPhone landscape), the editor app bar stays visible
  and consumes ≤ ~15% of vertical space.
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
3. "Open the editor" button reaches `/app/`.
4. `https://wikidown.org/app/` reaches interactive state with no
   console errors.
5. App bar shows `W↓ Wikidown` with Drafts menu visible.
6. `manifest.webmanifest` returns 200 and is valid JSON.
7. Service worker is registered and activated.
8. Favicon and apple-touch-icon return 200.
9. 480 px mobile layout stacks without horizontal scroll.
10. PWA install affordance appears in a supporting browser.

## Known gaps

- **`wikidown.app` may not be live.** Treat a missing custom domain
  as SKIPPED, not FAIL.
- **PAT-gated flows are not covered** unless the test runner has a
  throwaway GitHub or Azure DevOps token.
- **No OAuth / device-code flow test.** Those aren't implemented yet.
- **No accessibility audit here.** A11y (axe, Lighthouse a11y)
  should live in a dedicated page.
- **No performance budgets.** Lighthouse scores are informational;
  thresholds aren't defined yet.

## Reporting format

When you finish, produce a short summary like:

```text
Marketing site:     12/12 PASS
Editor PWA shell:   9/9 PASS
PAT flows:          SKIPPED (no token)
Custom domain:      SKIPPED (not live)
PWA install:        4/5 PASS  (1 failure, see screenshot)
Responsive/theme:   7/7 PASS
Regression (10):    10/10 PASS
```

Attach every failure screenshot and a combined HAR of both surfaces.
