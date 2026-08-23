using Wikidown.Core;
using Wikidown.Html;

namespace Wikidown.Cli;

public static class ExportHtmlCommand
{
    public static int Run(WikiRepository repo, ParsedArgs args, TextWriter w)
    {
        var result = HtmlExporter.Export(repo, new HtmlExportOptions(
            OutputDirectory: args.Require("output"),
            Title: args.Optional("title"),
            BaseUrl: args.Optional("base-url"),
            Clean: args.Flag("clean")));

        w.WriteLine($"exported {result.PageCount} page(s) to {result.OutputDirectory}");
        return 0;
    }
}
