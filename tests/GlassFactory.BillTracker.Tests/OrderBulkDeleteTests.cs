using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class OrderBulkDeleteTests
{
    [Fact]
    public async Task BulkDeleteOrders_ShouldRemoveOrdersAndItems()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(tempRoot, "data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        Guid orderId1;
        Guid orderId2;

        await using (var db = new BillTrackerDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                Name = "批删客户",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var order1 = new Order
            {
                OrderNo = "20260303-1001",
                DateTime = DateTime.Now,
                Customer = customer,
                PaymentMethod = PaymentMethod.现金,
                OrderStatus = OrderStatus.未收款,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Items = new List<OrderItem>
                {
                    new()
                    {
                        GlassLengthMm = 1000m,
                        GlassWidthMm = 1000m,
                        Quantity = 1,
                        GlassUnitPricePerM2 = 100m,
                        Model = "M-DEL-1",
                        WireType = "丝A",
                        HoleFee = 1m,
                        OtherFee = 0m
                    }
                }
            };

            var order2 = new Order
            {
                OrderNo = "20260303-1002",
                DateTime = DateTime.Now,
                Customer = customer,
                PaymentMethod = PaymentMethod.微信,
                OrderStatus = OrderStatus.部分收款,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Items = new List<OrderItem>
                {
                    new()
                    {
                        GlassLengthMm = 1200m,
                        GlassWidthMm = 800m,
                        Quantity = 2,
                        GlassUnitPricePerM2 = 88m,
                        Model = "M-DEL-2",
                        WireType = "丝B",
                        HoleFee = 2m,
                        OtherFee = 1m
                    }
                }
            };

            OrderAmountCalculator.ApplyOrderTotal(order1);
            OrderAmountCalculator.ApplyOrderTotal(order2);

            await db.Orders.AddRangeAsync(order1, order2);
            await db.SaveChangesAsync();

            orderId1 = order1.Id;
            orderId2 = order2.Id;
        }

        await using (var db = new BillTrackerDbContext(options))
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var ids = new[] { orderId1, orderId2 };
            var orders = await db.Orders.Where(x => ids.Contains(x.Id)).ToListAsync();
            db.Orders.RemoveRange(orders);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await using (var db = new BillTrackerDbContext(options))
        {
            Assert.Equal(0, await db.Orders.CountAsync(x => x.Id == orderId1 || x.Id == orderId2));
            Assert.Equal(0, await db.OrderItems.CountAsync(x => x.OrderId == orderId1 || x.OrderId == orderId2));
        }
    }
}
