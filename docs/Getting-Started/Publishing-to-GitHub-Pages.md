[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Publishing to GitHub Pages <!-- wikidown:breadcrumb -->

# Publishing to GitHub Pages

GitHub Pages can publish a Wikidown wiki straight from the repo using Jekyll,
its built-in static-site generator — no build pipeline, and nothing to
install: GitHub's servers run Jekyll, your machine never does. `wikidown pages`
scaffolds everything Jekyll needs into the wiki root, plus a starter theme
with a left-navigation tree that follows your `.order` files.

Not on GitHub, or want to preview locally? `wikidown export-html` renders
the **same theme** in .NET with no Jekyll or Ruby involved — see
[Any other host: `export-html`](#any-other-host-export-html) and
[GitLab Pages](#gitlab-pages) below.

## Quick start

```sh
wikidown pages            # run from the repo root; --root docs is the default
git add docs && git commit -m "Publish wiki with GitHub Pages" && git push
```

Then in GitHub: **Settings → Pages → Source** "Deploy from a branch", branch
`main`, folder `/docs`. The wiki is live at
`https://<owner>.github.io/<repo>/` a minute later.

Options:

- `--title T` — site title; defaults to the repo folder name.
- `--force` — overwrite theme files you've edited.

## What it scaffolds

Everything lands inside the wiki root:

| File | Purpose |
|---|---|
| `_config.yml` | Jekyll config: GFM markdown, the three GitHub-bundled plugins (`jekyll-relative-links`, `jekyll-titles-from-headings`, `jekyll-default-layout`), `include: [.attachments]` so images work (Jekyll skips dot-folders by default), default layout `wikidown`. Set `repository_url` here to get a GitHub link in the top bar. |
| `_data/navigation.yml` | The nav tree, generated from `.order`. **Regenerated automatically** by the CLI and MCP server on every write/move/delete/reorder once it exists — never edit by hand. |
| `_layouts/wikidown.html`, `_includes/nav-tree.html` | The starter theme: top bar, collapsible left nav (active page highlighted, ancestors expanded), content column, footer. Responsive — the nav becomes a slide-in drawer on narrow screens. |
| `assets/wikidown.css` | Styling, same palette as wikidown.org. Edit freely; `pages` never overwrites it without `--force`. |
| `index.html` | Redirects the site root to `/Home.html` (or the first top-level page if there is no Home). |

## Why it works with the Wikidown format

- Relative `.md` links (including the auto breadcrumb) are rewritten to the
  generated `.html` URLs by `jekyll-relative-links`.
- Page titles come from the first `# Heading`, so pages need no YAML front
  matter.
- `.order` files are dotfiles, so Jekyll ignores them; the nav tree reads
  them via `_data/navigation.yml` instead, because GitHub's Pages builder
  can't run custom plugins.
- `wikidown check-links` ignores `_`-prefixed folders and folders with no
  markdown, so the scaffolded files don't trip the index-page audit.

## Gotchas

- **Liquid in page bodies.** Jekyll processes `{{ }}` and `{% %}`
  everywhere, including code blocks. Wrap such content in
  `{% raw %} … {% endraw %}`. (`export-html` does *not* run Liquid over
  page bodies, so it's unaffected.)
- **Project sites live under `/<repo>/`.** The theme uses `relative_url`
  everywhere so this just works; if you serve from a custom domain, set
  `baseurl: ""` in `_config.yml`.
- **Don't `.nojekyll`.** That file disables Jekyll and would serve raw
  `.md` files.
- **Keep `_data/navigation.yml` committed.** If it's missing the layout
  falls back to a flat alphabetical page list.

## Any other host: `export-html`

```sh
wikidown export-html --output public
```

Renders every page through the same `_layouts/wikidown.html`,
`_includes/nav-tree.html`, and `assets/wikidown.css` that GitHub's Jekyll
would use — but in-process, with [Markdig](https://github.com/xoofx/markdig)
for markdown and [Fluid](https://github.com/sebastienros/fluid) for Liquid.
The output folder is a complete static site: one `.html` per page,
`index.html` redirect, `assets/`, and `.attachments/` copied through.
Nothing to install beyond the CLI.

- Works **with or without** having run `wikidown pages`. If the wiki root
  has theme files, they're used (so your customizations apply); anything
  missing falls back to the built-in copy. `_config.yml`'s `title`,
  `description`, `repository_url`, and `baseurl` are honored.
- `--base-url /prefix` — prefix for theme links (stylesheet, nav, redirect)
  when the site isn't served from the domain root, e.g. GitLab project
  sites at `https://<group>.gitlab.io/<project>/`. Overrides `baseurl` in
  `_config.yml`. Links inside page bodies are relative and never need it.
- `--title T` — overrides the site title.
- `--clean` — delete the output folder first, so removed pages don't
  linger.

Links, titles, and the nav tree match the Jekyll output: relative
`.md`/`.md#anchor` links become `.html`, the title is the first `# Heading`,
and the sidebar is built from `.order` (live, not from
`_data/navigation.yml`, so it's always current).

**Local preview** is just the export plus any static file server, e.g.
`dotnet serve -d public` (`dotnet tool install -g dotnet-serve`) or
`npx serve public`. Theme authors can iterate on `_layouts`/`assets` this
way before pushing to GitHub.

Theme files are written in Jekyll's Liquid dialect; `export-html` translates
the one construct that differs (`{% include file a=b %}` / `include.a`) on
the fly, so a single theme serves both paths. Stick to standard Liquid
filters and tags — Jekyll-only extras like `where_exp` aren't available in
the .NET renderer.

## GitLab Pages

GitLab Pages publishes whatever a CI job leaves in a `public/` artifact, and
doesn't run Jekyll for you — so use `export-html`. Run `wikidown pages` first
if you want the theme files in the repo to customize (optional), then add
`.gitlab-ci.yml` at the repo root:

```yaml
pages:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  stage: deploy
  script:
    - dotnet tool install -g Wikidown.Cli
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - wikidown export-html --output public --base-url "/$CI_PROJECT_NAME" --clean
  artifacts:
    paths:
      - public
  rules:
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
```

The job must be named `pages` and the artifact folder must be `public` —
both are GitLab conventions. Drop `--base-url` if the project uses a custom
domain or a group-level site served from `/`. Push; **Deploy → Pages**
shows the URL once the job finishes. The same three-line script works for
any CI that deploys a static folder (Azure Static Web Apps, Netlify,
Cloudflare Pages, S3).

See [CLI](../CLI.md) for the full command list, and
[Format](Format.md) for the on-disk conventions the generated site relies on.
