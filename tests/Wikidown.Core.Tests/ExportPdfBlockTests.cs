using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

// Every markdown construct a wiki page might contain must export to PDF —
// one page per case, so a failure names the construct that broke it.
public class ExportPdfBlockTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public ExportPdfBlockTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root, LineEndings.Lf);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private (int Code, string Output) Export(string root, params string[] extra)
    {
        var output = Path.Combine(_root, "out-" + Guid.NewGuid().ToString("N") + ".pdf");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CommandRunner.Run(
            new[] { "export-pdf", "--root", root, "--output", output }.Concat(extra).ToArray(), stdout, stderr);
        var text = stdout + stderr.ToString();
        if (code == 0)
        {
            var bytes = File.ReadAllBytes(output);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        }
        return (code, text);
    }

    private void AssertExports(string markdown)
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Home"), "# Home\n\n[Other](Other.md)\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Other"), "# Other\n\n## Section\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Page"), markdown));
        var (code, output) = Export(_root);
        Assert.True(code == 0, $"export-pdf exited {code}: {output}");
    }

    public static TheoryData<string, string> Blocks => new()
    {
        { "fenced backticks with info", "# P\n\n```csharp\nvar x = 1; // [link](Missing.md)\n```\n" },
        { "fenced tildes", "# P\n\n~~~\nplain\n~~~\n" },
        { "longer closing fence", "# P\n\n```\na\n`````\n\nafter\n" },
        { "fence containing other fence", "# P\n\n````markdown\n```\ninner\n```\n````\n" },
        { "unclosed fence at end", "# P\n\n```\nnever closed\n" },
        { "empty fenced block", "# P\n\n```\n```\n" },
        { "code with html and markdown",
            "# P\n\n```html\n<!-- comment -->\n<div class=\"x\">| a | b |</div>\n# not a heading\n- not a list\n```\n" },
        { "code with tabs, braces, unicode",
            "# P\n\n```\n\tindented\t{ } [ ] < > & \\ $ % ^\nünïcödé — “quotes” ✓ 日本語\n```\n" },
        { "very long code line", "# P\n\n```\n" + new string('x', 2000) + "\n```\n" },
        { "indented code block", "# P\n\nText.\n\n    indented code\n    more\n" },
        { "code block in list item", "# P\n\n- item\n\n  ```\n  code in item\n  ```\n\n- next\n" },
        { "code block in quote", "# P\n\n> quote\n>\n> ```\n> code\n> ```\n" },
        { "nested quotes with list and heading", "# P\n\n> ## Heading\n>\n> - a\n> - b\n>\n> > nested\n" },
        { "inline code everywhere", "# P `code`\n\nA `[x](Missing.md)` and ``tick ` inside`` span.\n\n- `list code`\n" },
        { "table with code, pipes and empty cells",
            "# P\n\n| Left | Center | Right |\n|:--|:-:|--:|\n| `a \\| b` | | **bold** |\n| [Other](Other.md) | `x` |\n" },
        { "table with more cells than header", "# P\n\n| A | B |\n| - | - |\n| 1 | 2 | 3 |\n" },
        { "table with only a header", "# P\n\n| A | B |\n| - | - |\n" },
        { "setext and deep headings", "Title\n=====\n\nSub\n---\n\n###### Six\n" },
        { "headings with links and code", "# P\n\n## [Other](Other.md)\n\n## `code` heading\n\n## [Section](Other.md#section)\n" },
        { "thematic breaks", "# P\n\n---\n\n***\n\n___\n" },
        { "hard line breaks", "# P\n\nline one  \nline two\\\nline three\n" },
        { "entities and escapes", "# P\n\n&copy; &amp; &#169; &nbsp; \\*not em\\* \\[not link\\]\n" },
        { "autolinks and reference links",
            "# P\n\n<https://example.com> and [ref][r] and [Other][o].\n\n[r]: https://example.com \"Title\"\n[o]: Other.md\n" },
        { "external and fragment links", "# P\n\n[ext](https://example.com) [top](#p) [mail](mailto:a@b.c)\n" },
        { "broken internal link", "# P\n\n[gone](Missing.md)\n" },
        { "nested emphasis", "# P\n\n***both*** **bold _and italic_** ~~strike~~ _a*b*c_\n" },
        { "task list", "# P\n\n- [ ] todo\n- [x] done\n" },
        { "ordered, loose and deep lists",
            "# P\n\n5. five\n6. six\n\n- loose\n\n- list\n\n- a\n  - b\n    - c\n      - d\n        1. e\n" },
        { "list item with two paragraphs", "# P\n\n- first para\n\n  second para\n- next\n" },
        { "list item with every block kind",
            "# P\n\n1. Step\n\n   More.\n\n   ```\n   code\n   ```\n\n   > quote\n\n   | A |\n   | - |\n   | 1 |\n\n   ![logo](https://example.com/l.png)\n\n   ---\n\n   - nested\n\n     ```\n     deeper code\n     ```\n" },
        { "list item starting with code", "# P\n\n- ```\n  code\n  ```\n-\n- empty above\n" },
        { "external image", "# P\n\n![logo](https://example.com/logo.png)\n\nInline ![i](https://example.com/i.png) image.\n" },
        { "crlf line endings", "# P\r\n\r\n```\r\ncode\r\n```\r\n\r\n| A |\r\n| - |\r\n| 1 |\r\n\r\n- a\r\n  - b\r\n" },
        { "heading only", "# P\n" },
        { "no heading at all", "just text\n" },
        { "whitespace only", "   \n\n\t\n" },
    };

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ExportPdf_HandlesBlock(string name, string markdown)
    {
        _ = name;
        AssertExports(markdown);
    }

    public static TheoryData<string, string> HtmlBlocks => new()
    {
        { "html comment block", "# P\n\n<!-- a note -->\n\ntext\n" },
        { "html block", "# P\n\n<details>\n<summary>More</summary>\n\nhidden\n\n</details>\n" },
        { "inline html", "# P\n\nline<br>break <kbd>Ctrl</kbd>\n" },
    };

    // Raw HTML is refused by default (documented) but must degrade cleanly
    // with --allow-html-skip rather than crash.
    [Theory]
    [MemberData(nameof(HtmlBlocks))]
    public void ExportPdf_RawHtml_FailsCleanlyOrSkips(string name, string markdown)
    {
        _ = name;
        _repo.Write(new WikiPage(PagePath.Parse("/Page"), markdown));

        var (refused, message) = Export(_root);
        Assert.Equal(1, refused);
        Assert.Contains("--allow-html-skip", message);

        var (skipped, output) = Export(_root, "--allow-html-skip");
        Assert.True(skipped == 0, $"export-pdf --allow-html-skip exited {skipped}: {output}");
    }

    [Fact]
    public void ExportPdf_Kanban()
    {
        Assert.Equal(0, CommandRunner.Run(["kanban", "init", "--root", _root], TextWriter.Null, TextWriter.Null));
        _repo.Write(new WikiPage(PagePath.Parse("/Kanban/To-Do/Buy-Paint"), "# Buy paint\n\n- [ ] white\n"));
        _repo.Append(PagePath.Parse("/Kanban"), "- [Buy paint](Kanban/To-Do/Buy-Paint.md)\n", afterSection: "To Do");

        var (code, output) = Export(_root);
        Assert.True(code == 0, $"export-pdf exited {code}: {output}");
    }

    [Fact]
    public void ExportPdf_ThisRepositorysOwnWiki()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Wikidown.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var (code, output) = Export(Path.Combine(dir.FullName, "docs"));
        Assert.True(code == 0, $"export-pdf of /docs exited {code}: {output}");
    }
}
