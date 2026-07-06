using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.Models;

public sealed class OrderExportDto
{
    public Guid Id { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public DateTime DateTime { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerAddress { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public OrderStatus OrderStatus { get; init; }
    public decimal TotalAmount { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<OrderExportItemDto> Items { get; init; } = Array.Empty<OrderExportItemDto>();
}

public sealed class OrderExportItemDto
{
    public string Model { get; init; } = string.Empty;
    public decimal GlassLengthMm { get; init; }
    public decimal GlassWidthMm { get; init; }
    public int Quantity { get; init; }
    public decimal GlassUnitPricePerM2 { get; init; }
    public decimal HoleFee { get; init; }
    public decimal OtherFee { get; init; }
    public decimal Amount { get; init; }
    public string? WireType { get; init; }
    public string? Note { get; init; }
}
