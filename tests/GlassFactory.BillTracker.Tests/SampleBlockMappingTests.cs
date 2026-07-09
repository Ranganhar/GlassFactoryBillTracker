using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
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
    public async Task SampleBlock_PersistsCustomerOrderTimeAndAttachments_CascadeDelete()
    {
        await using var db = NewDb();
        var sb = new SampleBlock { Model = "SB-1", Customer = "老王", OrderTime = new DateTime(2026, 5, 2), Note = "n" };
        sb.Attachments.Add(new SampleBlockAttachment { RelativePath = "attachments/sampleblocks/x/a.png" });
        db.SampleBlocks.Add(sb);
        await db.SaveChangesAsync();

        var reloaded = await db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).SingleAsync(x => x.Model == "SB-1");
        Assert.Equal("老王", reloaded.Customer);
        Assert.Equal(new DateTime(2026, 5, 2), reloaded.OrderTime);
        Assert.Single(reloaded.Attachments);

        db.SampleBlocks.Remove(await db.SampleBlocks.SingleAsync(x => x.Id == sb.Id));
        await db.SaveChangesAsync();
        Assert.Empty(await db.SampleBlockAttachments.ToListAsync());
    }
}
