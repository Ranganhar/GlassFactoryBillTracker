using GlassFactory.BillTracker.Data.Exports;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class OrderStatusFilterTests
{
    [Fact]
    public async Task ExportFilterByOrderStatus_ShouldReturnExpectedOrders()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(tempRoot, "data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var db = new BillTrackerDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                Name = "状态筛选客户",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await db.Customers.AddAsync(customer);

            var statuses = new[] { OrderStatus.未收款, OrderStatus.部分收款, OrderStatus.已收款 };
            for (var index = 0; index < statuses.Length; index++)
            {
                var order = new Order
                {
                    OrderNo = $"20260302-10{index + 1:D2}",
                    DateTime = DateTime.Now.AddMinutes(index),
                    Customer = customer,
                    PaymentMethod = PaymentMethod.现金,
                    OrderStatus = statuses[index],
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
                            Model = "M-001",
                            WireType = "筛选丝",
                            HoleFee = 1m,
                            OtherFee = 0m
                        }
                    }
                };

                OrderAmountCalculator.ApplyOrderTotal(order);
                await db.Orders.AddAsync(order);
            }

            await db.SaveChangesAsync();
        }

        var exportService = new ExportService(dbPath);

        var unpaid = await exportService.ExportJsonAsync(new ExportOrderFilter { OrderStatus = OrderStatus.未收款 }, dataDir);
        var partial = await exportService.ExportJsonAsync(new ExportOrderFilter { OrderStatus = OrderStatus.部分收款 }, dataDir);
        var paid = await exportService.ExportJsonAsync(new ExportOrderFilter { OrderStatus = OrderStatus.已收款 }, dataDir);

        Assert.Equal(1, unpaid.OrdersCount);
        Assert.Equal(1, partial.OrdersCount);
        Assert.Equal(1, paid.OrdersCount);
    }
}
