using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

        var normalizedName = (customer.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("客户名称不能为空。");
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
        var normalizedAddress = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
        var normalizedNote = string.IsNullOrWhiteSpace(customer.Note) ? null : customer.Note.Trim();

        var customerId = customer.Id == Guid.Empty ? Guid.NewGuid() : customer.Id;
        var duplicateExists = await db.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Name == normalizedName && x.Id != customerId, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("客户名称已存在，请使用其他名称。");
        }

        var now = DateTime.Now;
        var existing = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (existing is null)
        {
            var newCustomer = new Customer
            {
                Id = customerId,
                Name = normalizedName,
                Phone = normalizedPhone,
                Address = normalizedAddress,
                Note = normalizedNote,
                CreatedAt = now,
                UpdatedAt = now
            };

            await db.Customers.AddAsync(newCustomer, cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return newCustomer;
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "保存客户失败，CustomerId={CustomerId}, Name={CustomerName}", customerId, normalizedName);
                throw new InvalidOperationException("保存客户失败，请检查客户信息后重试。", ex);
            }
        }

        existing.Name = normalizedName;
        existing.Phone = normalizedPhone;
        existing.Address = normalizedAddress;
        existing.Note = normalizedNote;
        existing.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }
        catch (DbUpdateException ex)
        {
            Log.Error(ex, "保存客户失败，CustomerId={CustomerId}, Name={CustomerName}", customerId, normalizedName);
            throw new InvalidOperationException("保存客户失败，请检查客户信息后重试。", ex);
        }
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
