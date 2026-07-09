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
    public async Task SaveAsync_ShouldRequireExistingWire()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync(new SampleBlock { Model = "SB-1", WireId = Guid.NewGuid(), Price = 1m }));
    }

    [Fact]
    public async Task GetByModelAsync_ShouldReturnSampleBlockWithWire()
    {
        var (dbPath, dataDir) = NewEnv();
        var wireSvc = new WireService(dbPath, dataDir);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-9", Price = 7m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-9", WireId = wire.Id, Price = 30m });

        var found = await sbSvc.GetByModelAsync("SB-9");
        Assert.NotNull(found);
        Assert.Equal("W-9", found!.Wire.Model);
        Assert.Equal(30m, found.Price);
    }

    [Fact]
    public async Task GetByWireIdAsync_ShouldReturnRelatedSampleBlocks()
    {
        var (dbPath, dataDir) = NewEnv();
        var wireSvc = new WireService(dbPath, dataDir);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-10", Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-A", WireId = wire.Id, Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-B", WireId = wire.Id, Price = 2m });

        var related = await sbSvc.GetByWireIdAsync(wire.Id);
        Assert.Equal(2, related.Count);
    }

    [Fact]
    public async Task WireDelete_ShouldBeBlocked_WhenReferencedBySampleBlock()
    {
        var (dbPath, dataDir) = NewEnv();
        var wireSvc = new WireService(dbPath, dataDir);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-11", Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-C", WireId = wire.Id, Price = 1m });

        await Assert.ThrowsAsync<DbUpdateException>(() => wireSvc.DeleteAsync(wire.Id));
    }
}
