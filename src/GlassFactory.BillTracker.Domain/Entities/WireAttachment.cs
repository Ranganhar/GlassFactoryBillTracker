namespace GlassFactory.BillTracker.Domain.Entities;

public class WireAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WireId { get; set; }
    public Wire Wire { get; set; } = null!;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
