using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.JSInterop;
using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Wikidown.Pdf.PdfExport;

namespace Wikidown.Web.Services;

/// <summary>
/// Renders the connected wiki with the same MigraDoc pipeline the CLI's
/// export-pdf uses, entirely in the browser, and hands the bytes to the
/// browser as a file download.
/// </summary>
public sealed class WikiPdfDownloader(PageImageResolver images, IJSRuntime js)
{
    private static readonly MarkdownPipeline ScanPipeline =
        new MarkdownPipelineBuilder().UsePipeTables().Build();

    public sealed record Progress(string Stage, int Done, int Total);

    public async Task<IReadOnlyList<PdfExportWarning>> DownloadAsync(
        WikiConnection conn,
        IReadOnlyList<(PagePath Path, string Markdown)> pages,
        Func<PagePath, IReadOnlyList<string>> orderFor,
        string fileName,
        Func<Progress, Task> report,
        CancellationToken ct = default)
    {
        var done = 0;
        foreach (var (path, markdown) in pages)
        {
            await report(new Progress("Fetching images", done, pages.Count));
            await images.PrefetchAsync(conn, path, markdown, ct);
            await ConvertUnsupportedAsync(conn, path, markdown);
            done++;
        }

        await report(new Progress("Rendering PDF", pages.Count, pages.Count));
        // Let the progress text paint before the synchronous render blocks the UI thread.
        await Task.Delay(50, ct);

        var source = new InMemoryPdfPageSource(
            pages.Select(p => p.Path),
            (from, target) => ToMigraDocName(images.DataUrlFor(conn, from, target)));
        var content = WikiPdfContent.Build(pages, orderFor, source, allowHtmlSkip: true);

        using var stream = new MemoryStream();
        MigraDocRenderer.Render(content, stream, new PdfExportOptions(Title: conn.Repo));

        await js.InvokeVoidAsync("wikidown.downloadFile", ct, fileName, "application/pdf", stream.ToArray());
        return content.Warnings;
    }

    // PDFsharp embeds PNG and JPEG only; anything else (webp, svg, gif…) is
    // re-encoded to PNG by the browser's canvas.
    private async Task ConvertUnsupportedAsync(WikiConnection conn, PagePath page, string markdown)
    {
        var doc = Markdown.Parse(markdown, ScanPipeline);
        foreach (var link in doc.Descendants<LinkInline>())
        {
            if (!link.IsImage || link.Url is null) continue;
            var dataUrl = images.DataUrlFor(conn, page, link.Url);
            if (dataUrl is null || dataUrl.StartsWith("data:image/png", StringComparison.Ordinal)
                                || dataUrl.StartsWith("data:image/jpeg", StringComparison.Ordinal))
                continue;

            string? png = null;
            try { png = await js.InvokeAsync<string?>("wikidown.toPngDataUrl", dataUrl); }
            catch (JSException) { }
            images.Replace(conn, page, link.Url, png);
        }
    }

    private static string? ToMigraDocName(string? dataUrl)
    {
        if (dataUrl is null) return null;
        var comma = dataUrl.IndexOf(',');
        return comma < 0 ? null : "base64:" + dataUrl[(comma + 1)..];
    }
}
