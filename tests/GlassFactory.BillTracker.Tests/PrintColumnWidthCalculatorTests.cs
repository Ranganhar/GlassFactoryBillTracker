using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.Tests;

public class PrintColumnWidthCalculatorTests
{
    [Fact]
    public void Compute_UsesModelDataAndTitleOnlyColumns_PerSpec()
    {
        var columns = BuildColumns();
        var smallFontTitles = new Dictionary<string, double>
        {
            ["Model"] = 24d,
            ["Length"] = 38d,
            ["Width"] = 38d,
            ["Quantity"] = 24d,
            ["UnitPrice"] = 62d,
            ["HoleFee"] = 34d,
            ["OtherFee"] = 48d,
            ["Amount"] = 48d,
            ["Note"] = 24d
        };

        var inputs = columns.Select(c => new PrintColumnWidthInput
        {
            Key = c.Key,
            TitleWidth = smallFontTitles[c.Key],
            MinWidth = c.MinWidth,
            MaxWidth = c.MaxWidth,
            IsNote = c.IsNote
        }).ToList();

        var widths = PrintColumnWidthCalculator.Compute(
            inputs,
            modelMaxDataWidth: 70d,
            printableWidthDip: 620d,
            horizontalMarginsDip: 60d,
            gapDip: 0d,
            paddingDip: 6d);

        Assert.Equal(76d, widths["Model"], 3);
        Assert.Equal(44d, widths["Length"], 3);
        Assert.Equal(44d, widths["Width"], 3);
        Assert.Equal(30d, widths["Quantity"], 3);
        Assert.Equal(68d, widths["UnitPrice"], 3);
        Assert.Equal(40d, widths["HoleFee"], 3);
        Assert.Equal(54d, widths["OtherFee"], 3);
        Assert.Equal(54d, widths["Amount"], 3);
        Assert.Equal(620d - 60d - (76d + 44d + 44d + 30d + 68d + 40d + 54d + 54d), widths["Note"], 3);
    }

    [Fact]
    public void Compute_RecomputesWhenFontSizeChanges_ByUsingNewTitleWidths()
    {
        var columns = BuildColumns();
        var smallTitles = new Dictionary<string, double>
        {
            ["Model"] = 24d,
            ["Length"] = 38d,
            ["Width"] = 38d,
            ["Quantity"] = 24d,
            ["UnitPrice"] = 62d,
            ["HoleFee"] = 34d,
            ["OtherFee"] = 48d,
            ["Amount"] = 48d,
            ["Note"] = 24d
        };
        var largeTitles = new Dictionary<string, double>
        {
            ["Model"] = 31d,
            ["Length"] = 51d,
            ["Width"] = 51d,
            ["Quantity"] = 30d,
            ["UnitPrice"] = 82d,
            ["HoleFee"] = 45d,
            ["OtherFee"] = 63d,
            ["Amount"] = 63d,
            ["Note"] = 31d
        };

        var smallWidths = PrintColumnWidthCalculator.Compute(
            columns.Select(c => ToInput(c, smallTitles[c.Key])).ToList(),
            modelMaxDataWidth: 70d,
            printableWidthDip: 700d,
            horizontalMarginsDip: 60d,
            gapDip: 0d,
            paddingDip: 6d);

        var largeWidths = PrintColumnWidthCalculator.Compute(
            columns.Select(c => ToInput(c, largeTitles[c.Key])).ToList(),
            modelMaxDataWidth: 96d,
            printableWidthDip: 700d,
            horizontalMarginsDip: 60d,
            gapDip: 0d,
            paddingDip: 6d);

        Assert.True(largeWidths["Length"] > smallWidths["Length"]);
        Assert.True(largeWidths["UnitPrice"] > smallWidths["UnitPrice"]);
        Assert.True(largeWidths["Model"] > smallWidths["Model"]);

        var smallTotal = smallWidths.Values.Sum();
        var largeTotal = largeWidths.Values.Sum();
        Assert.InRange(smallTotal, 639.9d, 640.1d);
        Assert.InRange(largeTotal, 639.9d, 640.1d);
    }

    private static PrintColumnWidthInput ToInput((string Key, double MinWidth, double MaxWidth, bool IsNote) c, double titleWidth)
    {
        return new PrintColumnWidthInput
        {
            Key = c.Key,
            TitleWidth = titleWidth,
            MinWidth = c.MinWidth,
            MaxWidth = c.MaxWidth,
            IsNote = c.IsNote
        };
    }

    private static List<(string Key, double MinWidth, double MaxWidth, bool IsNote)> BuildColumns()
    {
        return new List<(string Key, double MinWidth, double MaxWidth, bool IsNote)>
        {
            ("Model", 48d, double.MaxValue, false),
            ("Length", 28d, double.MaxValue, false),
            ("Width", 28d, double.MaxValue, false),
            ("Quantity", 26d, double.MaxValue, false),
            ("UnitPrice", 40d, double.MaxValue, false),
            ("HoleFee", 40d, double.MaxValue, false),
            ("OtherFee", 40d, double.MaxValue, false),
            ("Amount", 44d, double.MaxValue, false),
            ("Note", 40d, double.MaxValue, true)
        };
    }
}
