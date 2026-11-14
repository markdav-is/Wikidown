[Home](../../Home.md) / [Site Templates](../../Site-Templates.md) / [Data Driven Templates](../Data-Driven-Templates.md) / Pricing Tiers <!-- wikidown:breadcrumb -->

# Pricing Tiers

**Goal:** a set of plan or membership cards, plus a "from $3 a month"
line that always quotes the cheapest tier, both driven by one list.

## Data

In `_data/site.json` (or a `_data/pricing.yml` of its own):

```json
{
  "membership": {
    "tiers": [
      { "name": "Founding Fan", "price": 3, "url": "/Membership.html" },
      { "name": "Big Believer", "price": 5, "url": "/Membership.html" },
      { "name": "Super Supporter", "price": 10, "url": "/Membership.html" },
      { "name": "Howie's Hero", "price": 40, "url": "https://example.org/join" }
    ]
  }
}
```

The same list in YAML works the same way, as long as prices are unquoted
so they load as numbers:

```yaml
tiers:
  - name: Founding Fan
    price: 3
  - name: Big Believer
    price: 5
```

## Template

The cards, `_includes/membership.html`:

```liquid
<div class="tiers">
  {% for t in site.data.site.membership.tiers %}
  <a class="tier" href="{{ t.url }}"{% if t.url contains '://' %} target="_blank" rel="noopener"{% endif %}>
    <span class="name">{{ t.name }}</span>
    <span class="price">${{ t.price }} / month</span>
  </a>
  {% endfor %}
</div>
```

The call to action on the landing page:

```liquid
{% assign minprice = site.data.site.membership.tiers | map: "price" | sort | first %}
<a class="btn" href="{{ '/Membership.html' | relative_url }}">
  Become a member for as little as ${{ minprice }} a month
</a>
```

Show the tiers cheapest first, whatever order the file has them in:

```liquid
{% assign tiers = site.data.site.membership.tiers | sort: "price" %}
```

## Notes

- `sort` compares numbers as numbers in `export-html`, so 3, 5, 10, 40
  stay in that order. A price written as a string (`"10"`) sorts as text.
- Put the explanatory copy (what members get, how billing works) in the
  `Membership` wiki page and show it next to the cards with
  `{{ "Membership.md" | wiki_body: "strip_title" }}`.
- To show the cards at the bottom of the Membership page too, see
  [Extras on Wiki Pages](Extras-on-Wiki-Pages.md).
