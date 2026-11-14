[Home](Home.md) / Site Templates <!-- wikidown:breadcrumb -->

# Site Templates

A Wikidown wiki publishes as a static website. The pages are always plain
markdown; **templates** decide what the site around them looks like. The
same wiki can come out as a documentation site with a sidebar, or as a
marketing site with a landing page, its own navigation, and sections built
from data files.

## Two kinds of site

| | Wiki template | Marketing-style templates |
|---|---|---|
| Looks like | Docs site: top bar, `.order`-driven sidebar, page body | Anything: hero, cards, listings, pricing, custom nav |
| Files | `_layouts/wikidown.html`, `_includes/nav-tree.html`, `assets/wikidown.css` | Your own `_layouts/*.html`, `_includes/*.html`, `assets/*`, plus `_data/*` |
| Set up by | Nothing: `export-html` has it built in. `wikidown pages --theme` copies it for editing | You write them; `_config.yml` says which pages use them |
| Example | [wikidown.org](https://wikidown.org)'s docs pages, this page included | [warehousedistrictarthop.com](https://warehousedistrictarthop.com) ([source](https://github.com/OpenEugene/art-hop)) |

You don't pick one or the other. A marketing site is usually the wiki
template for most pages plus one or two custom layouts: the Art Hop site
gives its home page a `landing` layout and every other page its own
restyled `wikidown` layout. wikidown.org keeps the stock wiki layout and
replaces only the root `index.html`.

## How a page is built

1. The page's markdown is rendered to HTML (Markdig, GitHub-style heading
   IDs, tables, task lists).
2. `_config.yml` picks the page's layout: an entry under
   `wikidown.layouts`, or `wikidown` by default.
3. The layout is rendered with Liquid. It sees the page as `page` and
   `{{ content }}`, and the whole site as `site`: every page, the
   `.order` nav tree, `_config.yml` values, and everything in `_data/`.
4. If the layout's front matter names another layout, the result is
   wrapped in that one too.
5. Static files (images, `assets/`, `.attachments/`, `CNAME`) are copied
   through unchanged.

Wiki pages never carry front matter, so they stay readable in GitHub, the
editor, and to AI agents. Everything about presentation lives in
`_config.yml`, `_layouts/`, `_includes/`, and `_data/`.

## Pages in this section

- [Building a Marketing Site](Site-Templates/Building-a-Marketing-Site.md):
  step by step, from a plain wiki to a landing page with custom nav,
  sections pulled from wiki pages, and data-driven listings.
- [Customizing the Theme](Site-Templates/Customizing-the-Theme.md): edit
  the stock wiki layout, or replace the root `index.html` with a
  hand-written landing page.
- [Data-Driven Templates](Site-Templates/Data-Driven-Templates.md): `_data/`
  files, `site.pages`, `wiki_body`, per-page layouts, and `root_page`, with
  worked examples.
- [Template Reference](Site-Templates/Template-Reference.md): every `site`
  and `page` variable, every filter, the `_config.yml` keys, and links to
  the Liquid, Fluid, and Jekyll documentation.

For deployment, see [Publishing to GitHub
Pages](Getting-Started/Publishing-to-GitHub-Pages.md) and [Publishing to
GitLab Pages](Getting-Started/Publishing-to-GitLab-Pages.md).
