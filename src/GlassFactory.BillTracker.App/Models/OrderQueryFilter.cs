using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.Models;

public sealed class OrderQueryFilter
{
    public IReadOnlyCollection<Guid>? SelectedOrderIds { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public string? Keyword { get; set; }
    public bool IncludeWireTypeInKeyword { get; set; } = true;

    public string SortBy { get; set; } = "DateTime";
    public bool SortDescending { get; set; } = true;
}
