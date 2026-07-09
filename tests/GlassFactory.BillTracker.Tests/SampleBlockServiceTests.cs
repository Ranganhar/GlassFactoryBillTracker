using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class SampleBlockServiceTests
{
    private static (string dbPath, string dataDir) NewEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(dir, "data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return (dbPath, dataDir);
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateModel()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        await svc.SaveAsync(new SampleBlock { Model = "SB-1" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SaveAsync(new SampleBlock { Model = "SB-1" }));
    }

    [Fact]
    public async Task GetSampleBlocksAsync_FiltersByModelCustomerOrderTimeNote()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        await svc.SaveAsync(new SampleBlock { Model = "样块甲", Customer = "老王", OrderTime = new DateTime(2026, 1, 5), Note = "红" });
        await svc.SaveAsync(new SampleBlock { Model = "样块乙", Customer = "老李", OrderTime = new DateTime(2026, 6, 5), Note = "蓝" });

        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Model = "甲" }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Customer = "李" }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { OrderFrom = new DateTime(2026, 3, 1) }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Note = "红" }));
        Assert.Equal(2, (await svc.GetSampleBlocksAsync()).Count);
    }

    [Fact]
    public async Task Attachment_AddThenRemove_CopiesAndDeletes()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        var sb = await svc.SaveAsync(new SampleBlock { Model = "SB-A" });
        var src = Path.Combine(dataDir, "src.png");
        await File.WriteAllBytesAsync(src, new byte[] { 1, 2, 3 });

        var att = await svc.AddAttachmentAsync(sb.Id, src);
        var abs = Path.Combine(dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(abs));
        Assert.StartsWith("attachments/sampleblocks/", att.RelativePath);
        Assert.Single((await svc.GetByIdAsync(sb.Id))!.Attachments);

        await svc.RemoveAttachmentAsync(att.Id);
        Assert.False(File.Exists(abs));
        Assert.Empty((await svc.GetByIdAsync(sb.Id))!.Attachments);
    }
}
