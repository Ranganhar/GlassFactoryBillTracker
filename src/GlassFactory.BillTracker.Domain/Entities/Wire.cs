namespace GlassFactory.BillTracker.Domain.Entities;

public class Wire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<WireAttachment> Attachments { get; set; } = new List<WireAttachment>();
    // Kept until the sample-block decoupling task (Task 6); removed there.
    public ICollection<SampleBlock> SampleBlocks { get; set; } = new List<SampleBlock>();
}
