namespace GlassFactory.BillTracker.App.Models;

public enum PrintTemplateKind
{
    DotMatrix,
    A4
}

public enum DotMatrixHeightMode
{
    Full,
    Half,
    Third
}

public sealed class PrintBillOptions
{
    public string HeaderText { get; init; } = "亿达夹丝玻璃";
    public bool UseCustomerPhone { get; init; } = true;
    public string? CustomPhone { get; init; }
    public PrintTemplateKind TemplateKind { get; init; } = PrintTemplateKind.DotMatrix;
    public DotMatrixHeightMode DotMatrixHeightMode { get; init; } = DotMatrixHeightMode.Third;
    public double FontSize { get; init; } = 12d;
    public bool FitToPageScale { get; init; } = true;
    public int ManualScalePercent { get; init; } = 100;
    public string? PrinterName { get; init; }
    public bool PrinterImageableAreaKnown { get; init; }
    public bool PrinterImageableAreaFromCapabilities { get; init; }
    public double PrinterImageableOriginXDip { get; init; }
    public double PrinterImageableOriginYDip { get; init; }
    public double PrinterImageableWidthDip { get; init; }
    public double PrinterImageableHeightDip { get; init; }
}
