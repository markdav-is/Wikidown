[Home](../../Home.md) / [Site Templates](../../Site-Templates.md) / [Data Driven Templates](../Data-Driven-Templates.md) / Featured Story <!-- wikidown:breadcrumb -->

# Featured Story

**Goal:** a landing-page teaser for one wiki page, such as this month's
featured artist, a case study, or the latest announcement: its title, the
first 70 words, and a "read more" link. Changing the feature is a
one-field data edit.

## Data

The Art Hop's monthly file names the feature page, or leaves it empty:

```json
{
  "date": "2026-11-07",
  "feature": "Features/Wheelhaus-Arts.md",
  "venues": []
}
```

## Template

```liquid
{% assign hop = site.data.hops[site.data.site.currentHop] %}
{% if hop.feature and hop.feature != "" %}
  {% assign fp = site.pages | where: "path", hop.feature | first %}
  {% if fp %}
  <section class="feature">
    <h2>{{ fp | wiki_title }}</h2>
    {% assign text = fp | wiki_body: "strip_title" %}
    <p>{{ text | strip_html | normalize_whitespace | truncatewords: 70 }}</p>
    <p><a class="btn" href="{{ fp.url | relative_url }}">Read the feature</a></p>
  </section>
  {% endif %}
{% endif %}
```

## Notes

- `strip_html | normalize_whitespace | truncatewords: 70` turns the
  rendered page into a plain-text excerpt. Without `strip_html`,
  truncating could cut an HTML tag in half.
- To show a whole section instead (a "highlights" gallery, say), output
  `wiki_body` directly without the excerpt filters. The Art Hop home page
  does this for its `Highlights` page.
- Without a data file, pick the first page in a folder by its position in
  `.order`. `site.pages` is in `.order` order, so if new features go at
  the top of `Features/.order`:

  ```liquid
  {% assign fp = site.pages | where: "dir", "/Features/" | first %}
  ```
