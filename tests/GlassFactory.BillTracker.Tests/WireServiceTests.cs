using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireServiceTests
{
    private static string NewDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return dbPath;
    }

    [Fact]
    public async Task SaveAsync_ShouldRejectDuplicateModel()
    {
        var dbPath = NewDbPath();
        var svc = new WireService(dbPath);
        await svc.SaveAsync(new Wire { Model = "W-1", Price = 1m });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync(new Wire { Model = "W-1", Price = 2m }));
    }

    [Fact]
    public async Task GetByModelAsync_ShouldReturnManufacturerAndPrice()
    {
        var dbPath = NewDbPath();
        var svc = new WireService(dbPath);
        await svc.SaveAsync(new Wire { Model = "W-2", Manufacturer = "厂B", Price = 9.5m });

        var found = await svc.GetByModelAsync("W-2");
        Assert.NotNull(found);
        Assert.Equal("厂B", found!.Manufacturer);
        Assert.Equal(9.5m, found.Price);
    }
}
