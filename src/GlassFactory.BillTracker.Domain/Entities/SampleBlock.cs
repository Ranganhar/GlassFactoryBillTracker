namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public Guid WireId { get; set; }
    public Wire Wire { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
