using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.Models;

public sealed class OrderEditModel
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.Now;
    public Guid? CustomerId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.现金;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.未收款;
    public string? Note { get; set; }
    public string? AttachmentPath { get; set; }
}
