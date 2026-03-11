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
}
