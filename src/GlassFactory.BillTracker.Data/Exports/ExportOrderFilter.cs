using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.Data.Exports;

public sealed class ExportOrderFilter
{
    public IReadOnlyCollection<Guid>? SelectedOrderIds { get; init; }
    public Guid? CustomerId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public OrderStatus? OrderStatus { get; init; }
    public string? Keyword { get; init; }
    public bool IncludeWireTypeInKeyword { get; init; } = true;
}
