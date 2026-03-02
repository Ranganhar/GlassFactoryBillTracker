using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.App.Services;

public sealed class CustomerService : ICustomerService
{
    public async Task<List<Customer>> GetCustomersAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Customer> SaveAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();

        if (customer.Id == Guid.Empty)
        {
            customer.Id = Guid.NewGuid();
        }

        var existing = await db.Customers.FirstOrDefaultAsync(x => x.Id == customer.Id, cancellationToken);
        if (existing is null)
        {
            customer.CreatedAt = DateTime.Now;
            customer.UpdatedAt = DateTime.Now;
            await db.Customers.AddAsync(customer, cancellationToken);
        }
        else
        {
            existing.Name = customer.Name;
            existing.Phone = customer.Phone;
            existing.Address = customer.Address;
            existing.Note = customer.Note;
            existing.UpdatedAt = DateTime.Now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        var entity = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("客户不存在。");

        db.Customers.Remove(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("该客户下存在订单，无法删除。请先删除或转移订单。", innerException: null);
        }
    }
}
