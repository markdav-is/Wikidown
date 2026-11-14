[Home](../Home.md) / [Site Templates](../Site-Templates.md) / Customizing the Theme <!-- wikidown:breadcrumb -->

# Customizing the Theme

`wikidown export-html` has a starter theme built in, so a wiki publishes
with no theme files at all. To change how it looks, copy the theme into
the wiki and edit it:

```powershell
wikidown pages --theme
```

That writes two independent sets of files into the wiki root. They solve
different problems:

| Template | Wraps | Purpose |
|---|---|---|
| `_layouts/wikidown.html` + `_includes/nav-tree.html` + `assets/wikidown.css` | Every wiki page (`Home.html`, `Guides.html`, …) | Page chrome: top bar, the `.order`-driven left-nav sidebar, footer |
| `index.html` | Nothing; it's standalone | The site root, free to look nothing like the wiki |

`export-html` uses any of these files it finds in the wiki root and falls
back to its built-in copy for the rest, so you can keep just the files you
change. A file you've copied no longer picks up theme updates from newer
CLI versions; delete it to go back to the built-in one. `wikidown pages`
never overwrites existing files unless you pass `--force`.

For deployment, see [Publishing to GitHub
Pages](../Getting-Started/Publishing-to-GitHub-Pages.md) and [Publishing to
GitLab Pages](../Getting-Started/Publishing-to-GitLab-Pages.md).

## The wiki page template

`_layouts/wikidown.html` is the Liquid layout every page goes through:
`<head>`, top bar, a slot for `{{ content }}` (the rendered markdown), and
the sidebar. `_includes/nav-tree.html` renders the sidebar recursively from
`site.data.navigation`, the tree built from your `.order` files, with
active-page highlighting and collapsible sections. `assets/wikidown.css` is
the styling.

The templates are [Liquid](https://shopify.github.io/liquid/), with site
conventions borrowed from Jekyll, such as `{% include file.html a=b %}`
read back as `include.a`. For every variable
and filter, see the [Template Reference](Template-Reference.md). For
templates that pull in `_data/` files or other pages' bodies, or a
different layout on some pages, see [Data-Driven
Templates](Data-Driven-Templates.md).

## The root `index.html`

`index.html` is **not** wrapped by `_layouts/wikidown.html`: the stock file
carries `layout: none` in its front matter. That means it's free to look
nothing like the rest of the wiki: no sidebar, its own nav bar, a hero
section, feature cards, whatever a homepage needs.

The stock version is a redirect: a meta-refresh and a canonical link that
send visitors to the wiki's home page, plus a visible fallback link.

**[wikidown.org](https://wikidown.org)'s own `index.html` is a working
example of replacing it**: hero section, feature cards, and its own top nav
that links into the wiki with a plain `href="Home.html"`. Copy from
[`docs/index.html`](../index.html) in this repo.

Two alternatives to a hand-written `index.html`:

- Set `wikidown.root_page` in `_config.yml` to serve a wiki page as the
  site root, with its own layout. The page's text stays editable as
  markdown. See [Building a Marketing Site](Building-a-Marketing-Site.md).
- Keep the redirect and put the landing content on the Home page.

## The `{{HOME}}` / `{{TITLE}}` placeholders

The stock `index.html` contains two placeholders:

```html
<meta http-equiv="refresh" content="0; url={{ '{{HOME}}' | relative_url }}">
<title>{{ site.title }}</title>
```

`{{HOME}}` and `{{TITLE}}` are plain text, not Liquid. They're replaced
with a string replace before the template is rendered: once by
`wikidown pages --theme` when it writes the file, and again by
`export-html` on every export, so a file that still contains them stays
current.

`{{HOME}}` becomes the wiki's `/Home` page if one exists, else the first
top-level page, always with a `.html` suffix (`/Home.html`). The sidebar
links the Home page as `/` instead.

The placeholder sits inside a Liquid string, `'{{HOME}}'`, so after the
replace it's ordinary Liquid (`'/Home.html' | relative_url`) and still gets
the site's base path. To make your own "go to the docs" link follow the
wiki's home page, reuse `{{ '{{HOME}}' | relative_url }}`. Hard-coding
`href="Home.html"`, as wikidown.org does, works too.

## Previewing changes

```powershell
wikidown export-html --output _site --clean
dotnet serve -d _site
```

Any static file server works. Edits to `_layouts/`, `_includes/`,
`assets/`, or `index.html` show up on the next export.
