using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface ISampleBlockService
{
    Task<List<SampleBlock>> GetSampleBlocksAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByModelAsync(string model, CancellationToken cancellationToken = default);
    Task<List<SampleBlock>> GetByWireIdAsync(Guid wireId, CancellationToken cancellationToken = default);
    Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
