using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface IWireService
{
    Task<List<Wire>> GetWiresAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wire?> GetByModelAsync(string model, CancellationToken cancellationToken = default);
    Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
