using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNo { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.Now;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.现金;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.未收款;

    public string? Note { get; set; }
    public string? AttachmentPath { get; set; }

    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderAttachment> Attachments { get; set; } = new List<OrderAttachment>();
}
