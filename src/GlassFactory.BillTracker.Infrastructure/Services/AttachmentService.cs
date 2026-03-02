using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Infrastructure.Services;

public sealed class AttachmentService : IAttachmentService
{
    private readonly string _dataDir;
    private readonly string _dbPath;

    public AttachmentService(string dataDir, string dbPath)
    {
        _dataDir = dataDir;
        _dbPath = dbPath;
    }

    public async Task<string> AddAttachmentAsync(Guid orderId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("附件文件不存在。", filePath);
        }

        await using var db = CreateDbContext();
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException("订单不存在，无法添加附件。");

        var orderDir = Path.Combine(_dataDir, "attachments", order.OrderNo);
        Directory.CreateDirectory(orderDir);

        var fileName = Path.GetFileName(filePath);
        var targetPath = Path.Combine(orderDir, fileName);
        if (File.Exists(targetPath))
        {
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            targetPath = Path.Combine(orderDir, uniqueName);
        }

        File.Copy(filePath, targetPath, overwrite: false);

        var relativePath = Path.GetRelativePath(_dataDir, targetPath).Replace('\\', '/');
        var attachment = new OrderAttachment
        {
            OrderId = order.Id,
            RelativePath = relativePath,
            CreatedAt = DateTime.Now
        };

        await db.OrderAttachments.AddAsync(attachment, cancellationToken);
        order.AttachmentPath = relativePath;
        order.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync(cancellationToken);
        return relativePath;
    }

    public async Task<IReadOnlyList<OrderAttachment>> ListAttachmentsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.OrderAttachments
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var attachment = await db.OrderAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("附件记录不存在。");

        var order = await db.Orders.FirstAsync(x => x.Id == attachment.OrderId, cancellationToken);

        var absolutePath = Path.Combine(_dataDir, attachment.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        db.OrderAttachments.Remove(attachment);

        if (order.AttachmentPath == attachment.RelativePath)
        {
            var fallback = await db.OrderAttachments
                .Where(x => x.OrderId == order.Id && x.Id != attachmentId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.RelativePath)
                .FirstOrDefaultAsync(cancellationToken);

            order.AttachmentPath = fallback;
            order.UpdatedAt = DateTime.Now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        return new BillTrackerDbContext(optionsBuilder.Options);
    }
}
