[Home](../Home.md) / [Site Templates](../Site-Templates.md) / Building a Marketing Site <!-- wikidown:breadcrumb -->

# Building a Marketing Site

This how-to turns a Wikidown wiki into a marketing-style website: a landing
page at the site root, your own navigation and footer, sections pulled from
wiki pages, and listings driven by data files. Content stays in ordinary
wiki pages that anyone (or any AI agent) can edit with the CLI or the
editor.

The running example is the [Warehouse District Art
Hop](https://warehousedistrictarthop.com), a monthly art walk whose whole
site is a Wikidown wiki ([source on
GitHub](https://github.com/OpenEugene/art-hop), in `docs/`). Copy from it
freely.

## What you'll end up with

```text
docs/
├── _config.yml           # title, root_page, which pages use which layout
├── _data/
│   ├── site.json         # nav links, social links, membership tiers
│   └── hops/2026-11.json # this month's event: date, venues, notes
├── _layouts/
│   ├── landing.html      # the home page
│   └── wikidown.html     # every other page (restyled)
├── _includes/            # nav, footer, head, venue list, ...
├── assets/site.css
├── Home.md               # landing page intro text
├── Venues.md
├── Venues/Pinebox.md     # one page per venue
└── ...
```

## 1. Start from a wiki

Any wiki works. Write the content as normal pages first: a `Home` page
with a short intro, one page per product, venue, or feature, an `About`
page. Preview the plain site to make sure every page renders:

```powershell
wikidown export-html --output _site --clean
dotnet serve -d _site     # or: npx serve _site
```

`_site` is a build output; add it to `.gitignore`.

## 2. Make Home the site root

By default the site root `index.html` redirects to `Home.html`. For a
marketing site, render the Home page itself at `/` and give it its own
layout. In `_config.yml`:

```yaml
title: "Warehouse District Art Hop"
description: "A monthly art walk through the studios, galleries, and shops of west Eugene, Oregon."
favicon: /assets/icons/favicon.ico
url: https://warehousedistrictarthop.com

wikidown:
  root_page: /Home
  layouts:
    /Home: landing
```

`root_page` writes the Home page to `index.html` (as well as
`Home.html`, so existing links to it keep working), and its `page.url`
and nav entry become `/`. `layouts` sends Home through `_layouts/landing.html`; every other
page keeps the `wikidown` layout.

## 3. Put shared settings in a data file

Anything you'd otherwise repeat across templates goes in `_data/`. The Art
Hop keeps its nav, contact details, and membership tiers in
`_data/site.json`:

```json
{
  "currentHop": "2026-11",
  "email": "hello@example.org",
  "social": { "instagram": "https://www.instagram.com/..." },
  "nav": [
    { "title": "Home", "url": "/" },
    { "title": "Map", "url": "/Map.html" },
    { "title": "Venues", "url": "/Venues.html" },
    { "title": "Become a Member", "url": "/Membership.html" }
  ],
  "membership": {
    "tiers": [
      { "name": "Founding Fan", "price": 3 },
      { "name": "Big Believer", "price": 5 },
      { "name": "Super Supporter", "price": 10 }
    ]
  }
}
```

Templates read it as `site.data.site`. Changing next month's event is
then a one-line edit (`currentHop`) instead of a template change. See
[Shared Navigation and Footer](Data-Driven-Templates/Shared-Navigation-and-Footer.md).

## 4. Write the landing layout

A landing layout is a full HTML document. It doesn't have to look like the
wiki at all. The Art Hop's `_layouts/landing.html`, trimmed:

```liquid
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ site.title }}</title>
  {% include head.html %}
</head>
<body class="home">
  {% include nav.html %}
  {% assign cfg = site.data.site %}
  {% assign hop = site.data.hops[cfg.currentHop] %}

  <section class="hero">
    <h1>The Art Hop</h1>
    <div class="intro">{{ "Home.md" | wiki_body: "strip_title" }}</div>
  </section>

  {% if hop %}
  <section>
    <h2>{{ hop.date | date: "%B" }}'s participating venues</h2>
    {% include venue-list.html %}
  </section>
  {% endif %}

  <section>
    <h2>Become a supporting member</h2>
    {{ "Membership.md" | wiki_body: "strip_title" }}
    {% include membership.html %}
  </section>

  {% include footer.html %}
</body>
</html>
```

The pattern to notice: **copy lives in wiki pages, structure lives in the
layout**. `{{ "Home.md" | wiki_body: "strip_title" }}` drops the Home
page's text (minus its `# Home` heading) into the hero, so editing the
intro means editing `Home.md`, not HTML. The same goes for the membership
blurb.

## 5. Restyle the other pages

Interior pages still use `_layouts/wikidown.html`. Either keep the stock
one (run `wikidown pages` to get an editable copy) or replace it with your
own so the whole site shares one header and footer. The Art Hop's version
drops the sidebar and reuses the landing page's includes:

```liquid
<body class="interior">
  {% include nav.html %}
  <main class="page" id="content">
    {{ content }}
    {% if page.path == "Membership.md" %}{% include membership.html %}{% endif %}
  </main>
  {% include footer.html %}
</body>
```

`{{ content }}` is the page's rendered markdown. Checking `page.path`
lets one layout add a widget to a specific page without a separate layout.
See [Extras on Wiki Pages](Data-Driven-Templates/Extras-on-Wiki-Pages.md).

If a whole folder needs a different look, map it in `_config.yml`
(`/Venues/*: venue`) and write `_layouts/venue.html`.

## 6. Add styles and images

Put CSS, icons, and fonts in `assets/`; it's copied to the site as-is.
Link it with `relative_url` so the site still works under a base path:

```liquid
<link rel="stylesheet" href="{{ '/assets/site.css' | relative_url }}">
```

Page images go in `.attachments/`, as in any Wikidown wiki. For link
previews in Slack, Teams, and social apps, set `url` in `_config.yml` and
use `absolute_url` for `og:image` and `og:url`.

## 7. Preview and publish

Preview with the same two commands as step 1. A layout or include with a
Liquid error stops the export and names the file; a `wiki_body` that
names a missing page prints a warning.

These templates use `wiki_body`, `wikidown.layouts`, and `root_page`,
which GitHub's built-in Jekyll builder doesn't support, so publish with
`export-html` from CI. On GitHub, set **Settings → Pages → Source** to
"GitHub Actions" and add `.github/workflows/pages.yml`:

```yaml
name: pages
on:
  push:
    branches: [main]
    paths: ['docs/**', '.github/workflows/pages.yml']
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
      root: docs
```

For a custom domain, set it under **Settings → Pages** and keep a `CNAME`
file in the wiki root. Other hosts: see [Publishing to GitLab
Pages](../Getting-Started/Publishing-to-GitLab-Pages.md); the same
`export-html` command works in any CI.

## Keeping it editable

- Keep editable text in wiki pages and lists in `_data/`. Layouts should
  rarely change after launch.
- Give AI agents the same rule in your `CLAUDE.md` or Copilot
  instructions: content changes go through `wikidown`, data changes go in
  `_data/*.json`, and templates are left alone unless asked.
- Leave internal pages (meeting notes, plans) out of the published site
  with `wikidown.exclude_from_site` in `_config.yml`.

## Next

- [Data-Driven Templates](Data-Driven-Templates.md) for the full feature
  set and more examples.
- [Template Reference](Template-Reference.md) for every variable and
  filter.
