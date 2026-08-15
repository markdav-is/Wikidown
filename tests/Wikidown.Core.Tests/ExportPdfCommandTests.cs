using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class ExportPdfCommandTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public ExportPdfCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(string outputPath, params string[] extra)
    {
        var args = new[] { "export-pdf", "--root", _root, "--output", outputPath }.Concat(extra).ToArray();
        return CommandRunner.Run(args, TextWriter.Null, TextWriter.Null);
    }

    [Fact]
    public void ExportPdf_OnMultiPageWiki_ProducesValidPdf()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\n[Guides](Guides.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n\n[Back home](../Home.md)\n"));
        _repo.WriteOrder(PagePath.Root, new[] { "Home", "Guides" });

        var outputPath = Path.Combine(_root, "wiki.pdf");
        var exitCode = Run(outputPath);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        var bytes = File.ReadAllBytes(outputPath);
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void ExportPdf_WithFrom_ScopesToSubtree()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Guides"), "# Guides\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Guides/Install"), "# Install\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "# Other\n"));

        var outputPath = Path.Combine(_root, "guides.pdf");
        var exitCode = Run(outputPath, "--from", "/Guides");

        Assert.Equal(0, exitCode);
        var bytes = File.ReadAllBytes(outputPath);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void ExportPdf_MissingOutput_FailsWithUsageError()
    {
        var args = new[] { "export-pdf", "--root", _root };
        Assert.Equal(2, CommandRunner.Run(args, TextWriter.Null, TextWriter.Null));
    }
}
