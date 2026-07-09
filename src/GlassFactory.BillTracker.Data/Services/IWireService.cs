using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface IWireService
{
    Task<List<Wire>> GetWiresAsync(WireFilter? filter = null, CancellationToken cancellationToken = default);
    Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WireAttachment> AddAttachmentAsync(Guid wireId, string sourcePath, CancellationToken cancellationToken = default);
    Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
