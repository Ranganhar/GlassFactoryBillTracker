using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireMappingTests
{
    private static BillTrackerDbContext NewDb(out string dbPath)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options;
        var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Wire_ShouldPersistAndReload()
    {
        await using var db = NewDb(out _);
        var wire = new Wire { Model = "W-100", Manufacturer = "厂A", Price = 12.5m, Note = "n" };
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        var reloaded = await db.Wires.AsNoTracking().SingleAsync(x => x.Model == "W-100");
        Assert.Equal("厂A", reloaded.Manufacturer);
        Assert.Equal(12.5m, reloaded.Price);
    }
}
