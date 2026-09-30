using Markdig;
using Markdig.Syntax;

namespace Wikidown.Core;

// A page's display title is its first "# Heading", the same title Jekyll's
// jekyll-titles-from-headings and export-html show on the page itself. The
// file name is only the URL; it's the fallback when a page has no heading.
public static class PageTitle
{
    // Inline spans are only filled in with precise source locations on.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePreciseSourceLocation().Build();

    public static string? FromMarkdown(string markdown)
    {
        var heading = Markdown.Parse(markdown, Pipeline).Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        if (heading?.Inline is null) return null;

        var start = heading.Inline.FirstChild?.Span.Start ?? -1;
        var end = heading.Inline.LastChild?.Span.End ?? -1;
        if (start < 0 || end < start) return null;

        var title = Markdown.ToPlainText(markdown[start..(end + 1)]).Trim();
        return title.Length == 0 ? null : title;
    }

    public static string For(PagePath page, string markdown) => FromMarkdown(markdown) ?? page.Name.Title;
}
