[Home](../../Home.md) / [Site Templates](../../Site-Templates.md) / [Data Driven Templates](../Data-Driven-Templates.md) / Monthly Event Listing <!-- wikidown:breadcrumb -->

# Monthly Event Listing

**Goal:** a landing-page section listing this month's venues, each with
the details that change monthly (stop number, hours, a note) and the
description that doesn't (from the venue's own wiki page). Next month is a
new data file plus a one-line switch.

This is the core of the Art Hop home page.

## Content and data

One wiki page per venue holds its permanent description, written as
normal: `Venues/Pinebox.md`, `Venues/Caffe-Pacori.md`, and so on.

One data file per month lists who's taking part, by page path.
`_data/hops/2026-11.json`:

```json
{
  "date": "2026-11-07",
  "time": "Hours vary by venue",
  "venues": [
    { "stop": 4, "page": "Venues/Pinebox.md",
      "hours": "10am to 1pm",
      "note": "Come peruse the latest FRIPP jewelry." },
    { "stop": 5, "page": "Venues/Caffe-Pacori.md",
      "address": "255 Wallis Street", "hours": "9am to 4pm",
      "note": "Music: The Name of the Sea (11am)." }
  ]
}
```

`_data/site.json` says which month is current:

```json
{ "currentHop": "2026-11" }
```

Files in a subfolder nest, so this one is `site.data.hops["2026-11"]`.

## Template

`_includes/venue-list.html`:

```liquid
{% assign cfg = site.data.site %}
{% assign hop = site.data.hops[cfg.currentHop] %}
<div class="venues">
{% for entry in hop.venues %}
  {% assign vp = site.pages | where: "path", entry.page | first %}
  {% if vp %}
  <article class="venue">
    <h3>
      {% if entry.stop %}<span class="stop">{{ entry.stop }}</span>{% endif %}
      <a href="{{ vp.url | relative_url }}">{{ vp | wiki_title }}</a>
    </h3>
    {% if entry.address and entry.address != "" %}<p class="meta">{{ entry.address }}</p>{% endif %}
    {% if entry.hours and entry.hours != "" %}<p class="meta">Open {{ entry.hours }}</p>{% endif %}
    {% if entry.note %}<p class="note">{{ entry.note }}</p>{% endif %}
    {{ vp | wiki_body: "strip_title" }}
  </article>
  {% endif %}
{% endfor %}
</div>
```

And in `_layouts/landing.html`:

```liquid
{% assign hop = site.data.hops[site.data.site.currentHop] %}
{% if hop %}
<section>
  <h2>{{ hop.date | date: "%B" }}'s participating venues</h2>
  <p>{{ hop.date | date: "%A, %B %-d, %Y" }} · {{ hop.time }}</p>
  {% include venue-list.html %}
</section>
{% endif %}
```

## Notes

- `where: "path", entry.page | first` finds the page object, which gives
  the URL; `wiki_title` and `wiki_body` take the same object.
- `wiki_body` makes the venue page's relative links and images
  site-absolute, so they work on the home page even though the venue page
  lives in `Venues/`.
- `{% if vp %}` skips an entry whose page was renamed or deleted, instead
  of rendering an empty card. A misspelled path given straight to
  `wiki_body` or `wiki_title` prints an export warning.
- `{% if hop %}` hides the section when `currentHop` names a month with no
  file.
- Old months stay in `_data/hops/` as an archive. A "past events" page can
  loop over them the same way.

Live: "November's participating venues" on
[warehousedistrictarthop.com](https://warehousedistrictarthop.com).
