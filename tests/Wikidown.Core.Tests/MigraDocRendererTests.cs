using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Wikidown.Pdf.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class MigraDocRendererTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public MigraDocRendererTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void Render_OnFullFidelityPage_ProducesValidPdf()
    {
        var md = """
            # Kitchen Sink

            A paragraph with **bold**, *italic*, `code`, and a [link](#kitchen-sink).

            - Top item
              - Nested item

            ```csharp
            var x = 1;
            ```

            | A | B |
            | --- | --- |
            | 1 | 2 |

            > A quoted line.
            >
            > > A nested quote.

            ---

            ![missing image](does-not-exist.png)
            """;

        _repo.Write(new WikiPage(PagePath.Parse("/A"), md));
        var blocks = MarkdownIrBuilder.Build(_repo.Read(PagePath.Parse("/A")).Markdown, PagePath.Parse("/A"), _repo);
        var page = new PageIr(PagePath.Parse("/A"), "Kitchen Sink", blocks);

        using var stream = new MemoryStream();
        MigraDocRenderer.Render(page, stream);

        Assert.True(stream.Length > 0);
        var bytes = stream.ToArray();
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    // A minimal valid 1x1 transparent PNG — exercises the real AddImage
    // codepath (not just the "not found" placeholder), since MigraDoc's
    // image loading turned out to be one more thing worth verifying at
    // runtime rather than assuming from the API shape (see the font
    // resolver surprise in the single-page render chunk).
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public void Render_WithRealImage_EmbedsItWithoutError()
    {
        var imagePath = Path.Combine(_root, "pic.png");
        File.WriteAllBytes(imagePath, MinimalPng);

        _repo.Write(new WikiPage(PagePath.Parse("/A"), "# A\n\n![a real picture](pic.png)\n"));
        var blocks = MarkdownIrBuilder.Build(_repo.Read(PagePath.Parse("/A")).Markdown, PagePath.Parse("/A"), _repo);
        var image = Assert.IsType<IrImage>(blocks[1]);
        Assert.Equal(imagePath, image.ResolvedPath);

        var page = new PageIr(PagePath.Parse("/A"), "A", blocks);
        using var stream = new MemoryStream();
        MigraDocRenderer.Render(page, stream);

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(stream.ToArray(), 0, 5));
    }
}
