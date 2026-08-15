namespace Wikidown.Core.PdfExport;

// Plain, PDF-library-agnostic intermediate representation of a page's
// markdown, built by MarkdownIrBuilder and consumed by a renderer (in
// Wikidown.Pdf). "Ir"-prefixed to avoid colliding with Markdig's own
// Syntax types (HeadingBlock, ListBlock, CodeBlock, ...) when both
// namespaces are in scope.

public abstract record IrRun;

public sealed record IrText(string Text, bool Bold = false, bool Italic = false, bool Code = false) : IrRun;

// An internal wiki link/heading jump. AnchorId is a PdfAnchors-scheme id.
public sealed record IrLink(IReadOnlyList<IrRun> Content, string AnchorId) : IrRun;

public sealed record IrExternalLink(IReadOnlyList<IrRun> Content, string Url) : IrRun;

// An image referenced inline, mixed with other text in a paragraph. A
// paragraph containing nothing but a single image becomes a block-level
// IrImage instead (the common case for wiki pages).
public sealed record IrInlineImage(string AltText, string RawTarget, string? ResolvedPath) : IrRun;

public abstract record IrBlock;

// AnchorId is the full PdfAnchors.HeadingAnchor for this heading, already
// scoped to the page it's on.
public sealed record IrHeading(int Level, IReadOnlyList<IrRun> Runs, string AnchorId) : IrBlock;

public sealed record IrParagraph(IReadOnlyList<IrRun> Runs) : IrBlock;

public sealed record IrListItem(IReadOnlyList<IrRun> Runs, IrList? Nested);

public sealed record IrList(bool Ordered, IReadOnlyList<IrListItem> Items) : IrBlock;

public sealed record IrCodeBlock(string? Language, string Code) : IrBlock;

public sealed record IrTable(
    IReadOnlyList<IReadOnlyList<IrRun>> HeaderCells,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<IrRun>>> Rows) : IrBlock;

// ResolvedPath is null when the target is external or doesn't resolve to a
// file on disk — the renderer degrades this to a visible placeholder rather
// than failing the whole export.
public sealed record IrImage(string AltText, string RawTarget, string? ResolvedPath) : IrBlock;

public sealed record IrThematicBreak : IrBlock;

// Stands in for a raw HTML block when --allow-html-skip degrades instead of
// failing (the default is to throw — see MarkdownIrBuilder).
public sealed record IrHtmlPlaceholder : IrBlock;
