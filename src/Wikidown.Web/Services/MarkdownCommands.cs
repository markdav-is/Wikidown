using System.Text.RegularExpressions;
using BlazorMonaco;
using BlazorMonaco.Editor;
using Wikidown.Core;
using Range = BlazorMonaco.Range;

namespace Wikidown.Web.Services;

// The editor toolbar's actions, applied to Monaco's current selection as one
// undoable edit each.
public static partial class MarkdownCommands
{
    private const string Source = "wikidown-toolbar";

    /// <summary>Wraps the selection (or a placeholder) in markers, e.g. **bold**, and selects the inner text.</summary>
    public static async Task Wrap(StandaloneCodeEditor editor, string before, string after, string placeholder)
    {
        var selection = await editor.GetSelection();
        var model = await editor.GetModel();
        var text = await model.GetValueInRange(selection, EndOfLinePreference.TextDefined);
        if (text.Length == 0) text = placeholder;

        await Replace(editor, selection, before + text + after);
        var startColumn = selection.StartColumn + before.Length;
        if (!text.Contains('\n'))
            await editor.SetSelection(new Range(selection.StartLineNumber, startColumn,
                selection.StartLineNumber, startColumn + text.Length), Source);
        await editor.Focus();
    }

    /// <summary>
    /// Puts a prefix on every selected line ("- ", "> ", "## "), or takes it off
    /// when every line already has it. Headings replace any existing level.
    /// </summary>
    public static async Task ToggleLinePrefix(StandaloneCodeEditor editor, string prefix)
    {
        var selection = await editor.GetSelection();
        var model = await editor.GetModel();
        var first = selection.StartLineNumber;
        var last = selection.EndColumn == 1 && selection.EndLineNumber > first
            ? selection.EndLineNumber - 1
            : selection.EndLineNumber;

        var lines = new List<string>();
        for (var n = first; n <= last; n++) lines.Add(await model.GetLineContent(n));

        var heading = prefix.StartsWith('#');
        var numbered = prefix == "1. ";
        bool Has(string line) => numbered ? OrderedItem().IsMatch(line) : line.StartsWith(prefix, StringComparison.Ordinal);
        var remove = lines.All(Has);

        var changed = lines.Select((line, i) =>
        {
            if (remove) return numbered ? OrderedItem().Replace(line, "", 1) : line[prefix.Length..];
            if (heading) line = HeadingMarker().Replace(line, "", 1);
            return (numbered ? $"{i + 1}. " : prefix) + line;
        });

        var range = new Range(first, 1, last, await model.GetLineMaxColumn(last));
        await Replace(editor, range, string.Join('\n', changed));
        await editor.Focus();
    }

    /// <summary>Inserts a block (table, code fence) on its own lines at the cursor.</summary>
    public static async Task InsertBlock(StandaloneCodeEditor editor, string block)
    {
        var selection = await editor.GetSelection();
        var model = await editor.GetModel();
        // Blocks need a blank line above them, or Markdown folds them into
        // the paragraph before.
        var line = await model.GetLineContent(selection.StartLineNumber);
        var above = selection.StartLineNumber > 1 ? await model.GetLineContent(selection.StartLineNumber - 1) : "";
        var lead = line.Trim().Length > 0 ? "\n\n" : above.Trim().Length > 0 ? "\n" : "";
        await Replace(editor, selection, lead + block + "\n");
        await editor.Focus();
    }

    /// <summary>Wraps the selection in a fenced code block.</summary>
    public static async Task CodeBlock(StandaloneCodeEditor editor)
    {
        var selection = await editor.GetSelection();
        var model = await editor.GetModel();
        var text = await model.GetValueInRange(selection, EndOfLinePreference.TextDefined);
        await InsertBlock(editor, "```\n" + (text.Length == 0 ? "code" : text) + "\n```");
    }

    /// <summary>Inserts a relative link from one wiki page to another, using the selection as its text.</summary>
    public static async Task LinkToPage(StandaloneCodeEditor editor, PagePath from, PagePath to, string title)
    {
        var selection = await editor.GetSelection();
        var model = await editor.GetModel();
        var text = await model.GetValueInRange(selection, EndOfLinePreference.TextDefined);
        await Replace(editor, selection, $"[{(text.Length == 0 ? title : text)}]({RelativeLink(from, to)})");
        await editor.Focus();
    }

    public static string RelativeLink(PagePath from, PagePath to)
    {
        var fromDir = "/" + Path.GetDirectoryName(from.ToFilePath())?.Replace('\\', '/');
        var target = "/" + to.ToFilePath().Replace('\\', '/');
        return Path.GetRelativePath(fromDir, target).Replace('\\', '/');
    }

    public static string Table(int columns, int rows)
    {
        string Row(Func<int, string> cell) => "| " + string.Join(" | ", Enumerable.Range(1, columns).Select(cell)) + " |";
        return string.Join('\n', new[] { Row(c => $"Column {c}"), Row(_ => "---") }
            .Concat(Enumerable.Range(0, rows).Select(_ => Row(_ => " "))));
    }

    private static Task Replace(StandaloneCodeEditor editor, Range range, string text) =>
        editor.ExecuteEdits(Source,
            [new IdentifiedSingleEditOperation { Range = range, Text = text, ForceMoveMarkers = true }],
            (List<Selection>)null!);

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex OrderedItem();

    [GeneratedRegex(@"^#{1,6}\s+")]
    private static partial Regex HeadingMarker();
}
