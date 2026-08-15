using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Wikidown.Core;

namespace Wikidown.Web.Services;

// Assembles the whole wiki into one print-friendly HTML document for the
// browser's native print-to-PDF. Not a substitute for the CLI's export-pdf
// (no real embedded outline/bookmarks, no MigraDoc typography) — MigraDoc
// can't run in Blazor WASM, so this is the quick client-side path instead.
public static partial class PdfExportHtmlBuilder
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePipeTables().UseAutoIdentifiers().Build();

    [GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex LinkRegex();

    // DFS over the .order-respecting NavNode tree, yielding page paths in
    // the same order the sidebar tree shows them.
    public static IEnumerable<PagePath> FlattenInNavOrder(IReadOnlyList<NavNode> tree)
    {
        foreach (var node in tree)
        {
            if (node.IsPage) yield return node.Path;
            foreach (var descendant in FlattenInNavOrder(node.Children))
                yield return descendant;
        }
    }

    public static string BuildDocument(
        string title,
        IReadOnlyList<NavNode> tree,
        IReadOnlyList<(PagePath Path, string Markdown)> pages)
    {
        var knownPages = pages.Select(p => p.Path).ToList();
        var byPath = pages.ToDictionary(p => p.Path, p => p.Markdown);

        var html = new StringBuilder();
        html.Append("<style>");
        html.Append(Styles);
        html.Append("</style>");

        html.Append("<h1>").Append(WebUtility.HtmlEncode(title)).Append("</h1>");
        html.Append("<nav class=\"wd-pdf-toc\"><h2>Contents</h2>");
        AppendTocList(html, tree, depth: 0);
        html.Append("</nav>");

        foreach (var path in FlattenInNavOrder(tree))
        {
            if (!byPath.TryGetValue(path, out var markdown)) continue;

            var stripped = Breadcrumb.Strip(markdown);
            var rewritten = RewriteInternalLinks(stripped, path, knownPages);
            var body = Markdown.ToHtml(rewritten, Pipeline);

            html.Append("<section id=\"page-").Append(AnchorId(path)).Append("\">");
            html.Append(body);
            html.Append("</section>");
        }

        return html.ToString();
    }

    private static void AppendTocList(StringBuilder html, IReadOnlyList<NavNode> nodes, int depth)
    {
        if (nodes.Count == 0) return;
        html.Append("<ul style=\"margin-left:").Append(depth * 1).Append("em\">");
        foreach (var node in nodes)
        {
            html.Append("<li>");
            if (node.IsPage)
            {
                html.Append("<a href=\"#page-").Append(AnchorId(node.Path)).Append("\">")
                    .Append(WebUtility.HtmlEncode(node.Title)).Append("</a>");
            }
            else
            {
                html.Append(WebUtility.HtmlEncode(node.Title));
            }
            AppendTocList(html, node.Children, depth + 1);
            html.Append("</li>");
        }
        html.Append("</ul>");
    }

    private static string AnchorId(PagePath path) =>
        Uri.EscapeDataString(path.ToLinkPath());

    public static string RewriteInternalLinks(
        string markdown, PagePath page, IReadOnlyList<PagePath> knownPages)
    {
        return LinkRegex().Replace(markdown, match =>
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith('#'))
            {
                return match.Value;
            }

            var resolved = Resolve(target, page);
            if (resolved is null) return match.Value;

            var known = knownPages.FirstOrDefault(p =>
                string.Equals(p.ToLinkPath(), resolved.ToLinkPath(), StringComparison.OrdinalIgnoreCase));
            if (known is null) return match.Value;

            var replacement = $"#page-{AnchorId(known)}";
            var linkStart = match.Value.IndexOf('(') + 1;
            return match.Value[..linkStart] + replacement + match.Value[(linkStart + target.Length)..];
        });
    }

    private static PagePath? Resolve(string target, PagePath page)
    {
        var withoutFragment = target.Split('#', 2)[0];
        if (withoutFragment.Length == 0) return null;

        if (withoutFragment.StartsWith('/'))
            return PagePath.Parse(withoutFragment.EndsWith(".md")
                ? withoutFragment[..^3]
                : withoutFragment);

        var current = page.Parent;
        var segments = withoutFragment.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                if (current.IsRoot) return null;
                current = current.Parent;
            }
            else
            {
                var name = segment.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    ? segment[..^3]
                    : segment;
                current = current.Append(PageName.FromFileBase(name));
            }
        }
        return current;
    }

    private const string Styles = """
        @media print {
          .mud-appbar, .mud-drawer { display: none !important; }
          section { page-break-before: always; }
          section:first-of-type { page-break-before: avoid; }
        }
        .wd-pdf-toc ul { list-style: none; padding-left: 0; }
        .wd-pdf-toc a { text-decoration: none; }
        section { max-width: 50em; }
        """;
}
