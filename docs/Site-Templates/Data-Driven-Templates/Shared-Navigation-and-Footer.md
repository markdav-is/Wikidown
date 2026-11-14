[Home](../../Home.md) / [Site Templates](../../Site-Templates.md) / [Data Driven Templates](../Data-Driven-Templates.md) / Shared Navigation and Footer <!-- wikidown:breadcrumb -->

# Shared Navigation and Footer

**Goal:** one top nav and one footer on every page, landing page included,
with the links and contact details in a data file instead of hard-coded in
each layout.

The stock wiki theme builds its sidebar from `.order`. A marketing site
usually wants a short, hand-picked menu instead, often with links that
aren't wiki pages.

## Data

`_data/site.json`:

```json
{
  "email": "hello@example.org",
  "social": {
    "instagram": "https://www.instagram.com/example/",
    "facebook": "https://www.facebook.com/example/"
  },
  "nav": [
    { "title": "Home", "url": "/" },
    { "title": "Map", "url": "/Map.html" },
    { "title": "Venues", "url": "/Venues.html" },
    { "title": "Become a Member", "url": "/Membership.html" },
    { "title": "About", "url": "/About.html" }
  ]
}
```

Use page URLs as the site serves them: `/` for the root page, otherwise
the page path with `.html`.

## Template

`_includes/nav.html`:

```liquid
{% assign cfg = site.data.site %}
<header class="topnav">
  <a class="brand" href="{{ '/' | relative_url }}">{{ site.title }}</a>
  <nav>
    {% for item in cfg.nav %}
      {% assign active = false %}
      {% if item.url == page.url %}
        {% assign active = true %}
      {% elsif item.url != "/" and page.url contains item.url %}
        {% assign active = true %}
      {% endif %}
      <a href="{{ item.url | relative_url }}"{% if active %} class="is-active" aria-current="page"{% endif %}>{{ item.title }}</a>
    {% endfor %}
  </nav>
</header>
```

`_includes/footer.html`:

```liquid
{% assign cfg = site.data.site %}
<footer>
  {% if cfg.social.instagram %}<a href="{{ cfg.social.instagram }}">Instagram</a>{% endif %}
  {% if cfg.social.facebook %}<a href="{{ cfg.social.facebook }}">Facebook</a>{% endif %}
  {% if cfg.email %}<a href="mailto:{{ cfg.email }}">{{ cfg.email }}</a>{% endif %}
</footer>
```

Then every layout (`landing.html`, `wikidown.html`, ...) has
`{% include nav.html %}` at the top of `<body>` and
`{% include footer.html %}` at the bottom.

## Notes

- `relative_url` adds the site's `baseurl`, so the same data works on a
  project site at `/my-repo/` and on a custom domain.
- The active-link test matches the page itself and, for every item except
  Home, any URL that contains the item's URL.
- `{% if %}` around each social link means an empty or missing value just
  drops the link; nothing breaks when the data is incomplete.
- To keep the `.order` sidebar as well, include the stock
  `nav-tree.html` with `site.data.navigation`; see
  [Customizing the Theme](../Customizing-the-Theme.md).

Live: the header and footer on every page of
[warehousedistrictarthop.com](https://warehousedistrictarthop.com).
