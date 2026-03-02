using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class OrderListItemViewModel
{
    public Guid Id { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public DateTime DateTime { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public PaymentMethod PaymentMethod { get; init; }
    public OrderStatus OrderStatus { get; init; }
    public decimal TotalAmount { get; init; }
    public string? Note { get; init; }
    public string? AttachmentPath { get; init; }
}
