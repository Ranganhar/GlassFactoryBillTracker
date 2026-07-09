using System.IO;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class SampleBlockService : ISampleBlockService
{
    private readonly string _dbPath;
    private readonly string _dataDir;

    public SampleBlockService(string dbPath, string dataDir)
    {
        _dbPath = dbPath;
        _dataDir = dataDir;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<SampleBlock>> GetSampleBlocksAsync(SampleBlockFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new SampleBlockFilter();
        await using var db = CreateDbContext();
        var query = db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var k = filter.Model.Trim();
            query = query.Where(x => x.Model.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(filter.Customer))
        {
            var c = filter.Customer.Trim();
            query = query.Where(x => (x.Customer ?? string.Empty).Contains(c));
        }
        if (filter.OrderFrom.HasValue) query = query.Where(x => x.OrderTime != null && x.OrderTime >= filter.OrderFrom.Value);
        if (filter.OrderTo.HasValue) query = query.Where(x => x.OrderTime != null && x.OrderTime <= filter.OrderTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Note))
        {
            var n = filter.Note.Trim();
            query = query.Where(x => (x.Note ?? string.Empty).Contains(n));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (sampleBlock.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
            throw new InvalidOperationException("样块型号不能为空。");

        await using var db = CreateDbContext();
        var id = sampleBlock.Id == Guid.Empty ? Guid.NewGuid() : sampleBlock.Id;
        var duplicate = await db.SampleBlocks.AsNoTracking().AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate) throw new InvalidOperationException("样块型号已存在，请使用其他型号。");

        var customer = string.IsNullOrWhiteSpace(sampleBlock.Customer) ? null : sampleBlock.Customer.Trim();
        var note = string.IsNullOrWhiteSpace(sampleBlock.Note) ? null : sampleBlock.Note.Trim();
        var now = DateTime.Now;
        var existing = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new SampleBlock { Id = id, Model = normalizedModel, Customer = customer, OrderTime = sampleBlock.OrderTime, Note = note, CreatedAt = now, UpdatedAt = now };
            await db.SampleBlocks.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        existing.Model = normalizedModel;
        existing.Customer = customer;
        existing.OrderTime = sampleBlock.OrderTime;
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

        var dir = Path.Combine(_dataDir, "attachments", "sampleblocks", id.ToString());
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* non-fatal */ }
        }
    }

    public async Task<SampleBlockAttachment> AddAttachmentAsync(Guid sampleBlockId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("附件文件不存在。", sourcePath);
        await using var db = CreateDbContext();
        _ = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == sampleBlockId, cancellationToken)
            ?? throw new InvalidOperationException("样块不存在，无法添加附件。");

        var dir = Path.Combine(_dataDir, "attachments", "sampleblocks", sampleBlockId.ToString());
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
        var att = new SampleBlockAttachment { Id = Guid.NewGuid(), SampleBlockId = sampleBlockId, RelativePath = rel, CreatedAt = DateTime.Now };
        await db.SampleBlockAttachments.AddAsync(att, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return att;
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var att = await db.SampleBlockAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("附件记录不存在。");
        var abs = Path.Combine(_dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(abs)) File.Delete(abs);
        db.SampleBlockAttachments.Remove(att);
        await db.SaveChangesAsync(cancellationToken);
    }
}
