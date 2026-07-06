using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.Services;

public interface ICustomerService
{
    Task<List<Customer>> GetCustomersAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer> SaveAsync(Customer customer, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
