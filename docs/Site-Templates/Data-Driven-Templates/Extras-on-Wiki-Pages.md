[Home](../../Home.md) / [Site Templates](../../Site-Templates.md) / [Data Driven Templates](../Data-Driven-Templates.md) / Extras on Wiki Pages <!-- wikidown:breadcrumb -->

# Extras on Wiki Pages

**Goal:** add a widget (a signup form, the pricing cards, an event map) to
one or two specific wiki pages, without front matter in the pages and
without a separate layout for each.

Wiki pages are plain markdown and can't call includes. The layout can,
and it knows which page it's rendering.

## Template

In `_layouts/wikidown.html`, around `{{ content }}`:

```liquid
<main class="page{% if page.path == 'Map.md' %} map-page{% endif %}" id="content">
  {{ content }}
  {% if page.path == "Map.md" %}{% include hop-map.html %}{% endif %}
  {% if page.path == "Membership.md" %}{% include membership.html %}{% endif %}
  {% if page.path == "Mailing-List.md" %}{% include newsletter.html %}{% endif %}
</main>
```

The page's own text renders first, then the widget.
`_includes/hop-map.html` reads this month's data file:

```liquid
{% assign hop = site.data.hops[site.data.site.currentHop] %}
<div class="hop-map">
  {% if hop %}
    <p class="hop-when">{{ hop.date | date: "%A, %B %-d, %Y" }}</p>
    {% if hop.map and hop.map != "" %}
      <img src="{{ hop.map | relative_url }}" alt="{{ hop.date | date: '%B %Y' }} Art Hop map">
    {% endif %}
  {% else %}
    <p>No Art Hop is scheduled right now. Check back soon.</p>
  {% endif %}
</div>
```

## When to use a layout instead

Testing `page.path` suits a handful of one-off pages. When a whole folder
needs different chrome, give it its own layout in `_config.yml`:

```yaml
wikidown:
  layouts:
    /Venues/*: venue
```

`_layouts/venue.html` can reuse the main layout by naming it in front
matter, and add only what's different:

```liquid
---
layout: wikidown
---
<article class="venue">{{ content }}</article>
<p><a href="{{ '/Venues.html' | relative_url }}">All venues</a></p>
```

## Notes

- `page.path` is the file path from the wiki root with `.md`
  (`Venues/Pinebox.md`); `page.url` is the served URL. Either works for
  matching.
- If a page is renamed with `wikidown move`, update the path here too.
  `move` rewrites links in pages, not in templates.
