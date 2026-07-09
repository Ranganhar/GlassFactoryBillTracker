namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public DateTime? OrderTime { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<SampleBlockAttachment> Attachments { get; set; } = new List<SampleBlockAttachment>();
}
