using System.IO;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireService : IWireService
{
    private readonly string _dbPath;
    private readonly string _dataDir;

    public WireService(string dbPath, string dataDir)
    {
        _dbPath = dbPath;
        _dataDir = dataDir;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<Wire>> GetWiresAsync(WireFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new WireFilter();
        await using var db = CreateDbContext();
        var query = db.Wires.AsNoTracking().Include(x => x.Attachments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var k = filter.Model.Trim();
            query = query.Where(x => x.Model.Contains(k));
        }
        if (filter.PriceMin.HasValue) query = query.Where(x => x.Price >= filter.PriceMin.Value);
        if (filter.PriceMax.HasValue) query = query.Where(x => x.Price <= filter.PriceMax.Value);
        if (filter.PurchaseFrom.HasValue) query = query.Where(x => x.PurchaseDate != null && x.PurchaseDate >= filter.PurchaseFrom.Value);
        if (filter.PurchaseTo.HasValue) query = query.Where(x => x.PurchaseDate != null && x.PurchaseDate <= filter.PurchaseTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Note))
        {
            var n = filter.Note.Trim();
            query = query.Where(x => (x.Note ?? string.Empty).Contains(n));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (wire.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
            throw new InvalidOperationException("丝型号不能为空。");

        await using var db = CreateDbContext();
        var id = wire.Id == Guid.Empty ? Guid.NewGuid() : wire.Id;
        var duplicate = await db.Wires.AsNoTracking().AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate) throw new InvalidOperationException("丝型号已存在，请使用其他型号。");

        var note = string.IsNullOrWhiteSpace(wire.Note) ? null : wire.Note.Trim();
        var now = DateTime.Now;
        var existing = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new Wire { Id = id, Model = normalizedModel, Price = wire.Price, PurchaseDate = wire.PurchaseDate, Note = note, CreatedAt = now, UpdatedAt = now };
            await db.Wires.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        existing.Model = normalizedModel;
        existing.Price = wire.Price;
        existing.PurchaseDate = wire.PurchaseDate;
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
        await db.SaveChangesAsync(cancellationToken);

        var dir = Path.Combine(_dataDir, "attachments", "wires", id.ToString());
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* non-fatal */ }
        }
    }

    public async Task<WireAttachment> AddAttachmentAsync(Guid wireId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("附件文件不存在。", sourcePath);
        await using var db = CreateDbContext();
        _ = await db.Wires.FirstOrDefaultAsync(x => x.Id == wireId, cancellationToken)
            ?? throw new InvalidOperationException("丝不存在，无法添加附件。");

        var dir = Path.Combine(_dataDir, "attachments", "wires", wireId.ToString());
        Directory.CreateDirectory(dir);
        var fileName = Path.GetFileName(sourcePath);
        var target = Path.Combine(dir, fileName);
        if (File.Exists(target))
        {
            var unique = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            target = Path.Combine(dir, unique);
        }
        File.Copy(sourcePath, target, overwrite: false);

        var rel = Path.GetRelativePath(_dataDir, target).Replace('\\', '/');
        var att = new WireAttachment { Id = Guid.NewGuid(), WireId = wireId, RelativePath = rel, CreatedAt = DateTime.Now };
        await db.WireAttachments.AddAsync(att, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return att;
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var att = await db.WireAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("附件记录不存在。");
        var abs = Path.Combine(_dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(abs)) File.Delete(abs);
        db.WireAttachments.Remove(att);
        await db.SaveChangesAsync(cancellationToken);
    }
}
