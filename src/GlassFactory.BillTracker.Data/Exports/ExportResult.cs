namespace GlassFactory.BillTracker.Data.Exports;

public sealed class ExportResult
{
    public string FilePath { get; init; } = string.Empty;
    public int OrdersCount { get; init; }
    public int ItemsCount { get; init; }
    public decimal SumTotalAmount { get; init; }
}
