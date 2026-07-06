namespace GlassFactory.BillTracker.App.Models;

public sealed class OrderListQueryResult
{
    public List<OrderListRowDto> Rows { get; init; } = new();
    public int TotalCount { get; init; }
    public decimal SumTotalAmount { get; init; }
}
