using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class SampleBlockMappingTests
{
    private static BillTrackerDbContext NewDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SampleBlock_ShouldPersistWithWire()
    {
        await using var db = NewDb();
        var wire = new Wire { Model = "W-1", Price = 3m };
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        db.SampleBlocks.Add(new SampleBlock { Model = "SB-1", WireId = wire.Id, Price = 20m });
        await db.SaveChangesAsync();

        var reloaded = await db.SampleBlocks.AsNoTracking().Include(x => x.Wire).SingleAsync(x => x.Model == "SB-1");
        Assert.Equal("W-1", reloaded.Wire.Model);
        Assert.Equal(20m, reloaded.Price);
    }


}
