namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlockAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SampleBlockId { get; set; }
    public SampleBlock SampleBlock { get; set; } = null!;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
