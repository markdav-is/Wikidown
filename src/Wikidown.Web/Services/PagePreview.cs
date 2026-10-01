using AngleSharp.Dom;
using Ganss.Xss;
using Microsoft.AspNetCore.Components;
using Wikidown.Core;
using Wikidown.Html;

namespace Wikidown.Web.Services;

/// <summary>
/// Renders a page the way the published site does (export-html's Markdig
/// pipeline, styled by the theme's page rules), then sanitizes it: pages come
/// from the repo, and anything that could run script here could read the
/// token this app holds. Images become cached data URLs and wiki links become
/// in-app routes.
/// </summary>
public sealed class PagePreview(PageImageResolver images)
{
    public MarkupString Render(WikiConnection conn, PagePath page, string markdown)
    {
        var sanitizer = new HtmlSanitizer();
        foreach (var attribute in new[] { "id", "class", "title", "target", "rel", "width", "height", "align", "open" })
            sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element) return;
            if (element.LocalName == "img" && element.GetAttribute("src") is { } src
                && images.DataUrlFor(conn, page, src) is { } data)
                element.SetAttribute("src", data);
            else if (element.LocalName == "a" && element.GetAttribute("href") is { } href)
                RewriteLink(element, page, href);
        };
        return new MarkupString(sanitizer.Sanitize(MarkBreadcrumb(MarkdownPageRenderer.ToHtml(markdown))));
    }

    // The theme's layout script does this on the site: the first paragraph,
    // when it carries the breadcrumb marker, gets the "breadcrumb" class.
    private static string MarkBreadcrumb(string html)
    {
        var end = html.IndexOf("</p>", StringComparison.Ordinal);
        return html.StartsWith("<p>", StringComparison.Ordinal) && end > 0
            && html.AsSpan(0, end).Contains(Breadcrumb.Marker, StringComparison.Ordinal)
                ? "<p class=\"breadcrumb\">" + html[3..]
                : html;
    }

    private static void RewriteLink(IElement link, PagePath page, string href)
    {
        if (href.StartsWith('#'))
        {
            link.SetAttribute("href", Route(page.ToLinkPath()) + href);
            return;
        }
        if (LinkChecker.IsExternal(href))
        {
            link.SetAttribute("target", "_blank");
            link.SetAttribute("rel", "noopener noreferrer");
            return;
        }

        var hash = href.IndexOf('#');
        var fragment = hash < 0 ? "" : href[hash..];
        var target = LinkChecker.ResolveDocsRelativePath(page, href);
        if (target is null) return;
        if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            link.SetAttribute("href", Route("/" + target[..^3]) + fragment);
        else if (!Path.HasExtension(target))
            link.SetAttribute("href", Route("/" + target) + fragment);
    }

    private static string Route(string linkPath) =>
        "/browse/" + string.Join('/', linkPath.Trim('/').Split('/').Select(Uri.EscapeDataString));
}
