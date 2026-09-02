[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Customizing the Theme <!-- wikidown:breadcrumb -->

# Customizing the Theme

`wikidown pages` scaffolds two independent templates. Both are plain
files in the wiki root, and both are optional to edit — but they solve
different problems and get replaced in different ways.

| Template | Wraps | Purpose |
|---|---|---|
| `_layouts/wikidown.html` + `_includes/nav-tree.html` + `assets/wikidown.css` | Every generated wiki page (`Home.html`, `Guides.html`, …) | Page chrome: top bar, the `.order`-driven left-nav sidebar, footer |
| `index.html` | Nothing — it's standalone | The site root; free to look nothing like the wiki |

This page covers customizing both. For the scaffold itself and publishing
mechanics, see [Publishing to GitHub Pages](Publishing-to-GitHub-Pages.md)
and [Publishing to GitLab Pages](Publishing-to-GitLab-Pages.md).

## The wiki page template

`_layouts/wikidown.html` is the Liquid layout every rendered page goes
through: `<head>`, top bar, a slot for `{{ content }}` (the rendered
markdown), and the sidebar. `_includes/nav-tree.html` renders that sidebar
recursively from `site.data.navigation` — the tree built from your `.order`
files — with active-page highlighting and collapsible sections.
`assets/wikidown.css` is the styling.

To customize: edit those three files directly in the wiki root. Both
GitHub's built-in Jekyll and `wikidown export-html` prefer wiki-root copies
over their embedded defaults, so your edits apply either way. Nothing
regenerates them once they exist — the only thing that overwrites them is
`wikidown pages --force`.

Both files are standard Liquid. `export-html`'s Fluid renderer translates
the one Jekyll-only construct it needs — `{% include file.html a=b %}`
becomes `include.a` — so sticking to standard Liquid filters and tags keeps
the layout working identically on both engines. Jekyll-only extras like
`where_exp` aren't available in the .NET renderer.

## The root `index.html`

Unlike every other generated page, `index.html` is **not** wrapped by
`_layouts/wikidown.html` at all — the scaffolded file carries `layout: none`
in its front matter, and both renderers treat it as fully standalone. That
means it's free to look nothing like the rest of the wiki: no sidebar, its
own nav bar, a hero section, feature cards — whatever a marketing homepage
needs.

The default scaffolded version is a trivial redirect stub: a meta-refresh
and a canonical link that send visitors straight to the wiki's home page,
plus a visible fallback link for the no-JS/no-refresh case.

**[wikidown.org](https://wikidown.org)'s own `index.html` is a real,
working example of replacing it** — hero section, feature cards, its own
top nav that links into the wiki with a plain `href="Home.html"`. It's
worth reading directly as the reference implementation to copy from:
[`docs/index.html`](../index.html) in this repo.

To customize: edit or replace `docs/index.html` after scaffolding. Both
publishing paths read it straight off disk when present:

- `wikidown pages` only writes files that don't already exist, unless you
  pass `--force` — which clobbers a customized `index.html` back to the
  stock redirect (see the `--force` warning on
  [Publishing to GitHub Pages](Publishing-to-GitHub-Pages.md#quick-start)).
- `wikidown export-html` layers wiki-root files over its embedded defaults
  — a wiki-root `index.html` always wins over the built-in one when
  present, and it doesn't need `wikidown pages` to have been run first.

## The `{{HOME}}` / `{{TITLE}}` placeholders

The stock `index.html` contains two placeholders:

```html
<meta http-equiv="refresh" content="0; url={{ '{{HOME}}' | relative_url }}">
<title>{{ site.title }}</title>
```

`{{TITLE}}` and `{{HOME}}` are **plain literal text**, not Liquid — they're
substituted with a raw string replace over the file's text, done
independently in two places:

- `wikidown pages` (`PagesCommand`) substitutes them once, at scaffold
  time, and writes the already-substituted text into the committed
  `index.html`. Real Jekyll — GitHub's own builder — never sees the
  placeholders at all; by the time it builds, they're already gone.
- `wikidown export-html` (`HtmlExporter`) substitutes them fresh on every
  export, so it keeps a hand-scaffolded `index.html` that still contains
  the placeholders self-updating even without rerunning `wikidown pages`.

`{{HOME}}` resolves to the wiki's `/Home` page if one exists, else the
first top-level page, else `/Home.html` as a last resort — always with a
`.html` suffix, since it's the concrete redirect target of a real generated
file. (Links *within* rendered wiki pages — the sidebar, breadcrumbs — are
different: those resolve the wiki's Home page to `/` instead of
`/Home.html`, so a fully custom homepage that serves Home's content at the
literal site root doesn't break them. The redirect stub itself doesn't need
that, because `Home.html` always exists as its own file alongside
`index.html`.)

### Why the placeholder sits inside a Liquid string

Look closely at the source template
(`src/Wikidown.Html/Theme/index.html`):

```liquid
{{ '{{HOME}}' | relative_url }}
```

`{{HOME}}` is written inside a Liquid *string literal*, itself wrapped in
the `relative_url` filter. The raw-text replace happens **before** any
Liquid or Fluid parsing runs, so it substitutes the literal characters
`{{HOME}}` inside the quotes — turning `'{{HOME}}'` into e.g.
`'/Home.html'` — leaving well-formed Liquid that only gets evaluated
afterward. That's how baseurl-prefixing still works on both engines:
`relative_url` runs at each engine's own render time (real Jekyll's own
filter on the GitHub Pages path, a custom-registered Fluid filter using
`--base-url` for `export-html`) — the placeholder substitution only ever
supplies the unprefixed link path, never the final href.

Practical takeaway if you're customizing `index.html`: reuse
`{{ '{{HOME}}' | relative_url }}` somewhere in your markup if you want your
"go to the docs" link to auto-track the wiki's actual home page instead of
a URL you'd have to keep in sync by hand. It's optional, not required —
wikidown.org's own page skips it and hardcodes `href="Home.html"` in its
nav bar instead, which is fine too.

## Previewing changes

`wikidown export-html --output public --clean` plus any static file
server gives instant local preview of either kind of edit, with no Jekyll,
Ruby, or GitHub Pages deploy required — see [Publishing to GitHub
Pages](Publishing-to-GitHub-Pages.md#any-other-host-export-html) for the
full workflow.

See [CLI](../CLI.md) for the full option reference, and
[Publishing to GitLab Pages](Publishing-to-GitLab-Pages.md) for a worked
example of `export-html` in CI — the theme-customization mechanics here
apply to any `export-html`-based host, not just GitHub.
