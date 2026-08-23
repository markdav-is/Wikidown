[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Publishing to GitHub Pages <!-- wikidown:breadcrumb -->

# Publishing to GitHub Pages

GitHub Pages can publish a Wikidown wiki straight from the repo using Jekyll,
its built-in static-site generator — no build pipeline. `wikidown pages`
scaffolds everything Jekyll needs into the wiki root, plus a starter theme
with a left-navigation tree that follows your `.order` files.

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
| `Gemfile` | For local preview only: `cd docs && bundle install && bundle exec jekyll serve`. |

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
  `{% raw %} … {% endraw %}`.
- **Project sites live under `/<repo>/`.** The theme uses `relative_url`
  everywhere so this just works; if you serve from a custom domain, set
  `baseurl: ""` in `_config.yml`.
- **Don't `.nojekyll`.** That file disables Jekyll and would serve raw
  `.md` files.
- **Keep `_data/navigation.yml` committed.** If it's missing the layout
  falls back to a flat alphabetical page list.

See [CLI](../CLI.md) for the full command list, and
[Format](Format.md) for the on-disk conventions the generated site relies on.
