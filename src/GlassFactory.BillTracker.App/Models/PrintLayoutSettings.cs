namespace GlassFactory.BillTracker.App.Models;

public sealed class PrintLayoutSettings
{
    public double PrintableWidthMm { get; init; } = 210d;
    public double PrintableHeightMm { get; init; } = 93d;
    public double MarginLeftMm { get; init; } = 5d;
    public double MarginRightMm { get; init; } = 5d;
    public double MarginTopMm { get; init; } = 5d;
    public double MarginBottomMm { get; init; } = 5d;
}
