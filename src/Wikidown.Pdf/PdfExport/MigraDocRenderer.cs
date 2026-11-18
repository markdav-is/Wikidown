using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using Wikidown.Core;
using Wikidown.Core.PdfExport;

namespace Wikidown.Pdf.PdfExport;

// The only file in the solution touching MigraDoc/PDFsharp types. Everything
// upstream (Wikidown.Core.PdfExport) is plain records with no PDF-library
// dependency, so this translation layer is the sole place that needs to
// change if the rendering engine ever does.
public static class MigraDocRenderer
{
    private static readonly object FontResolverLock = new();
    private static bool _fontResolverRegistered;

    private const string BodyFont = EmbeddedFontResolver.BodyFamily;
    private const string MonospaceFont = EmbeddedFontResolver.MonospaceFamily;
    private const string FallbackBodyFont = EmbeddedFontResolver.FallbackBodyFamily;
    private const string FallbackMonospaceFont = EmbeddedFontResolver.FallbackMonospaceFamily;
    private static readonly Color LinkColor = Color.FromRgb(0x05, 0x63, 0xC1);

    // Renders a whole wiki (or the subtree WikiPdfContent.BuildAll was
    // scoped to) into one PDF: an optional cover page, an optional in-doc
    // TOC page, then one section per page. Each page's own leading heading
    // is placed at the outline depth NavTree gave it, so the sidebar
    // bookmark panel mirrors the wiki's nav hierarchy rather than listing
    // every page as a flat top-level entry.
    public static void Render(PdfExportContent content, Stream output, PdfExportOptions options)
    {
        EnsureFontResolverRegistered();
        var document = NewDocument();

        var includeToc = options.IncludeToc && content.Nav.Count > 0;
        var cover = options.IncludeCover ? RenderCover(document, options.Title, compact: includeToc) : null;
        if (includeToc) RenderToc(cover ?? AddSection(document), content.Nav);

        var depths = ComputePageDepths(content.Nav);
        foreach (var page in content.Pages)
            RenderPage(document, page, depths.GetValueOrDefault(page.Path.ToLinkPath(), 1));

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(output);
    }

    // Convenience overload for tests exercising block-rendering behavior in
    // isolation: no cover, TOC only if there's a nav to show.
    public static void Render(PdfExportContent content, Stream output) =>
        Render(content, output, new PdfExportOptions(Title: "", IncludeCover: false, IncludeToc: true));

    // Kept for single-page tests: wraps one page
    // as a single-item PdfExportContent with no nav (so no TOC section).
    public static void Render(PageIr page, Stream output) =>
        Render(new PdfExportContent(new[] { page }, Array.Empty<NavNode>(), Array.Empty<PdfExportWarning>()), output);

    // A title alone leaves the cover page mostly blank, so when a TOC
    // follows the title moves up and the TOC starts on the same page.
    private static Section RenderCover(Document document, string title, bool compact)
    {
        var section = AddSection(document);

        var titleParagraph = section.AddParagraph();
        AddText(titleParagraph, title);
        titleParagraph.Format.Font.Size = 28;
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.Alignment = ParagraphAlignment.Center;
        titleParagraph.Format.SpaceBefore = Unit.FromCentimeter(compact ? 3 : 8);

        var subtitle = section.AddParagraph($"Generated {DateTime.Now:yyyy-MM-dd}");
        subtitle.Format.Alignment = ParagraphAlignment.Center;
        subtitle.Format.SpaceBefore = Unit.FromCentimeter(1);
        subtitle.Format.SpaceAfter = Unit.FromCentimeter(compact ? 1.5 : 0);
        subtitle.Format.Font.Size = 11;
        subtitle.Format.Font.Color = Color.FromRgb(0x66, 0x66, 0x66);
        return section;
    }

