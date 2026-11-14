[Home](../Home.md) / [Site Templates](../Site-Templates.md) / Template Reference <!-- wikidown:breadcrumb -->

# Template Reference

Everything a layout or include can use when a wiki is rendered by
`wikidown export-html`. Templates are written in
[Liquid](https://shopify.github.io/liquid/), Jekyll's dialect, and rendered
by [Fluid](https://github.com/sebastienros/fluid), a .NET Liquid engine. A
template that sticks to what's listed here also works with GitHub's Jekyll
builder, except for the items marked *export-html only*.

## Files

| Path (in the wiki root) | Used for |
|---|---|
| `_config.yml` | Site settings; every top-level key is also `site.<key>` |
| `_layouts/<name>.html` | Layouts. `wikidown` is the default for every page |
| `_includes/<name>.html` | Partials for `{% include name.html %}` |
| `_data/**/*.json`, `*.yml`, `*.yaml` | Data, as `site.data` |
| `index.html` | The site root, when `root_page` isn't set |
| `assets/`, `.attachments/`, other files | Copied to the site unchanged |

Files you add override the built-in theme file of the same name, so a
wiki with no `_layouts/` still renders with the stock theme.

## `_config.yml` keys

| Key | Meaning |
|---|---|
| `title` | `site.title`. Defaults to the repo folder name |
| `description` | `site.description` |
| `url` | Absolute site address (`https://example.org`), used by `absolute_url` |
| `baseurl` | Path prefix for a project site (`/my-repo`). `--base-url` overrides it |
| `favicon`, `image`, `repository_url` | Exposed as `site.*` for the layouts to use |
| `wikidown.root_page` | Page rendered as the site's `index.html` with URL `/`. *export-html only* |
| `wikidown.layouts` | Map of page path (`/Home`, `/Venues/*`) to layout name. *export-html only* |
| `wikidown.exclude_from_site` | Page paths (with their subpages) to leave out of the site. *export-html only* |

Page paths can be written `/Home`, `Home`, or `Home.md`.

## `site`

| Variable | Contents |
|---|---|
| `site.title`, `site.description`, `site.url`, `site.baseurl`, `site.favicon`, `site.image`, `site.repository_url` | From `_config.yml` |
| `site.<any key>` | Any other top-level `_config.yml` key |
| `site.data.<file>` | A `_data/` file; subfolders nest: `site.data.hops["2026-11"]` |
| `site.data.navigation` | The nav tree built from `.order` files (see below) |
| `site.pages` | Every published page, in `.order` order, as page objects |

`site.data.navigation` is a list of items, each with `title`, `url`
(missing for a folder with no page), and for folders `prefix` (`/Venues/`)
and `children`. The stock `_includes/nav-tree.html` shows how to walk it.

## `page`

| Variable | Example | Notes |
|---|---|---|
| `page.title` | `Pinebox` | From the page's first `# Heading` |
| `page.url` | `/Venues/Pinebox.html` | `/` for the root page |
| `page.path` | `Venues/Pinebox.md` | File path from the wiki root |
| `page.name` | `Pinebox.md` | File name |
| `page.dir` | `/Venues/` | `/` at the top level |
| `page.layout` | `wikidown` | Layout chosen in `_config.yml` |
| `page.content` | rendered HTML | Links relative to the page's own folder |
| `content` | rendered HTML | In a layout: the page, or the inner layout's output |

The same fields are on every entry in `site.pages`. For the root
`index.html`, `page` holds `url`, `path`, `name`, `dir`, and whatever its
front matter sets.

## Filters

**Standard Liquid**, from Fluid: `abs`, `append`, `capitalize`, `ceil`,
`compact`, `concat`, `date`, `default`, `divided_by`, `downcase`, `escape`,
`first`, `floor`, `join`, `last`, `map`, `minus`, `modulo`,
`newline_to_br`, `plus`, `prepend`, `remove`, `replace`, `reverse`,
`round`, `size`, `slice`, `split`, `strip`, `strip_html`,
`strip_newlines`, `times`, `truncate`, `truncatewords`, `uniq`, `upcase`,
`url_encode`, `where`, and more. See the [Liquid filter
docs](https://shopify.github.io/liquid/filters/abs/) and [Fluid's
list](https://github.com/sebastienros/fluid#filters).

**Jekyll filters** added by `export-html`, matching [Jekyll's
behavior](https://jekyllrb.com/docs/liquid/filters/):

| Filter | Does |
|---|---|
| `relative_url` | Prefixes `baseurl` to a site path such as `/assets/site.css` |
| `absolute_url` | Prefixes `url` and `baseurl`; leaves full URLs alone |
| `markdownify` | Renders a markdown string (from a data file, say) to HTML |
| `jsonify` | Outputs a value as JSON, e.g. for a `<script>` block |
| `slugify` | `"Big Believer"` → `big-believer` |
| `xml_escape` | HTML/XML-escapes a string |
| `cgi_escape` | URL-encodes a string for a query parameter |
| `normalize_whitespace` | Collapses runs of whitespace to one space |
| `number_of_words` | Word count |
| `sort` | Sorts numbers as numbers (`3, 5, 10`, not `10, 3, 5`); `sort: "price"` sorts by a property |

**Wikidown filters** (*export-html only*):

| Filter | Does |
|---|---|
| `wiki_body` | Another page's rendered body, without the breadcrumb. Links are made site-absolute so it works anywhere |
| `wiki_body: "strip_title"` | The same, without the page's `# Heading` |
| `wiki_title` | Another page's title |

Their input is a path (`Venues/Pinebox.md`, `/Venues/Pinebox`) or a page
object from `site.pages`. An unknown page outputs nothing and the export
prints a warning.

**Dates.** `date` takes a date or a string like `2026-11-07` and a
[strftime](https://strftime.net/) format: `%B` (November), `%A, %B %-d,
%Y` (Saturday, November 7, 2026). See the [Liquid `date`
docs](https://shopify.github.io/liquid/filters/date/).

## Tags

Standard Liquid tags work: `assign`, `capture`, `if` / `elsif` / `else`,
`unless`, `case`, `for` (with `limit`, `offset`, `reversed`, `forloop.*`),
`break`, `continue`, `cycle`, `comment`, `raw`, `increment`. See the
[Liquid tag docs](https://shopify.github.io/liquid/tags/control-flow/).

`{% include file.html key=value %}` uses Jekyll's syntax, with the
parameters read as `include.key`, so the same includes work on both
renderers. Includes can include other includes.

Not available: Jekyll's `where_exp`, `group_by`, and `group_by_exp`
filters; the `link`, `post_url`, and `highlight` tags; `site.posts`
and collections. Use `where` plus `first`, or a data file, instead.

## Layout front matter

A layout can start with front matter naming the layout it sits inside:

```liquid
---
layout: base
---
<main>{{ content }}</main>
```

A missing layout, or one that includes itself, stops the export with an
error naming it. Wiki pages themselves never have front matter.

## Markdown

Pages are rendered with [Markdig](https://github.com/xoofx/markdig) and
its advanced extensions: GitHub-style heading IDs, pipe tables, task
lists, footnotes, auto-links, and inline HTML and SVG. Relative `.md`
links become `.html` links. The on-disk rules are in
[Format](../Getting-Started/Format.md).

## Data file parsing

JSON is parsed as-is. YAML is parsed with
[YamlDotNet](https://github.com/aaubry/YamlDotNet) and typed the way
Jekyll types it: unquoted numbers are numbers and `true`/`false`/`yes`/`no`
are booleans. Quote a value to keep it a string. A file that doesn't parse
stops the export with an error naming it.

## Further reading

- [Liquid for designers](https://shopify.github.io/liquid/basics/introduction/): the language itself
- [Fluid](https://github.com/sebastienros/fluid): the engine `export-html` uses, and how it differs from Ruby Liquid
- [Jekyll: Liquid](https://jekyllrb.com/docs/liquid/), [layouts](https://jekyllrb.com/docs/layouts/), [includes](https://jekyllrb.com/docs/includes/), [data files](https://jekyllrb.com/docs/datafiles/), [variables](https://jekyllrb.com/docs/variables/): the conventions Wikidown follows
- [GitHub Pages](https://docs.github.com/en/pages): hosting, custom domains, and Actions deploys
- [Markdig](https://github.com/xoofx/markdig): the markdown renderer
- [strftime.net](https://strftime.net/): date format codes
