using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireService : IWireService
{
    private readonly string _dbPath;

    public WireService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<Wire>> GetWiresAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var query = db.Wires.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.Model.Contains(k) || (x.Manufacturer ?? string.Empty).Contains(k));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Wire?> GetByModelAsync(string model, CancellationToken cancellationToken = default)
    {
        var normalized = (model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().FirstOrDefaultAsync(x => x.Model == normalized, cancellationToken);
    }

    public async Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (wire.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new InvalidOperationException("丝型号不能为空。");
        }

        await using var db = CreateDbContext();
        var id = wire.Id == Guid.Empty ? Guid.NewGuid() : wire.Id;
        var duplicate = await db.Wires.AsNoTracking()
            .AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("丝型号已存在，请使用其他型号。");
        }

        var manufacturer = string.IsNullOrWhiteSpace(wire.Manufacturer) ? null : wire.Manufacturer.Trim();
        var note = string.IsNullOrWhiteSpace(wire.Note) ? null : wire.Note.Trim();
        var now = DateTime.Now;

        var existing = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new Wire
            {
                Id = id,
                Model = normalizedModel,
                Manufacturer = manufacturer,
                Price = wire.Price,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now
            };
            await db.Wires.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.Model = normalizedModel;
        existing.Manufacturer = manufacturer;
        existing.Price = wire.Price;
        existing.Note = note;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var entity = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("丝不存在。");

        db.Wires.Remove(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("该丝已被样块引用，无法删除。请先删除相关样块。");
        }
    }
}