    // Depth of each page in the nav tree (1 = top level), used as the
    // page's own heading level so the outline panel nests the way the wiki
    // does. Bare folders (no page content) don't get an entry here — only
    // IsPage nodes correspond to a PageIr this renderer ever sees.
    private static Dictionary<string, int> ComputePageDepths(IReadOnlyList<NavNode> nav)
    {
        var result = new Dictionary<string, int>();
        void Walk(IReadOnlyList<NavNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                if (node.IsPage) result[node.Path.ToLinkPath()] = depth;
                Walk(node.Children, depth + 1);
            }
        }
        Walk(nav, 1);
        return result;
    }

    private static void RenderToc(Section section, IReadOnlyList<NavNode> nav)
    {
        var title = section.AddParagraph("Table of Contents");
        ApplyHeadingFormat(title, semanticLevel: 1, outlineDepth: 1);

        foreach (var node in nav) RenderTocNode(section, node, depth: 0);
    }

    private static void RenderTocNode(Section section, NavNode node, int depth)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.5 * depth);
        // MigraDoc measures tab stop positions from the page margin, not
        // from the paragraph's (indented) left edge, so every depth targets
        // the same absolute PageContentWidth — no indent compensation.
        // Subtracting indent here (an earlier attempt) pulled indented
        // rows' page numbers short of the top-level rows' by exactly the
        // indent amount.
        paragraph.Format.TabStops.AddTabStop(PageContentWidth, TabAlignment.Right, TabLeader.Dots);

        if (node.IsPage)
        {
            var anchor = PdfAnchors.PageAnchor(node.Path);
            StyleAsLink(AddText(paragraph.AddHyperlink(anchor, HyperlinkType.Bookmark).AddFormattedText(), node.Title));
            paragraph.AddTab();
            paragraph.AddPageRefField(anchor);
        }
        else
        {
            AddText(paragraph, node.Title).Font.Bold = true;
        }

        foreach (var child in node.Children) RenderTocNode(section, child, depth + 1);
    }

    // The PDFsharp-MigraDoc package is platform-agnostic and has no font
    // resolver wired up by default (unlike its -GDI/-WPF Windows-only
    // siblings), so document/error fonts can't be created at all without
    // one. EmbeddedFontResolver serves Atkinson Hyperlegible (plus DejaVu as
    // a glyph fallback) from TTF files embedded in this assembly, so
    // rendering works identically on any OS — no dependency on what's installed on the host (an
    // earlier Windows-only approach broke both this repo's own Linux CI
    // and any future non-Windows host). May only be set once per process,
    // before any font operation — guard against a second Render call
    // trying to set it again.
    private static void EnsureFontResolverRegistered()
    {
        if (_fontResolverRegistered) return;
        lock (FontResolverLock)
        {
            if (_fontResolverRegistered) return;
            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            _fontResolverRegistered = true;
        }
    }

    // A4 width (21cm) minus 2cm margins on each side. Set explicitly —
    // rather than guessing at MigraDoc's own default margins — so the TOC's
    // right-aligned tab stop (PageContentWidth below) can target the exact
    // usable width instead of a guessed value that silently falls outside
    // the printable area for some paragraphs and not others.
    private static readonly Unit PageContentWidth = Unit.FromCentimeter(17);
    private static readonly Unit PageContentHeight = Unit.FromCentimeter(25.7);

    // A file's natural size (pixels at its DPI, 72 when it has none) says
    // little about how big it should print: a phone photo is many pages
    // wide, an icon-sized PNG is a speck. Block images are set to a fixed
    // share of the text width instead, and shrink only when that would make
    // them taller than BlockImageMaxHeight. Inline images sit in a line of
    // text, so they keep their natural size unless it overflows the page.
    private const double BlockImageWidthFraction = 0.75;
    private const double BlockImageMaxHeightFraction = 0.75;
    private const double InlineImageMaxFraction = 0.8;

    private static Document NewDocument()
    {
        var document = new Document();
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = BodyFont;
        normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        return document;
    }

    // Document.DefaultPageSetup is frozen (read-only template) — every
    // section needs its own Clone() of it before margins can be changed.
    // Every call site that used to call document.AddSection() directly
    // goes through here instead so every section gets the same margins.
    private static Section AddSection(Document document)
    {
        var section = document.AddSection();
        section.PageSetup = section.PageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        return section;
    }

    // Every page written via `wikidown new`/Commands.New starts its body
    // with "# Title" (its own IrHeading), so synthesizing a second Heading1
    // here would print the title twice. The page's own leading heading (if
    // it has one) carries the PageAnchor bookmark instead; only pages that
    // genuinely don't open with a heading get a synthesized one. Its
    // outline level is the page's nav depth, and any further headings in
    // the body shift by the same offset so they nest underneath it.
    private static void RenderPage(Document document, PageIr page, int navDepth)
    {
        var section = AddSection(document);
        var pageAnchor = PdfAnchors.PageAnchor(page.Path);
        var pageLevel = Math.Clamp(navDepth, 1, 9);

        if (page.Blocks.Count > 0 && page.Blocks[0] is IrHeading firstHeading)
        {
            RenderHeading(section, firstHeading, pageAnchor, firstHeading.Level, pageLevel);
            var offset = pageLevel - firstHeading.Level;
            foreach (var block in page.Blocks.Skip(1)) RenderBlock(section, block, depth: 0, offset);
        }
        else
        {
            var heading = section.AddParagraph();
            AddText(heading, page.Title);
            ApplyHeadingFormat(heading, semanticLevel: 1, outlineDepth: pageLevel);
            heading.AddBookmark(pageAnchor);
            foreach (var block in page.Blocks) RenderBlock(section, block, depth: 0, headingOffset: pageLevel - 1);
        }
    }

    private static void RenderBlock(Section section, IrBlock block, int depth, int headingOffset)
    {
        switch (block)
        {
            case IrHeading h: RenderHeading(section, h, extraAnchor: null, h.Level, h.Level + headingOffset); break;
            case IrParagraph p: RenderRuns(section.AddParagraph(), p.Runs); break;
            case IrList l: RenderList(section, l, depth, headingOffset); break;
            case IrCodeBlock c: RenderCodeBlock(section, c, depth); break;
            case IrBlockQuote bq: RenderBlockQuote(section, bq, depth, headingOffset); break;
            case IrTable t: RenderTable(section, t); break;
            case IrImage img: RenderImage(section, img); break;
            case IrThematicBreak: RenderThematicBreak(section); break;
            case IrHtmlPlaceholder: RenderHtmlPlaceholder(section); break;
        }
    }

    // Visual size, semanticLevel, comes from the heading's own markdown
    // level (1 = biggest) so a page's title always looks like a title and
    // an H2 always looks like an H2, regardless of how deep the page sits
    // in the wiki. outlineDepth (nav depth + relative offset) is a
    // completely separate axis that only controls sidebar-bookmark
    // nesting — conflating the two (the original bug here) made headings
    // on deeply-nested pages shrink toward invisible.
    private static readonly double[] HeadingFontSizes = { 20, 16, 14, 12, 11, 10.5 };

    private static void RenderHeading(Section section, IrHeading heading, string? extraAnchor, int semanticLevel, int outlineDepth)
    {
        var paragraph = section.AddParagraph();
        ApplyHeadingFormat(paragraph, semanticLevel, outlineDepth);
        paragraph.AddBookmark(heading.AnchorId);
        if (extraAnchor is not null) paragraph.AddBookmark(extraAnchor);
        RenderRuns(paragraph, heading.Runs);
    }

    private static void ApplyHeadingFormat(Paragraph paragraph, int semanticLevel, int outlineDepth)
    {
        var sizeIndex = Math.Clamp(semanticLevel, 1, HeadingFontSizes.Length) - 1;
        paragraph.Format.Font.Size = HeadingFontSizes[sizeIndex];
        paragraph.Format.Font.Bold = true;
        paragraph.Format.SpaceBefore = Unit.FromPoint(sizeIndex == 0 ? 20 : 14);
        paragraph.Format.SpaceAfter = Unit.FromPoint(6);
        paragraph.Format.OutlineLevel = OutlineLevelFor(outlineDepth);
    }

    private static readonly OutlineLevel[] OutlineLevels =
    {
        OutlineLevel.Level1, OutlineLevel.Level2, OutlineLevel.Level3, OutlineLevel.Level4, OutlineLevel.Level5,
        OutlineLevel.Level6, OutlineLevel.Level7, OutlineLevel.Level8, OutlineLevel.Level9,
    };

    private static OutlineLevel OutlineLevelFor(int depth) => OutlineLevels[Math.Clamp(depth, 1, 9) - 1];

    // An item's later blocks sit under its bullet line: continuation
    // paragraphs and code align with the item text, nested lists and quotes
    // step one level deeper.
    private static void RenderList(Section section, IrList list, int depth, int headingOffset)
    {
        var index = 1;
        foreach (var item in list.Items)
        {
            var paragraph = section.AddParagraph();
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.6 * (depth + 1));
            paragraph.Format.SpaceAfter = Unit.FromPoint(2);
            var marker = list.Ordered ? $"{index}. " : "• ";
            paragraph.AddFormattedText(marker);
            RenderRuns(paragraph, item.Runs);
            foreach (var block in item.Blocks)
            {
                if (block is IrParagraph p)
                {
                    var continuation = section.AddParagraph();
                    continuation.Format.LeftIndent = Unit.FromCentimeter(0.6 * (depth + 1));
                    continuation.Format.SpaceAfter = Unit.FromPoint(2);
                    RenderRuns(continuation, p.Runs);
                }
                else
                {
                    RenderBlock(section, block, depth + 1, headingOffset);
                }
            }
            index++;
        }
    }

    private static void RenderCodeBlock(Section section, IrCodeBlock code, int depth)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.6 * depth);
        paragraph.Format.Font.Name = MonospaceFont;
        paragraph.Format.Shading.Color = Color.FromRgb(0xEE, 0xEE, 0xEE);
        paragraph.Format.Borders.Width = Unit.FromPoint(0.5);
        paragraph.Format.Borders.Color = Color.FromRgb(0xCC, 0xCC, 0xCC);
        paragraph.Format.SpaceBefore = Unit.FromPoint(4);
        paragraph.Format.SpaceAfter = Unit.FromPoint(4);

        var lines = code.Code.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) paragraph.AddLineBreak();
            AddText(paragraph, lines[i], monospace: true);
        }
    }

    // Only IrParagraph children get the indented/bordered/italic quote
    // styling directly — other block types (a nested list, say) fall
    // through to the normal dispatch, which still indents via the same
    // `depth` RenderList already scales by, just without the border/italic
    // treatment. A nested IrBlockQuote increases depth again so `>>` reads
    // as visually deeper than a single `>`.
    private static void RenderBlockQuote(Section section, IrBlockQuote quote, int depth, int headingOffset)
    {
        foreach (var block in quote.Blocks)
        {
            switch (block)
            {
                case IrParagraph p:
                    var paragraph = section.AddParagraph();
                    ApplyBlockQuoteFormat(paragraph, depth);
                    RenderRuns(paragraph, p.Runs);
                    break;
                case IrBlockQuote nested:
                    RenderBlockQuote(section, nested, depth + 1, headingOffset);
                    break;
                default:
                    RenderBlock(section, block, depth, headingOffset);
                    break;
            }
        }
    }

    private static void ApplyBlockQuoteFormat(Paragraph paragraph, int depth)
    {
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.6 * (depth + 1));
        paragraph.Format.Borders.Left.Width = Unit.FromPoint(2);
        paragraph.Format.Borders.Left.Color = Color.FromRgb(0xCC, 0xCC, 0xCC);
        paragraph.Format.Borders.DistanceFromLeft = Unit.FromPoint(8);
        paragraph.Format.Font.Italic = true;
        paragraph.Format.Font.Color = Color.FromRgb(0x55, 0x55, 0x55);
        paragraph.Format.SpaceBefore = Unit.FromPoint(4);
        paragraph.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void RenderTable(Section section, IrTable table)
    {
        var mdTable = section.AddTable();
        mdTable.Borders.Width = Unit.FromPoint(0.5);
        var columnCount = Math.Max(table.HeaderCells.Count, 1);
        foreach (var width in TableColumnLayout.ComputeWidths(table, PageContentWidth.Point))
            mdTable.AddColumn(Unit.FromPoint(width));

        var headerRow = mdTable.AddRow();
        headerRow.Format.Font.Bold = true;
        headerRow.Shading.Color = Color.FromRgb(0xEE, 0xEE, 0xEE);
        for (var i = 0; i < table.HeaderCells.Count; i++)
            RenderRuns(headerRow.Cells[i].AddParagraph(), table.HeaderCells[i]);

        foreach (var row in table.Rows)
        {
            var mdRow = mdTable.AddRow();
            for (var i = 0; i < row.Count && i < columnCount; i++)
                RenderRuns(mdRow.Cells[i].AddParagraph(), row[i]);
        }
    }

    private static void RenderImage(Section section, IrImage image)
    {
        if (image.ResolvedPath is not null)
        {
            var paragraph = section.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            paragraph.Format.SpaceBefore = Unit.FromPoint(6);
            paragraph.Format.SpaceAfter = Unit.FromPoint(10);
            AddImage(paragraph, image.ResolvedPath, image.LinkUrl, block: true);
        }
        else
        {
            var placeholder = section.AddParagraph();
            AddText(placeholder, $"[image not found: {image.RawTarget}]");
            placeholder.Format.Font.Italic = true;
            placeholder.Format.Borders.Width = Unit.FromPoint(0.5);
        }
    }

    private static void AddImage(Paragraph paragraph, string name, string? linkUrl, bool block)
    {
        var image = linkUrl is null
            ? paragraph.AddImage(name)
            : paragraph.AddHyperlink(linkUrl, HyperlinkType.Web).AddImage(name);
        FitImage(image, name, block);
    }

    private static void FitImage(Image image, string name, bool block)
    {
        if (NaturalSize(name) is not var (width, height) || width <= 0 || height <= 0) return;
        var scale = block
            ? Math.Min(
                BlockImageWidthFraction * PageContentWidth.Point / width,
                BlockImageMaxHeightFraction * PageContentHeight.Point / height)
            : Math.Min(1.0, Math.Min(
                InlineImageMaxFraction * PageContentWidth.Point / width,
                InlineImageMaxFraction * PageContentHeight.Point / height));
        image.LockAspectRatio = true;
        image.Width = Unit.FromPoint(width * scale);
        image.Height = Unit.FromPoint(height * scale);
    }

    // Same "base64:" convention MigraDoc's own image loader accepts.
    private static (double Width, double Height)? NaturalSize(string name)
    {
        try
        {
            using var image = name.StartsWith("base64:", StringComparison.Ordinal)
                ? XImage.FromStream(new MemoryStream(Convert.FromBase64String(name["base64:".Length..])))
                : XImage.FromFile(name);
            return (image.PointWidth, image.PointHeight);
        }
        catch (Exception)
        {
            // Unreadable here means MigraDoc can't load it either; it draws its own placeholder.
            return null;
        }
    }

    private static void RenderThematicBreak(Section section)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.Borders.Bottom.Width = Unit.FromPoint(1);
        paragraph.Format.Borders.Bottom.Color = Color.FromRgb(0xCC, 0xCC, 0xCC);
        paragraph.Format.SpaceBefore = Unit.FromPoint(6);
        paragraph.Format.SpaceAfter = Unit.FromPoint(6);
    }

    private static void RenderHtmlPlaceholder(Section section)
    {
        var paragraph = section.AddParagraph("[unsupported HTML block omitted]");
        paragraph.Format.Font.Italic = true;
    }

    // Link labels are rendered as plain text: nested bold/italic inside a
    // link label is rare in this wiki's content and Paragraph/Hyperlink
    // don't share a common formatted-content type worth generalizing over
    // for that edge case.
    private static void RenderRuns(Paragraph paragraph, IReadOnlyList<IrRun> runs)
    {
        foreach (var run in runs)
        {
            switch (run)
            {
                case IrText t:
                    var formatted = AddText(paragraph, t.Text, t.Code);
                    formatted.Font.Bold = t.Bold;
                    formatted.Font.Italic = t.Italic;
                    if (t.Code) formatted.Font.Name = MonospaceFont;
                    break;

                case IrLink link:
                    StyleAsLink(AddText(paragraph.AddHyperlink(link.AnchorId, HyperlinkType.Bookmark).AddFormattedText(), PlainText(link.Content)));
                    break;

                case IrExternalLink ext:
                    StyleAsLink(AddText(paragraph.AddHyperlink(ext.Url, HyperlinkType.Web).AddFormattedText(), PlainText(ext.Content)));
                    break;

                case IrInlineImage img when img.ResolvedPath is not null:
                    AddImage(paragraph, img.ResolvedPath, img.LinkUrl, block: false);
                    break;

                case IrInlineImage img:
                    AddText(paragraph, $"[image not found: {img.RawTarget}]").Font.Italic = true;
                    break;
            }
        }
    }

    private static FormattedText AddText(Paragraph paragraph, string text, bool monospace = false) =>
        AddText(paragraph.AddFormattedText(), text, monospace);

    // Atkinson Hyperlegible has no glyph for many characters wiki pages use
    // (arrows, box drawing, Greek, Cyrillic), which PDFsharp would
    // print as blanks. Each uncovered stretch goes into a nested run set in
    // DejaVu instead; nested runs inherit the parent's bold/italic/link style.
    private static FormattedText AddText(FormattedText target, string text, bool monospace = false)
    {
        bool Covered(char c) => c < ' ' || EmbeddedFontResolver.IsCovered(c, monospace);

        var start = 0;
        while (start < text.Length)
        {
            var covered = Covered(text[start]);
            var end = start + 1;
            while (end < text.Length && Covered(text[end]) == covered) end++;

            var segment = text[start..end];
            if (covered) target.AddText(segment);
            else target.AddFormattedText(segment).Font.Name = monospace ? FallbackMonospaceFont : FallbackBodyFont;
            start = end;
        }
        return target;
    }

    private static void StyleAsLink(FormattedText text)
    {
        text.Font.Color = LinkColor;
        text.Font.Underline = Underline.Single;
    }

    private static string PlainText(IReadOnlyList<IrRun> runs) =>
        string.Concat(runs.Select(r => r switch
        {
            IrText t => t.Text,
            IrLink l => PlainText(l.Content),
            IrExternalLink e => PlainText(e.Content),
            IrInlineImage i => i.AltText,
            _ => string.Empty,
        }));
}
