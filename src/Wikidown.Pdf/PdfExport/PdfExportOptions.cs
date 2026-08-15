namespace Wikidown.Pdf.PdfExport;

public sealed record PdfExportOptions(string Title, bool IncludeCover = true, bool IncludeToc = true);
