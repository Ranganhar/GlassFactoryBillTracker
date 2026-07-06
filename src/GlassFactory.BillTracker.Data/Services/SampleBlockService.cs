using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class SampleBlockService : ISampleBlockService
{
    private readonly string _dbPath;

    public SampleBlockService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<SampleBlock>> GetSampleBlocksAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var query = db.SampleBlocks.AsNoTracking().Include(x => x.Wire).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.Model.Contains(k) || x.Wire.Model.Contains(k));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SampleBlock?> GetByModelAsync(string model, CancellationToken cancellationToken = default)
    {
        var normalized = (model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .FirstOrDefaultAsync(x => x.Model == normalized, cancellationToken);
    }

    public async Task<List<SampleBlock>> GetByWireIdAsync(Guid wireId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .Where(x => x.WireId == wireId)
            .OrderBy(x => x.Model)
            .ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (sampleBlock.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new InvalidOperationException("样块型号不能为空。");
        }

        if (sampleBlock.WireId == Guid.Empty)
        {
            throw new InvalidOperationException("请为样块选择丝。");
        }

        await using var db = CreateDbContext();

        var wireExists = await db.Wires.AsNoTracking().AnyAsync(x => x.Id == sampleBlock.WireId, cancellationToken);
        if (!wireExists)
        {
            throw new InvalidOperationException("所选丝不存在，请重新选择。");
        }

        var id = sampleBlock.Id == Guid.Empty ? Guid.NewGuid() : sampleBlock.Id;
        var duplicate = await db.SampleBlocks.AsNoTracking()
            .AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("样块型号已存在，请使用其他型号。");
        }

        var note = string.IsNullOrWhiteSpace(sampleBlock.Note) ? null : sampleBlock.Note.Trim();
        var now = DateTime.Now;

        var existing = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new SampleBlock
            {
                Id = id,
                Model = normalizedModel,
                WireId = sampleBlock.WireId,
                Price = sampleBlock.Price,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now
            };
            await db.SampleBlocks.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.Model = normalizedModel;
        existing.WireId = sampleBlock.WireId;
        existing.Price = sampleBlock.Price;
        existing.Note = note;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var entity = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("样块不存在。");

        db.SampleBlocks.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
