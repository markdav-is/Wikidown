using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Wikidown.Html;

public sealed record RenderedPage(string Title, string Html);

// Markdown -> HTML for one wiki page, doing the two things the Jekyll path
// gets from plugins: relative `.md` links become `.html`
// (jekyll-relative-links) and the title comes from the first `# Heading`
// (jekyll-titles-from-headings). The heading stays in the body, as it does
// there with strip_title: false.
public static class MarkdownPageRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    public static RenderedPage Render(string markdown, string fallbackTitle)
    {
        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage || link.Url is null) continue;
            link.Url = RewriteMarkdownLink(link.Url);
        }

        var title = document.Descendants<HeadingBlock>()
            .Where(h => h.Level == 1)
            .Select(InlineText)
            .FirstOrDefault(t => t.Length > 0) ?? fallbackTitle;

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return new RenderedPage(title, writer.ToString());
    }

    // Only relative links to markdown files are touched, same as
    // jekyll-relative-links: absolute URLs, anchors, and site-absolute
    // paths pass through unchanged.
    public static string RewriteMarkdownLink(string url)
    {
        if (url.Length == 0 || url[0] is '#' or '/' || url.Contains("://", StringComparison.Ordinal)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return url;

        var hash = url.IndexOf('#');
        var path = hash < 0 ? url : url[..hash];
        var fragment = hash < 0 ? "" : url[hash..];
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return url;
        return path[..^3] + ".html" + fragment;
    }

    private static string InlineText(HeadingBlock heading)
    {
        if (heading.Inline is null) return "";
        return string.Concat(heading.Inline.Descendants<LiteralInline>().Select(l => l.Content.ToString())).Trim();
    }
}
