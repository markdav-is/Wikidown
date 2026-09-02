[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Publishing to GitLab Pages <!-- wikidown:breadcrumb -->

# Publishing to GitLab Pages

GitLab Pages serves static sites, but unlike GitHub it doesn't run a
site generator for you — it publishes whatever a CI job leaves in a
`public/` artifact. That suits Wikidown fine: `wikidown export-html`
renders the wiki to finished HTML with the same starter theme the
GitHub path uses, in one CI job, with no Jekyll or Ruby anywhere (see
[Publishing to GitHub Pages](Publishing-to-GitHub-Pages.md) for the
GitHub story and the full `export-html` reference).

## Steps

1. *(Optional)* Run `wikidown pages` and commit the result, if you want
   the theme files (`_layouts`, `assets`, `_config.yml`) in your repo to
   customize. `export-html` works without this — anything missing falls
   back to the built-in theme.

2. Add `.gitlab-ci.yml` at the repo root:

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

   The job must be named `pages` and the artifact folder must be
   `public` — both are GitLab conventions.

3. Push. **Deploy → Pages** in the project shows the URL once the job
   finishes — `https://<group>.gitlab.io/<project>/` by default. Pages
   must be enabled on the instance (it is on gitlab.com).

## `--base-url`

Project sites are served under `/<project>/`, and the theme's own links
(stylesheet, nav, root redirect) need that prefix — `--base-url
"/$CI_PROJECT_NAME"` supplies it. Drop the flag if the project uses a
**custom domain** or a **group-level site** served from `/`. Links inside
page bodies are relative and never need it.

## Notes

- The CI container runs .NET, so nothing is installed on your machine —
  same as the GitHub path.
- `wikidown.exclude_from_site`, `favicon`, `title`, and the other
  `_config.yml` settings are honored — see
  [Publishing to GitHub Pages](Publishing-to-GitHub-Pages.md) for what
  each does.
- The same three-line script works for any CI that deploys a static
  folder: Azure Static Web Apps, Netlify, Cloudflare Pages, S3.
- Want a custom `_layouts/wikidown.html`/`assets/wikidown.css` or a
  hand-authored root `index.html` instead of the stock redirect? Same
  files, same rules as the GitHub path — see [Customizing the
  Theme](Customizing-the-Theme.md).

See [CLI](../CLI.md) for the full `export-html` option reference.
