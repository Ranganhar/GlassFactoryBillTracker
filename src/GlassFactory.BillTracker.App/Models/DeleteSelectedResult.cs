namespace GlassFactory.BillTracker.App.Models;

public sealed class DeleteSelectedResult
{
    public int RequestedCount { get; init; }
    public int DeletedCount { get; init; }
    public int NotFoundCount { get; init; }
    public int FailedCount { get; init; }
    public int AttachmentCleanupFailedCount { get; set; }
    public string? ErrorMessage { get; init; }
}
