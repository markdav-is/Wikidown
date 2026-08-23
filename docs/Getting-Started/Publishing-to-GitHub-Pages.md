[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Publishing to GitHub Pages <!-- wikidown:breadcrumb -->

# Publishing to GitHub Pages

GitHub Pages can publish a Wikidown wiki straight from the repo using Jekyll,
its built-in static-site generator — no build pipeline, and nothing to
install: GitHub's servers run Jekyll, your machine never does. `wikidown pages`
scaffolds everything Jekyll needs into the wiki root, plus a starter theme
with a left-navigation tree that follows your `.order` files.

Using GitLab instead? The same scaffold works — see
[GitLab Pages](#gitlab-pages) below for the one extra step.

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
  `{% raw %} … {% endraw %}`.
- **Project sites live under `/<repo>/`.** The theme uses `relative_url`
  everywhere so this just works; if you serve from a custom domain, set
  `baseurl: ""` in `_config.yml`.
- **Don't `.nojekyll`.** That file disables Jekyll and would serve raw
  `.md` files.
- **Keep `_data/navigation.yml` committed.** If it's missing the layout
  falls back to a flat alphabetical page list.
- **Previewing locally** means running Jekyll yourself, which needs Ruby —
  Wikidown deliberately doesn't scaffold that. Push to a branch and let
  GitHub build it, or check the rendered markdown on github.com.

## GitLab Pages

GitLab Pages serves static sites too, but with one difference that matters:
**GitLab doesn't run Jekyll for you.** It publishes whatever a CI job leaves
in a `public/` artifact. So the scaffold from `wikidown pages` is used
unchanged — layout, CSS, nav include, `_data/navigation.yml` — and you add a
pipeline that runs Jekyll in a container. Nothing is installed on your
machine either way.

1. Run `wikidown pages` as above and commit the result.

2. Add a `Gemfile` **in the wiki folder** (`docs/Gemfile`). GitHub
   preinstalls the three plugins the theme relies on; GitLab's container
   doesn't, so they have to be declared:

   ```ruby
   source "https://rubygems.org"
   gem "jekyll", "~> 4.3"
   gem "jekyll-relative-links"
   gem "jekyll-titles-from-headings"
   gem "jekyll-default-layout"
   ```

   Add `Gemfile` and `Gemfile.lock` to the `exclude:` list in
   `docs/_config.yml` so they aren't copied into the site.

3. Set `baseurl` in `docs/_config.yml`. GitHub fills this in automatically;
   GitLab doesn't. For the default project URL
   `https://<group>.gitlab.io/<project>/` use `baseurl: "/<project>"`; for a
   custom domain or a group-level site leave it `""`.

4. Add `.gitlab-ci.yml` at the repo root:

   ```yaml
   pages:
     image: ruby:3.3
     stage: deploy
     variables:
       BUNDLE_PATH: vendor/bundle
     cache:
       paths:
         - docs/vendor/bundle
     script:
       - cd docs
       - bundle install
       - bundle exec jekyll build --destination ../public
     artifacts:
       paths:
         - public
     rules:
       - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
   ```

   The job must be named `pages` and the artifact folder must be `public` —
   both are GitLab conventions. Running Jekyll from inside `docs/` means the
   `_config.yml`, `_layouts`, and `_data` there are picked up with no extra
   flags.

5. Push. **Deploy → Pages** in the project shows the URL once the job
   finishes. Pages must be enabled on the instance (it is on gitlab.com).

Everything in [Gotchas](#gotchas) applies to GitLab as well, plus one more:
`Gemfile.lock` is generated by the first `bundle install` — commit it after
the first successful pipeline so later builds are reproducible.

See [CLI](../CLI.md) for the full command list, and
[Format](Format.md) for the on-disk conventions the generated site relies on.
