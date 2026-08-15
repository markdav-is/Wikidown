using Wikidown.Core;
using Wikidown.Core.PdfExport;
using Wikidown.Pdf.PdfExport;

namespace Wikidown.Cli;

public static class ExportPdfCommand
{
    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var outputPath = args.Require("output");
        var fromArg = args.Optional("from");
        var from = fromArg is null ? (PagePath?)null : PagePath.Parse(fromArg);

        var content = WikiPdfContent.BuildAll(repo, from, allowHtmlSkip: args.Flag("allow-html-skip"));

        using (var stream = File.Create(outputPath))
            MigraDocRenderer.Render(content, stream);

        w.WriteLine($"wrote {outputPath} ({content.Pages.Count} page(s))");
        return 0;
    }
}
