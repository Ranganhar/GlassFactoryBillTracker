using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireServiceTests
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
        var svc = new WireService(dbPath, dataDir);
        await svc.SaveAsync(new Wire { Model = "W-1", Price = 1m });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SaveAsync(new Wire { Model = "W-1", Price = 2m }));
    }

    [Fact]
    public async Task GetWiresAsync_FiltersByModelPriceDateNote()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new WireService(dbPath, dataDir);
        await svc.SaveAsync(new Wire { Model = "钢丝A", Price = 10m, PurchaseDate = new DateTime(2026, 1, 10), Note = "红色" });
        await svc.SaveAsync(new Wire { Model = "铜丝B", Price = 50m, PurchaseDate = new DateTime(2026, 6, 20), Note = "蓝色" });

        Assert.Single(await svc.GetWiresAsync(new WireFilter { Model = "钢丝" }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PriceMin = 30m }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PriceMin = 5m, PriceMax = 20m }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PurchaseFrom = new DateTime(2026, 6, 1) }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { Note = "蓝" }));
        Assert.Equal(2, (await svc.GetWiresAsync()).Count);
    }

    [Fact]
    public async Task Attachment_AddThenRemove_CopiesAndDeletesFileAndRow()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new WireService(dbPath, dataDir);
        var wire = await svc.SaveAsync(new Wire { Model = "W-A", Price = 1m });
        var src = Path.Combine(dataDir, "src.png");
        await File.WriteAllBytesAsync(src, new byte[] { 1, 2, 3 });

        var att = await svc.AddAttachmentAsync(wire.Id, src);
        var abs = Path.Combine(dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(abs));
        Assert.StartsWith("attachments/wires/", att.RelativePath);
        Assert.Single((await svc.GetByIdAsync(wire.Id))!.Attachments);

        await svc.RemoveAttachmentAsync(att.Id);
        Assert.False(File.Exists(abs));
        Assert.Empty((await svc.GetByIdAsync(wire.Id))!.Attachments);
    }
}
