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

    [Fact]
    public void Render_SetsBodyInAtkinsonNextAndCodeInAtkinsonMono()
    {
        var pdf = RenderToText("# Fonts\n\nPlain **bold** *italic* and `inline code`.\n\n```\nvar x = 1;\n```\n");

        Assert.Contains("+Atkinson#20Hyperlegible#20Next", pdf);
        Assert.Contains("+Atkinson#20Hyperlegible#20Mono", pdf);
        Assert.DoesNotContain("DejaVu", pdf);
    }

    [Fact]
    public void Render_CharactersAtkinsonLacks_FallBackToDejaVu()
    {
        var pdf = RenderToText("# Arrows\n\nStep one → step two.\n\n```\n├── `ticks`\n```\n");

        Assert.Contains("+DejaVu#20Sans/", pdf);
        Assert.Contains("+DejaVu#20Sans#20Mono/", pdf);
    }

    private string RenderToText(string markdown)
    {
        var path = PagePath.Parse("/A");
        _repo.Write(new WikiPage(path, markdown));
        var blocks = MarkdownIrBuilder.Build(_repo.Read(path).Markdown, path, _repo);

        using var stream = new MemoryStream();
        MigraDocRenderer.Render(new PageIr(path, "A", blocks), stream);
        return System.Text.Encoding.Latin1.GetString(stream.ToArray());
    }

    // A minimal valid 1x1 transparent PNG — exercises the real AddImage
    // codepath (not just the "not found" placeholder), since MigraDoc's
    // image loading turned out to be one more thing worth verifying at
    // runtime rather than assuming from the API shape (font resolution
    // was a similar surprise).
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

    [Theory]
    [InlineData(3000, 1000, 385.51, 128.50)]  // too wide: 80% of the 17cm content width
    [InlineData(400, 3000, 77.71, 582.80)]    // too tall: 80% of the 25.7cm content height
    [InlineData(200, 100, 200, 100)]          // small images keep their natural size
    public void Render_ScalesImagesDownToFitKeepingAspectRatio(int width, int height, double expectedWidth, double expectedHeight)
    {
        File.WriteAllBytes(Path.Combine(_root, "pic.png"), SolidPng(width, height));
        var path = PagePath.Parse("/A");
        _repo.Write(new WikiPage(path, "# A\n\n![pic](pic.png)\n"));
        var blocks = MarkdownIrBuilder.Build(_repo.Read(path).Markdown, path, _repo);

        var (drawnWidth, drawnHeight) = DrawnImageSize(new PageIr(path, "A", blocks));

        Assert.Equal(expectedWidth, drawnWidth, 1);
        Assert.Equal(expectedHeight, drawnHeight, 1);
    }

    [Fact]
    public void Render_ImageWithLinkUrl_IsAClickableWebLink()
    {
        File.WriteAllBytes(Path.Combine(_root, "pic.png"), MinimalPng);
        var page = new PageIr(PagePath.Parse("/A"), "A", new IrBlock[]
        {
            new IrImage("pic", "pic.png", Path.Combine(_root, "pic.png"), "https://github.com/o/r/blob/main/docs/pic.png"),
        });

        using var stream = new MemoryStream();
        MigraDocRenderer.Render(page, stream);

        Assert.Contains("/URI(https://github.com/o/r/blob/main/docs/pic.png)",
            System.Text.Encoding.Latin1.GetString(stream.ToArray()).Replace(" ", ""));
    }

    // The image's placement matrix ("w 0 0 h x y cm" right before "/I0 Do")
    // from the rendered page's content stream.
    private static (double Width, double Height) DrawnImageSize(PageIr page)
    {
        using var stream = new MemoryStream();
        MigraDocRenderer.Render(page, stream);
        stream.Position = 0;
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(stream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        var content = string.Concat(pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(p =>
            System.Text.Encoding.Latin1.GetString(p.Contents.CreateSingleContent().Stream.UnfilteredValue)));
        var match = System.Text.RegularExpressions.Regex.Match(content,
            @"([\d.]+) 0 0 (-?[\d.]+) [-\d.]+ [-\d.]+ cm\s*/I\d+ Do");
        Assert.True(match.Success, "no image drawn");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return (double.Parse(match.Groups[1].Value, inv), Math.Abs(double.Parse(match.Groups[2].Value, inv)));
    }

    // An uncompressed-DPI (so 72 dpi, 1px = 1pt) single-colour RGB PNG.
    private static byte[] SolidPng(int width, int height)
    {
        var raw = new byte[(width * 3 + 1) * height];
        using var zlib = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(zlib, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 2;
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", zlib.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typed = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typed);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(len, Crc32(typed));
        s.Write(len);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
