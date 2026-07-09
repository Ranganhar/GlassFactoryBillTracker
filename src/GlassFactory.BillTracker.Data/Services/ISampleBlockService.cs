using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface ISampleBlockService
{
    Task<List<SampleBlock>> GetSampleBlocksAsync(SampleBlockFilter? filter = null, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlockAttachment> AddAttachmentAsync(Guid sampleBlockId, string sourcePath, CancellationToken cancellationToken = default);
    Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
