using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Wikidown.Core.PdfExport;

public static class MarkdownIrBuilder
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePipeTables().UseAutoIdentifiers().Build();

    public static IReadOnlyList<IrBlock> Build(
        string markdown, PagePath page, WikiRepository repo, bool allowHtmlSkip = false) =>
        Build(markdown, page, repo, allowHtmlSkip, out _);

    public static IReadOnlyList<IrBlock> Build(
        string markdown, PagePath page, WikiRepository repo, bool allowHtmlSkip,
        out IReadOnlyList<PdfExportWarning> warnings)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var ctx = new BuildContext(page, repo, allowHtmlSkip, new List<PdfExportWarning>());
        var blocks = BuildBlocks(document, ctx);
        warnings = ctx.Warnings;
        return blocks;
    }

    private sealed record BuildContext(PagePath Page, WikiRepository Repo, bool AllowHtmlSkip, List<PdfExportWarning> Warnings);

    private static IReadOnlyList<IrBlock> BuildBlocks(ContainerBlock container, BuildContext ctx)
    {
        var result = new List<IrBlock>();
        foreach (var block in container)
            result.Add(BuildBlock(block, ctx));
        return result;
    }

    private static IrBlock BuildBlock(Block block, BuildContext ctx) => block switch
    {
        HeadingBlock h => BuildHeading(h, ctx),
        Table t => BuildTable(t, ctx),
        ListBlock l => BuildList(l, ctx),
        FencedCodeBlock fc => new IrCodeBlock(fc.Info, fc.Lines.ToString()),
        CodeBlock c => new IrCodeBlock(null, c.Lines.ToString()),
        ThematicBreakBlock => new IrThematicBreak(),
        ParagraphBlock p => BuildParagraph(p, ctx),
        HtmlBlock html => BuildHtml(html, ctx),
        _ => throw new NotSupportedException(
            $"{ctx.Page.ToLinkPath()}: unsupported markdown block '{block.GetType().Name}'"),
    };

    private static IrHeading BuildHeading(HeadingBlock h, BuildContext ctx)
    {
        var slug = h.GetAttributes().Id ?? h.Level + "-" + h.Line;
        var runs = BuildInlines(h.Inline, ctx);
        return new IrHeading(h.Level, runs, PdfAnchors.HeadingAnchor(ctx.Page, slug));
    }

    private static IrBlock BuildParagraph(ParagraphBlock p, BuildContext ctx)
    {
        if (p.Inline is { } inline && SingleImage(inline) is { } img)
            return BuildImageBlock(img, ctx);
        return new IrParagraph(BuildInlines(p.Inline, ctx));
    }

    private static LinkInline? SingleImage(ContainerInline inline)
    {
        var first = inline.FirstChild;
        return first is LinkInline { IsImage: true } img && ReferenceEquals(first, inline.LastChild) ? img : null;
    }

    private static IrImage BuildImageBlock(LinkInline img, BuildContext ctx)
    {
        var alt = ExtractPlainText(img);
        var target = img.Url ?? string.Empty;
        return new IrImage(alt, target, ResolveImagePath(ctx, target));
    }

    // Null covers two very different cases: an external image (not a wiki
    // authoring mistake, just unsupported for embedding — no warning) and a
    // relative path that doesn't resolve to a real file (a broken
    // reference — recorded so the CLI can report it and reflect it in the
    // exit code, same as check-links does for broken body links).
    private static string? ResolveImagePath(BuildContext ctx, string target)
    {
        if (LinkChecker.IsExternal(target)) return null;
        var withoutFragment = target.Split('#')[0];
        if (withoutFragment.Length == 0) return null;
        var full = LinkChecker.ResolveFullPath(ctx.Repo, ctx.Page, withoutFragment);
        if (File.Exists(full)) return full;
        ctx.Warnings.Add(new PdfExportWarning(ctx.Page, target));
        return null;
    }

    private static IrBlock BuildHtml(HtmlBlock html, BuildContext ctx) =>
        ctx.AllowHtmlSkip
            ? new IrHtmlPlaceholder()
            : throw new NotSupportedException(
                $"{ctx.Page.ToLinkPath()}: raw HTML block is not supported " +
                "(pass --allow-html-skip to degrade instead of failing)");

    private static IrList BuildList(ListBlock list, BuildContext ctx)
    {
        var items = new List<IrListItem>();
        foreach (var itemBlock in list)
        {
            IReadOnlyList<IrRun> runs = Array.Empty<IrRun>();
            IrList? nested = null;
            foreach (var child in (ListItemBlock)itemBlock)
            {
                switch (child)
                {
                    case ParagraphBlock p when runs.Count == 0:
                        runs = BuildInlines(p.Inline, ctx);
                        break;
                    case ListBlock nestedList:
                        nested = BuildList(nestedList, ctx);
                        break;
                }
            }
            items.Add(new IrListItem(runs, nested));
        }
        return new IrList(list.IsOrdered, items);
    }

    private static IrTable BuildTable(Table table, BuildContext ctx)
    {
        var rows = table.OfType<TableRow>().ToList();
        if (rows.Count == 0)
            return new IrTable(Array.Empty<IReadOnlyList<IrRun>>(), Array.Empty<IReadOnlyList<IReadOnlyList<IrRun>>>());

        var header = rows[0].OfType<TableCell>().Select(c => CellRuns(c, ctx)).ToList();
        var dataRows = rows.Skip(1)
            .Select(r => (IReadOnlyList<IReadOnlyList<IrRun>>)r.OfType<TableCell>().Select(c => CellRuns(c, ctx)).ToList())
            .ToList();
        return new IrTable(header, dataRows);
    }

    private static IReadOnlyList<IrRun> CellRuns(TableCell cell, BuildContext ctx)
    {
        foreach (var block in cell)
            if (block is ParagraphBlock p)
                return BuildInlines(p.Inline, ctx);
        return Array.Empty<IrRun>();
    }

    private static IReadOnlyList<IrRun> BuildInlines(ContainerInline? inline, BuildContext ctx) =>
        inline is null ? Array.Empty<IrRun>() : BuildInlineRuns(inline, ctx, bold: false, italic: false);

    private static IReadOnlyList<IrRun> BuildInlineRuns(ContainerInline container, BuildContext ctx, bool bold, bool italic)
    {
        var result = new List<IrRun>();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    result.Add(new IrText(lit.Content.ToString(), bold, italic));
                    break;

                case CodeInline code:
                    result.Add(new IrText(code.Content, bold, italic, Code: true));
                    break;

                case EmphasisInline em:
                {
                    var isBold = em.DelimiterCount >= 2;
                    var childBold = bold || isBold;
                    var childItalic = italic || !isBold;
                    result.AddRange(BuildInlineRuns(em, ctx, childBold, childItalic));
                    break;
                }

                case LinkInline { IsImage: true } img:
                {
                    var alt = ExtractPlainText(img);
                    var target = img.Url ?? string.Empty;
                    result.Add(new IrInlineImage(alt, target, ResolveImagePath(ctx, target)));
                    break;
                }

                case LinkInline link:
                {
                    var content = BuildInlineRuns(link, ctx, bold, italic);
                    var target = link.Url ?? string.Empty;
                    if (LinkChecker.IsExternal(target))
                    {
                        result.Add(new IrExternalLink(content, target));
                    }
                    else
                    {
                        var anchor = ResolveInternalAnchor(ctx.Repo, ctx.Page, target);
                        if (anchor is not null) result.Add(new IrLink(content, anchor));
                        else result.AddRange(content); // broken link: keep the text, drop the jump
                    }
                    break;
                }

                case AutolinkInline auto:
                    result.Add(new IrExternalLink(new IrRun[] { new IrText(auto.Url) }, auto.Url));
                    break;

                case LineBreakInline:
                    result.Add(new IrText(" ", bold, italic));
                    break;

                case HtmlEntityInline entity:
                    result.Add(new IrText(entity.Transcoded.ToString(), bold, italic));
                    break;

                case HtmlInline:
                    if (!ctx.AllowHtmlSkip)
                        throw new NotSupportedException(
                            $"{ctx.Page.ToLinkPath()}: raw inline HTML is not supported " +
                            "(pass --allow-html-skip to degrade instead of failing)");
                    result.Add(new IrText("[html]", bold, italic));
                    break;

                default:
                    throw new NotSupportedException(
                        $"{ctx.Page.ToLinkPath()}: unsupported markdown inline '{inline.GetType().Name}'");
            }
        }
        return result;
    }

    private static string ExtractPlainText(ContainerInline container)
    {
        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit: sb.Append(lit.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case ContainerInline nested: sb.Append(ExtractPlainText(nested)); break;
            }
        }
        return sb.ToString();
    }

    // Resolves a link target (relative .md link, legacy absolute /Title/Path
    // link, or same-page #fragment) to a PdfAnchors id, or null if it's
    // broken — mirrors LinkChecker.Classify's target handling.
    private static string? ResolveInternalAnchor(WikiRepository repo, PagePath page, string target)
    {
        var parts = target.Split('#', 2);
        var withoutFragment = parts[0];
        var fragment = parts.Length > 1 ? parts[1] : null;

        PagePath? targetPage;
        if (withoutFragment.Length == 0)
        {
            targetPage = page;
        }
        else if (withoutFragment.StartsWith('/'))
        {
            var parsed = PagePath.Parse(withoutFragment);
            targetPage = repo.Exists(parsed) ? parsed : null;
        }
        else
        {
            var full = LinkChecker.ResolveFullPath(repo, page, withoutFragment);
            targetPage = File.Exists(full) ? FilePathToPagePath(repo, full) : null;
        }

        if (targetPage is null) return null;
        return fragment is null
            ? PdfAnchors.PageAnchor(targetPage)
            : PdfAnchors.HeadingAnchor(targetPage, fragment);
    }

    private static PagePath? FilePathToPagePath(WikiRepository repo, string fullPath)
    {
        if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return null;
        var relative = Path.GetRelativePath(repo.RootPath, fullPath);
        if (relative.StartsWith("..")) return null;

        var segments = relative[..^3]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Select(PageName.FromFileBase)
            .ToList();
        return segments.Count == 0 ? null : new PagePath(segments);
    }
}
