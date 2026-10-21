using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Wikidown.Core;

namespace Wikidown.Html;

public sealed record RenderedPage(string Title, string Html);

// Markdown -> HTML for one wiki page, doing the two things the Jekyll path
// gets from plugins: relative `.md` links become `.html`
// (jekyll-relative-links) and the title comes from the first `# Heading`
// (jekyll-titles-from-headings). The heading stays in the body, as it does
// there with strip_title: false.
public static partial class MarkdownPageRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    public static RenderedPage Render(string markdown, string fallbackTitle)
    {
        var document = Parse(markdown);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage || link.Url is null) continue;
            link.Url = RewriteMarkdownLink(link.Url);
        }

        // Raw HTML (an inline SVG map's <a> regions) isn't in the link tree.
        // Code is HTML-escaped by now, so only real tags match.
        var html = LinkChecker.HtmlHref().Replace(ToHtml(document), match =>
        {
            var href = match.Groups["href"];
            var offset = href.Index - match.Index;
            return match.Value[..offset] + RewriteMarkdownLink(href.Value) + match.Value[(offset + href.Length)..];
        });

        var title = PageTitle.FromMarkdown(markdown) ?? fallbackTitle;
        return new RenderedPage(title, html);
    }

    /// <summary>
    /// The page body exactly as the site renders it, but with links left as
    /// written, for hosts (the web editor) that map links themselves.
    /// </summary>
    public static string ToHtml(string markdown) => ToHtml(Parse(markdown));

    private static MarkdownDocument Parse(string markdown) =>
        Markdown.Parse(KeepSvgBlocksWhole(markdown), Pipeline);

    // CommonMark ends a raw HTML block at the first blank line, so an inline
    // SVG with one falls apart into paragraphs. Blank lines mean nothing in
    // SVG markup; drop them between a line-opening <svg and its </svg>.
    private static string KeepSvgBlocksWhole(string markdown)
    {
        if (markdown.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0) return markdown;

        var lines = markdown.Split('\n');
        var drop = new HashSet<int>();
        var prose = LinkChecker.ProseLines(lines).ToHashSet();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!prose.Contains(i) || !SvgOpen().IsMatch(lines[i])) continue;
            var close = Array.FindIndex(lines, i, l => l.Contains("</svg>", StringComparison.OrdinalIgnoreCase));
            if (close < 0) break;
            for (var j = i + 1; j < close; j++)
                if (string.IsNullOrWhiteSpace(lines[j])) drop.Add(j);
            i = close;
        }
        return drop.Count == 0 ? markdown : string.Join('\n', lines.Where((_, i) => !drop.Contains(i)));
    }

    [GeneratedRegex(@"^ {0,3}<svg[\s>]", RegexOptions.IgnoreCase)]
    private static partial Regex SvgOpen();

    private static string ToHtml(MarkdownDocument document)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return writer.ToString();
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
}
