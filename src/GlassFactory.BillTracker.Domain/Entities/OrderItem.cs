namespace GlassFactory.BillTracker.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public decimal GlassLengthMm { get; set; }
    public decimal GlassWidthMm { get; set; }
    public int Quantity { get; set; }
    public decimal GlassUnitPricePerM2 { get; set; }

    public string Model { get; set; } = string.Empty;
    public string WireType { get; set; } = string.Empty;
    public decimal WireUnitPrice { get; set; }
    public decimal HoleFee { get; set; }
    public decimal OtherFee { get; set; }

    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
