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

    [Fact]
    public async Task OrderItem_ShouldPersistSampleBlockSnapshot_WithoutAffectingAmount()
    {
        await using var db = NewDb();
        var customer = new Customer { Name = "客户", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
        db.Customers.Add(customer);

        var item = new OrderItem
        {
            GlassLengthMm = 1000m,
            GlassWidthMm = 1000m,
            Quantity = 1,
            GlassUnitPricePerM2 = 10m,
            Model = "M-1",
            SampleBlockModel = "SB-1",
            WireType = "W-1",
            WireUnitPrice = 99m, // 样块价快照，不计入金额
            HoleFee = 3m,
            OtherFee = 2m
        };
        var order = new Order
        {
            OrderNo = "20260705-0001",
            DateTime = DateTime.Now,
            Customer = customer,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = new List<OrderItem> { item }
        };
        OrderAmountCalculator.ApplyOrderTotal(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var reloaded = await db.OrderItems.AsNoTracking().SingleAsync(x => x.Model == "M-1");
        Assert.Equal("SB-1", reloaded.SampleBlockModel);
        Assert.Equal("W-1", reloaded.WireType);
        // 金额 = 玻璃费(1.00*1*10=10) + 打孔3 + 其他2 = 15，样块价99 不参与
        Assert.Equal(15m, reloaded.Amount);
    }
}
