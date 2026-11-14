[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Migrating from Jekyll Publishing <!-- wikidown:breadcrumb -->

# Migrating from Jekyll Publishing

Starting with **Wikidown 0.14.0**, a wiki publishes only through a
pipeline that runs `wikidown export-html`. GitHub's built-in Jekyll
builder ("Deploy from a branch") is no longer supported: the CLI stops
generating `_data/navigation.yml`, `wikidown pages` writes a GitHub
Actions workflow instead of a Jekyll site, and new theme changes are
tested only against `export-html`.

One renderer means one set of behavior: the site you preview locally is
the site that deploys, and features like [Site Templates](../Site-Templates.md)
work everywhere.

## Are you affected?

Check **Settings → Pages → Build and deployment → Source** in your repo.

| Source | What to do |
|---|---|
| "Deploy from a branch" | Follow every step below. |
| "GitHub Actions", calling `wiki-pages.yml` | Only [step 4](#step-4-clean-up-jekyll-leftovers): delete `_data/navigation.yml`. |
| Not on GitHub (GitLab Pages, other CI running `export-html`) | Only [step 4](#step-4-clean-up-jekyll-leftovers). |

## Step 1: Update the CLI

```powershell
dotnet tool update -g Wikidown.Cli
```

Or re-run the [install script](../CLI.md). Check with `wikidown --help`:
the `pages` entry should mention a GitHub Actions workflow.

## Step 2: Add the workflow

From the repo root:

```powershell
wikidown pages
```

This writes `.github/workflows/pages.yml` at the root of the checkout. Your
existing `_config.yml` and theme files are left alone (`exists, skipped`),
and the command lists Jekyll leftovers it finds. Commit the workflow, but
don't push yet.

If your wiki isn't in `docs/`, pass `--root`; the workflow's `root:` is set
to match.

## Step 3: Switch the Pages source

In GitHub, **Settings → Pages → Source: "GitHub Actions"**. Then push. The
`pages` workflow runs and deploys the site; watch it under the **Actions**
tab. If you pushed before switching, run it again from the Actions tab
(**pages → Run workflow**).

- **Project sites** (`https://<owner>.github.io/<repo>/`): nothing to do.
  The workflow detects the `/<repo>` path, as GitHub's Jekyll builder did.
- **Custom domains**: the domain stays in **Settings → Pages**. Keep the
  `CNAME` file in the wiki root; it's copied to the site.

## Step 4: Clean up Jekyll leftovers

None of these break the build, but they're dead weight now:

- **`_data/navigation.yml`**: delete it. The nav is built from `.order`
  files at export time, and `export-html` warns while the file exists.
- **`Gemfile`, `Gemfile.lock`**: delete them if they were only for
  previewing with Jekyll locally.
- **Jekyll keys in `_config.yml`**: `export-html` ignores `markdown`,
  `kramdown`, `plugins`, `relative_links`, `titles_from_headings`,
  `defaults`, `include`, `exclude`, `theme`, and `remote_theme`. Delete
  them. Keep `title`, `description`, `url`, `baseurl`, `favicon`, `image`,
  `repository_url`, and the `wikidown:` block. A clean starting point:

  ```yaml
  title: "My Project"
  description: ""
  # repository_url: https://github.com/<owner>/<repo>
  # favicon: /favicon.ico
  wikidown:
    exclude_from_site:
      - /Internal-Notes
  ```

- **Pages you hid with Jekyll's `exclude:`**: list them under
  `wikidown.exclude_from_site` instead, as wiki page paths (`/Meta`), or
  they'll now be published.

## Step 5: Check what rendered differently

Preview locally before or after the switch:

```powershell
wikidown export-html --output _site --clean
dotnet serve -d _site
wikidown check-links
```

Things Jekyll did that `export-html` doesn't:

- **Liquid in page bodies.** Jekyll ran `{{ }}` and `{% %}` in every page;
  `export-html` runs Liquid only in layouts and includes. Remove
  `{% raw %}` / `{% endraw %}` wrappers, which now show up as text. A page
  that used Liquid for content (`{{ site.title }}`, a loop over
  `site.data`) needs that moved into a layout. See [Extras on Wiki
  Pages](../Site-Templates/Data-Driven-Templates/Extras-on-Wiki-Pages.md).
- **Front matter in pages.** A `---` block at the top of a page renders as
  text. Wikidown pages don't use front matter: titles come from the first
  `# Heading`, and layouts are chosen in `_config.yml` under
  `wikidown.layouts` ([Data-Driven
  Templates](../Site-Templates/Data-Driven-Templates.md#per-page-layouts-and-the-site-root)).
- **Jekyll themes and plugins.** `theme:` / `remote_theme:` (minima and
  friends) and plugins such as `jekyll-seo-tag`, `jekyll-feed`, and
  `jekyll-sitemap` don't run. The site uses the Wikidown theme or your own
  `_layouts/`. Put SEO and preview tags in your layout's `<head>`; set
  `url` and `image` in `_config.yml` for the stock theme's link previews.
- **Jekyll-only template features.** `where_exp`, `group_by`, `{% link %}`,
  `{% post_url %}`, `{% highlight %}`, `site.posts`, and collections aren't
  available. The [Template Reference](../Site-Templates/Template-Reference.md)
  lists what is, including the Jekyll filters `export-html` provides
  (`relative_url`, `absolute_url`, `markdownify`, `jsonify`, ...).

Customized theme files keep working: `export-html` reads `_layouts/`,
`_includes/`, and `assets/` from the wiki root, and understands Jekyll's
`{% include file.html key=value %}` syntax.

## If you can't switch yet

Pin the old CLI (`dotnet tool install -g Wikidown.Cli --version 0.13.0`)
and keep "Deploy from a branch" until you're ready. With 0.14.0 or later,
editing the wiki no longer refreshes `_data/navigation.yml`, so a
branch-deployed site's sidebar goes stale as pages are added or moved.
