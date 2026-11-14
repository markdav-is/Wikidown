[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Publishing to GitHub Pages <!-- wikidown:breadcrumb -->

# Publishing to GitHub Pages

A Wikidown wiki publishes to GitHub Pages through a GitHub Actions
workflow: on every push, the workflow runs `wikidown export-html` to render
the wiki to a static site and deploys it. `wikidown pages` writes that
workflow for you. That's how [wikidown.org](https://wikidown.org) itself is
published: it *is* this wiki, exported on every change.

> **Upgrading from 0.13 or earlier?** Wikidown no longer supports GitHub's
> built-in Jekyll builder ("Deploy from a branch"). See [Migrating from
> Jekyll Publishing](Migrating-from-Jekyll-Publishing.md).

## Quick start

From the repo root (`--root docs` is the default):

```powershell
wikidown pages
git add .github docs
git commit -m "Publish the wiki to GitHub Pages"
git push
```

Then in GitHub: **Settings → Pages → Source: "GitHub Actions"**. The
workflow runs on the push (watch it under the **Actions** tab), and the
wiki is live at `https://<owner>.github.io/<repo>/` when it finishes.

Options:

- `--title T`: site title. Defaults to the repo folder name.
- `--theme`: also copy the theme into the wiki so you can edit it (see
  [What it writes](#what-it-writes)).
- `--force`: overwrite files that already exist. **Careful:** with
  `--theme` this replaces edited theme files and a customized root
  `index.html`.

## What it writes

| File | Purpose |
|---|---|
| `.github/workflows/pages.yml` | Written at the root of the git checkout. Calls Wikidown's reusable workflow with `root:` set to the wiki folder. |
| `_config.yml` (wiki root) | Site settings: `title`, `description`, `repository_url` (GitHub link in the top bar), `favicon`, `url` and `image` (link previews), and `wikidown.exclude_from_site` (below). |

With `--theme`, it also copies the starter theme into the wiki root:

| File | Purpose |
|---|---|
| `_layouts/wikidown.html`, `_includes/nav-tree.html` | Top bar, collapsible left nav built from `.order` (active page highlighted, ancestors expanded), content column, footer. On narrow screens the nav becomes a slide-in drawer. |
| `assets/wikidown.css` | Styling, same palette as wikidown.org. |
| `index.html` | Redirects the site root to `/Home.html` (or the first top-level page if there is no Home). Replace it with a landing page if you want one. |

Without `--theme`, the CLI's built-in copy of the theme is used, so the
site picks up theme fixes when the workflow installs a newer CLI. Copy the
theme only when you want to change it; see [Customizing the
Theme](../Site-Templates/Customizing-the-Theme.md) and, for landing pages and
data-driven layouts, [Site Templates](../Site-Templates.md).

## The workflow

`wikidown pages` writes this file. You can also write it by hand:

```yaml
name: pages
on:
  push:
    branches: [main]
  workflow_dispatch:
permissions:
  contents: read
  pages: write
  id-token: write
concurrency:
  group: pages
  cancel-in-progress: false
jobs:
  pages:
    uses: markdav-is/Wikidown/.github/workflows/wiki-pages.yml@main
    with:
      root: docs              # the wiki folder, relative to the repo root
      # base-url: /my-repo    # only to override the detected path
      # cli-version: 0.14.0   # pin a Wikidown.Cli version; default is the latest
```

The reusable workflow installs the `Wikidown.Cli` tool, runs
`wikidown export-html --clean`, and deploys the result. The job fails if
the export fails, for example on a template error.

- **Project sites.** A site at `https://<owner>.github.io/<repo>/` lives
  under `/<repo>`. The workflow reads that path from GitHub and passes it
  as `--base-url`, unless `_config.yml` sets `baseurl` or you pass
  `base-url`. Links in page bodies are relative and never need it.
- **Custom domains.** Set the domain under **Settings → Pages**. A `CNAME`
  file in the wiki root is copied to the site with the other static files.
- **Pinning.** Without `cli-version`, each run installs the newest CLI. Pin
  it to control when theme and renderer changes reach your site.
- **Branches.** Change `branches: [main]` if your default branch has
  another name, and add `paths: ['docs/**']` to skip runs for code-only
  pushes.

## Excluding pages from the published site

Internal notes can stay in the wiki without shipping to the site. List
subtrees under `wikidown.exclude_from_site` in `_config.yml`:

```yaml
wikidown:
  exclude_from_site:
    - /Meta
    - /Testing
```

Excluded pages remain first-class for the CLI, editor, and
`check-links`. They just produce no `.html` and no nav entry.

Body links pointing *into* an excluded subtree will 404 on the published
site. `check-links` still validates them against the repo (where the pages
exist), so trim such links from published pages yourself.

## How the wiki maps to the site

- Relative `.md` and `.md#anchor` links become `.html` links, in markdown
  and in raw HTML and inline SVG `href`s (see [Format § Raw HTML and inline
  SVG](Format.md#raw-html-and-inline-svg)).
- A page's title is its first `# Heading`, so pages need no front matter.
  The nav uses the same title: `Attacks-Defense.md` headed
  `# Attacks & Defense` shows as "Attacks & Defense".
- The nav tree is built from the `.order` files at export time, so it's
  always current.
- The output is one `.html` per page, the root `index.html`, `assets/`,
  `.attachments/`, and every other static file in the wiki root (images,
  install scripts, favicons, extra stylesheets). Wiki sources (`.md`,
  `.order`), `_`-prefixed folders, other dot-files, and Gemfiles stay out.
- **Only files inside the wiki folder publish.** An image linked from
  elsewhere in the repo (`../../assets/x.png`) 404s on the site.
  `wikidown check-links` flags these, and `wikidown check-links --fix`
  copies them into `.attachments/from-repo/` and relinks them.

## Previewing locally

```powershell
wikidown export-html --output _site --clean
dotnet serve -d _site      # dotnet tool install -g dotnet-serve
```

Or `npx serve _site`. Add `_site` to `.gitignore`. The export uses the
same theme, settings, and renderer as the workflow, so what you see is what
deploys.

`export-html` options:

- `--base-url /prefix`: prefix for theme links when the site isn't served
  from the domain root. Overrides `baseurl` in `_config.yml`.
- `--title T`: overrides the site title.
- `--clean`: empty the output folder first, so removed pages don't linger.

## Other hosts

`export-html` output is a plain static folder, so any CI can publish it:
[Publishing to GitLab Pages](Publishing-to-GitLab-Pages.md) covers GitLab,
and the same command works for Azure Static Web Apps, Netlify, or a file
share.

See [CLI](../CLI.md) for the full command list, [Format](Format.md) for the
on-disk conventions, and [Site Templates](../Site-Templates.md) for changing
how the site looks.
