using Wikidown.Core.PdfExport;
using Wikidown.Pdf.PdfExport;
using Xunit;

namespace Wikidown.Core.Tests;

public class TableColumnLayoutTests
{
    private const double Available = 482; // 17cm in points

    private static IrTable Table(string[] header, params string[][] rows) =>
        new(
            header.Select(h => (IReadOnlyList<IrRun>)[new IrText(h)]).ToList(),
            rows.Select(r => (IReadOnlyList<IReadOnlyList<IrRun>>)r.Select(c => (IReadOnlyList<IrRun>)[new IrText(c)]).ToList()).ToList());

    [Fact]
    public void ShortTable_FitsContentInsteadOfFillingThePage()
    {
        var widths = TableColumnLayout.ComputeWidths(Table(["A", "B"], ["x", "y"]), Available);

        Assert.Equal(2, widths.Length);
        Assert.Equal(widths[0], widths[1], precision: 6);
        Assert.True(widths.Sum() < Available / 4, $"expected a narrow table, got {widths.Sum()}pt");
    }

    [Fact]
    public void ProseColumn_TakesTheSpaceNarrowColumnsDoNotNeed()
    {
        var prose = "The Guild hand at an argosy's helm and brakes, driving the mekillot teams, a skilled trade learned on the road.";
        var widths = TableColumnLayout.ComputeWidths(Table(["DC", "Tidbit", "Check"], ["20", prose, "History"]), Available);

        Assert.Equal(Available, widths.Sum(), precision: 6);
        Assert.True(widths[0] < 40, $"DC column should stay narrow, got {widths[0]}pt");
        Assert.True(widths[2] < 70, $"Check column should fit 'History' on one line, got {widths[2]}pt");
        Assert.True(widths[1] > Available * 0.7, $"prose column should dominate, got {widths[1]}pt");
    }

    [Fact]
    public void NarrowColumn_GetsItsFullPreferredWidthBeforeProseIsSized()
    {
        var prose = "A Guild land-train the size of a small village, drawn by mekillots; the Ring Road's freight and its inns";
        var narrowPref = TableColumnLayout.ComputeWidths(Table(["Word"], ["Long hauler"]), Available)[0];
        var widths = TableColumnLayout.ComputeWidths(Table(["Word", "Meaning"], ["Long hauler", prose]), Available);

        Assert.Equal(narrowPref, widths[0], precision: 6);
    }

    [Fact]
    public void SeveralProseColumns_ShareTheWidthProportionally()
    {
        var a = string.Join(' ', Enumerable.Repeat("word", 40));
        var b = string.Join(' ', Enumerable.Repeat("word", 40));
        var widths = TableColumnLayout.ComputeWidths(Table(["A", "B"], [a, b]), Available);

        Assert.Equal(Available, widths.Sum(), precision: 6);
        Assert.Equal(widths[0], widths[1], precision: 6);
    }

    [Fact]
    public void UnbreakableTokens_SetTheMinimumWidth()
    {
        var longToken = new string('x', 30);
        var widths = TableColumnLayout.ComputeWidths(Table(["A", "B"], [longToken + " " + longToken, "short words here"]), Available);

        Assert.True(widths[0] >= 30 * 6, $"column must hold its longest token, got {widths[0]}pt");
    }

    [Fact]
    public void MinimumsWiderThanThePage_AreScaledDownRatherThanOverflowing()
    {
        var huge = new string('x', 200);
        var widths = TableColumnLayout.ComputeWidths(Table(["A", "B"], [huge, huge]), Available);

        Assert.Equal(Available, widths.Sum(), precision: 6);
        Assert.Equal(widths[0], widths[1], precision: 6);
    }

    [Fact]
    public void RaggedRows_OnlyMeasureTheColumnsTheyHave()
    {
        var widths = TableColumnLayout.ComputeWidths(Table(["A", "B", "C"], ["only one cell"]), Available);

        Assert.Equal(3, widths.Length);
        Assert.Equal(widths[1], widths[2], precision: 6);
        Assert.True(widths[0] > widths[1]);
    }
}
