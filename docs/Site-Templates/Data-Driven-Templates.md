[Home](../Home.md) / [Site Templates](../Site-Templates.md) / Data Driven Templates <!-- wikidown:breadcrumb -->

# Data-Driven Templates

`wikidown export-html` can render a full site — a landing page with a venue list, a map page with this month's date, membership tiers — from data files and wiki pages, with no front matter in any wiki page.

These features belong to `export-html`. GitHub's built-in Jekyll builder doesn't have `wiki_body`, `wiki_title`, or the `wikidown.layouts` / `wikidown.root_page` keys, so a site that uses them deploys through [GitHub Actions](../Getting-Started/Publishing-to-GitHub-Pages.md#deploying-with-github-actions) instead of "Deploy from a branch".

## Data files: `site.data`

Every `.json`, `.yml`, and `.yaml` file under the wiki's `_data/` folder is loaded into `site.data`, keyed by file name without the extension. Subfolders become nested keys:

| File | Liquid |
|---|---|
| `_data/site.json` | `site.data.site` |
| `_data/team.yml` | `site.data.team` |
| `_data/hops/2026-11.json` | `site.data.hops["2026-11"]` |

```liquid
{% assign cfg = site.data.site %}
{% assign hop = site.data.hops[cfg.currentHop] %}
<h2>{{ hop.date | date: "%B" }}'s venues</h2>
```

YAML scalars are typed the way Jekyll types them: unquoted numbers are numbers, so `sort` orders them numerically, and `yes`/`no` are booleans. A file that doesn't parse stops the export with an error that names the file.

`site.data.navigation` is always the nav tree built from `.order`. A `_data/navigation.yml` file doesn't replace it, because that file can be out of date.

Every other top-level key in `_config.yml` is also available as `site.<key>`, as in Jekyll.

## Other pages in a template

Each entry in `site.pages` has `url`, `title`, `name`, `path` (`Venues/Pinebox.md`), `dir`, `layout`, and `content` (the rendered HTML). Jekyll's `where` / `first` lookup works:

```liquid
{% assign vp = site.pages | where: "path", "Venues/Pinebox.md" | first %}
<a href="{{ vp.url | relative_url }}">{{ vp.title }}</a>
```

To show another page's text, use `wiki_body` instead of `content`:

```liquid
{{ "Venues/Pinebox.md" | wiki_body }}
{{ "Venues/Pinebox.md" | wiki_body: "strip_title" }}
{{ "Venues/Pinebox.md" | wiki_title }}
```

- `wiki_body` outputs the page's rendered body without its breadcrumb line. With `"strip_title"` it also drops the first `#` heading.
- `wiki_title` outputs the page's title (`Pinebox`).
- The input can be `Venues/Pinebox.md`, `/Venues/Pinebox`, or a page object from `site.pages`.
- `wiki_body` makes the page's relative links and images site-absolute (with the base URL), so they still work when the body is shown on a page in a different folder. `content` keeps them relative to the page's own folder.
- If no published page matches, the filter outputs nothing and `export-html` prints a warning.

## Filters

Fluid supplies standard Liquid filters (`where`, `map`, `first`, `date`, `truncatewords`, `strip_html`, ...). `export-html` adds the common Jekyll filters (`relative_url`, `absolute_url`, `markdownify`, `jsonify`, `slugify`, ...), a numeric `sort`, and its own `wiki_body` / `wiki_title`. Jekyll-only extras such as `where_exp` and `group_by` aren't available. The full list, with what each does, is in the [Template Reference](Template-Reference.md#filters).

## Per-page layouts and the site root

Wiki pages don't choose a layout; `_config.yml` does:

```yaml
wikidown:
  root_page: /Home      # also rendered as the site's index.html
  layouts:
    /Home: landing      # _layouts/landing.html
    /Map: map
    /Venues/*: venue    # every page under /Venues
```

- `layouts` maps a page's link path to a layout in `_layouts/<name>.html`. An exact path is matched first, then the closest `/Folder/*` entry above the page. Every other page uses `wikidown`. A layout that doesn't exist stops the export.
- A layout can put itself inside another layout with front matter (`layout: base`), as in Jekyll. `{{ content }}` is the page's rendered markdown.
- `root_page` serves that page as `index.html` and gives it the URL `/` in `page.url`, `site.pages`, and the nav. The page is still written to its own `.html` file, so breadcrumbs that link to it keep working. When `root_page` is set, the wiki root's `index.html` isn't used.

Without `root_page`, the wiki root's `index.html` is rendered as before. It gets a `page` object (`page.url` is `/`) plus any values from its front matter. A `layout:` other than `none` in that front matter is applied.

## Examples

Each example is taken from the [Warehouse District Art Hop](https://warehousedistrictarthop.com) site ([source](https://github.com/OpenEugene/art-hop)), and shows the data, the template, and why it's built that way.

- [Shared Navigation and Footer](Data-Driven-Templates/Shared-Navigation-and-Footer.md): a hand-picked top menu and footer from `_data/site.json`, with active-link highlighting.
- [Monthly Event Listing](Data-Driven-Templates/Monthly-Event-Listing.md): this month's venues from `_data/hops/<month>.json`, each with its blurb from its own wiki page.
- [Pricing Tiers](Data-Driven-Templates/Pricing-Tiers.md): membership cards and a "from $3 a month" line from one list.
- [Featured Story](Data-Driven-Templates/Featured-Story.md): a title, a 70-word excerpt, and a link for one page picked in a data file.
- [Extras on Wiki Pages](Data-Driven-Templates/Extras-on-Wiki-Pages.md): add a widget to specific pages from the layout, or give a folder its own layout.

For a start-to-finish walkthrough, see [Building a Marketing Site](Building-a-Marketing-Site.md). See [Customizing the Theme](Customizing-the-Theme.md) for the default layout and `index.html`, and [Publishing to GitHub Pages](../Getting-Started/Publishing-to-GitHub-Pages.md) for deployment.
