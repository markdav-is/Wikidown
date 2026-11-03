[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Clickable Maps <!-- wikidown:breadcrumb -->

# Clickable Maps with Inline SVG

Make any image clickable, with areas that link to wiki pages, using a plain inline `<svg>`. You don't need JavaScript, plugins or a special Wikidown feature.

## What it is and when to use it

Wikidown passes raw HTML and inline SVG in markdown straight through. Since 0.12, `export-html` also rewrites relative `.md` hrefs to `.html` inside them (see [Format § Raw HTML and inline SVG](Format.md#raw-html-and-inline-svg)). So an `<svg>` that draws an image and wraps shapes in `<a href="Page.md">` links works as an image map, both in the wiki on GitHub and on the exported site.

Use it for any image whose areas should link to pages:

- **Maps.** A continent whose regions link to region pages, and region pages that zoom in with pins linking to place pages.
- **Diagrams.** Architecture boxes that link to the component's page.
- **Floor plans.** Rooms that link to room or equipment pages.
- **Org charts.** People or teams that link to their pages.

This pattern already runs in production on a Wikidown-built site, as a continent map with zoomable region pages.

## Minimal example

Put the image in `.attachments` like any other wiki image, then add the SVG to the page. This example assumes the page is one folder below the wiki root, so the image path starts with `../`:

```html
<svg class="map" viewBox="0 0 1000 600" xmlns="http://www.w3.org/2000/svg"
     role="img" aria-label="Map of the region">
  <image href="../.attachments/region-map.png" width="1000" height="600"/>
  <a href="Places/North-Town.md" aria-label="North Town">
    <path class="area" d="M120,80 L380,60 L400,260 L140,280 Z"/>
  </a>
  <a href="Places/Harbor.md" aria-label="Harbor">
    <circle class="pin" cx="720" cy="430" r="14"/>
    <circle class="hit" cx="720" cy="430" r="60"/>
  </a>
</svg>
```

Then add the matching CSS to the site's stylesheet, `assets/wikidown.css` (see [Customizing the Theme](Customizing-the-Theme.md)):

```css
.content svg.map { width: 100%; height: auto; display: block; }
.map .area { fill: #fff; fill-opacity: 0; stroke: none; cursor: pointer; }
.map a:hover .area, .map a:focus .area { fill-opacity: 0.2; }
.map .pin { fill: #fff; stroke: #222; stroke-width: 4; }
.map a:hover .pin { fill: #f5c542; }
.map .hit { fill: transparent; }   /* big invisible touch target for phones */
```

Areas are invisible until you hover over them or focus them with the keyboard. Pins are always visible.

## Tips

- **Set `viewBox` to the image's size in pixels.** With `viewBox="0 0 1000 600"` for a 1000×600 image, shape coordinates are just pixel coordinates on the image. Read them from the cursor position in any image editor.
- **Write hrefs like normal wiki links.** Use relative `.md` paths, resolved from the page's own folder. `export-html` turns them into `.html`, and `check-links` and `wikidown move` check and rewrite them like markdown links.
- **Keep `href` on the tag's first line.** `check-links` and `move` work line by line, so put `href` on the same line as the opening `<a` or `<image`.
- **Give every link an `aria-label`.** Screen readers announce it, and keyboard users can reach each area with Tab and open it with Enter.
- **Make tap targets big enough on phones.** Keep the visible pin small and add a large transparent `circle.hit` (or a padded shape) in the same `<a>`, so the area a finger can tap is bigger than the pin.
- **Use CSS classes for optional states.** For example, hide or grey out areas whose pages aren't ready yet, or set `pointer-events: none` on pins in an overview map so only the regions can be tapped.
- **Don't leave blank lines inside the `<svg>`.** In markdown, a blank line can end a raw HTML block. `export-html` keeps an `<svg>` block together anyway, but GitHub doesn't.
- **Reference the image from `.attachments`** like any other wiki image, with a relative path adjusted for the page's depth.

## Limitations

- **Sanitizing renderers strip it.** A theme or renderer that removes raw HTML removes the whole map.
- **GitHub's preview is partial.** GitHub's own markdown view may show the SVG without rendering the `<image>` or the links. The exported site is the real target.
