namespace GlassFactory.BillTracker.Domain.Entities;

public class Wire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ICollection<SampleBlock> SampleBlocks { get; set; } = new List<SampleBlock>();
}
