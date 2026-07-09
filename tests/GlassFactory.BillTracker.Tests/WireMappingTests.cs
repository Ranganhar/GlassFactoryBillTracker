using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireMappingTests
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
    public async Task Wire_PersistsPurchaseDateAndAttachments_CascadeDelete()
    {
        await using var db = NewDb();
        var wire = new Wire { Model = "W-100", Price = 12.5m, PurchaseDate = new DateTime(2026, 3, 1), Note = "n" };
        wire.Attachments.Add(new WireAttachment { RelativePath = "attachments/wires/x/a.png" });
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        var reloaded = await db.Wires.AsNoTracking().Include(x => x.Attachments).SingleAsync(x => x.Model == "W-100");
        Assert.Equal(new DateTime(2026, 3, 1), reloaded.PurchaseDate);
        Assert.Single(reloaded.Attachments);

        db.Wires.Remove(await db.Wires.SingleAsync(x => x.Id == wire.Id));
        await db.SaveChangesAsync();
        Assert.Empty(await db.WireAttachments.ToListAsync());
    }
}
