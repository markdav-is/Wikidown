using Wikidown.Core.PdfExport;

namespace Wikidown.Pdf.PdfExport;

// Sizes table columns to their content the way an HTML auto-layout table
// does, instead of splitting the page width equally: a "DC" column stays a
// few characters wide and the prose column takes the rest. Widths are
// estimated from character counts rather than measured with PdfSharp so
// the result is deterministic, cheap for large tables, and identical in
// the browser (Blazor WASM) and the CLI.
public static class TableColumnLayout
{
    // DejaVu Sans at the 10pt body size averages a little under 0.6em per
    // character across mixed prose; rounding up leaves slack so a cell
    // judged to fit on one line actually does.
    private const double CharWidthPt = 6.0;
    private const double BoldWidthFactor = 1.1;
    private const double CodeWidthFactor = 1.05;

    // MigraDoc's default left + right cell padding plus a hair of slack.
    private const double CellPaddingPt = 9.0;

    private const double InlineImageWidthPt = 60.0;

    public static double[] ComputeWidths(IrTable table, double availableWidthPt)
    {
        var columnCount = Math.Max(table.HeaderCells.Count, 1);
        var min = new double[columnCount];
        var pref = new double[columnCount];

        void Measure(IReadOnlyList<IReadOnlyList<IrRun>> cells, bool bold)
        {
            for (var i = 0; i < cells.Count && i < columnCount; i++)
            {
                var (cellMin, cellPref) = MeasureCell(cells[i], bold);
                min[i] = Math.Max(min[i], cellMin);
                pref[i] = Math.Max(pref[i], cellPref);
            }
        }

        Measure(table.HeaderCells, bold: true);
        foreach (var row in table.Rows) Measure(row, bold: false);

        for (var i = 0; i < columnCount; i++)
        {
            min[i] = Math.Min(min[i] + CellPaddingPt, availableWidthPt);
            pref[i] = Math.Clamp(pref[i] + CellPaddingPt, min[i], availableWidthPt);
        }

        return Distribute(min, pref, availableWidthPt);
    }

    // Narrow columns get their full preferred width first (walking columns
    // from the smallest preference up) as long as every remaining column can
    // still be given at least its minimum; whatever is left is shared among
    // the wide columns in proportion to how much more than their minimum
    // they wanted. If even the minimums don't fit, scale them down and let
    // MigraDoc wrap mid-word.
    private static double[] Distribute(double[] min, double[] pref, double available)
    {
        var count = min.Length;
        var widths = new double[count];
        var minTotal = min.Sum();
        if (minTotal > available)
        {
            for (var i = 0; i < count; i++) widths[i] = available * min[i] / minTotal;
            return widths;
        }

        var order = Enumerable.Range(0, count).OrderBy(i => pref[i]).ToArray();
        var assigned = new bool[count];
        var used = 0.0;
        var remainingMin = minTotal;
        foreach (var i in order)
        {
            if (used + pref[i] + (remainingMin - min[i]) > available) break;
            widths[i] = pref[i];
            assigned[i] = true;
            used += pref[i];
            remainingMin -= min[i];
        }

        var unassigned = Enumerable.Range(0, count).Where(i => !assigned[i]).ToArray();
        if (unassigned.Length == 0) return widths;

        var surplus = available - used - remainingMin;
        var wantTotal = unassigned.Sum(i => pref[i] - min[i]);
        foreach (var i in unassigned)
        {
            var share = wantTotal > 0 ? (pref[i] - min[i]) / wantTotal : 1.0 / unassigned.Length;
            widths[i] = min[i] + surplus * share;
        }
        return widths;
    }

    // Min is the widest unbreakable token, pref is the widest line if
    // nothing wrapped (explicit line breaks inside a cell start a new line).
    private static (double Min, double Pref) MeasureCell(IReadOnlyList<IrRun> runs, bool bold)
    {
        var min = 0.0;
        var pref = 0.0;
        var line = 0.0;
        var token = 0.0;

        void EndToken() { min = Math.Max(min, token); token = 0; }
        void EndLine() { EndToken(); pref = Math.Max(pref, line); line = 0; }

        foreach (var run in runs)
        {
            if (run is IrInlineImage)
            {
                EndToken();
                line += InlineImageWidthPt;
                token = InlineImageWidthPt;
                EndToken();
                continue;
            }

            var (text, factor) = run switch
            {
                IrText t => (t.Text, (bold || t.Bold ? BoldWidthFactor : 1.0) * (t.Code ? CodeWidthFactor : 1.0)),
                IrLink l => (PlainText(l.Content), bold ? BoldWidthFactor : 1.0),
                IrExternalLink e => (PlainText(e.Content), bold ? BoldWidthFactor : 1.0),
                _ => (string.Empty, 1.0),
            };

            foreach (var c in text)
            {
                if (c == '\n') { EndLine(); continue; }
                var w = CharWidthPt * factor;
                line += w;
                if (char.IsWhiteSpace(c)) EndToken(); else token += w;
            }
        }

        EndLine();
        return (min, pref);
    }

    private static string PlainText(IReadOnlyList<IrRun> runs) =>
        string.Concat(runs.Select(r => r switch
        {
            IrText t => t.Text,
            IrLink l => PlainText(l.Content),
            IrExternalLink e => PlainText(e.Content),
            IrInlineImage i => i.AltText,
            _ => string.Empty,
        }));
}
