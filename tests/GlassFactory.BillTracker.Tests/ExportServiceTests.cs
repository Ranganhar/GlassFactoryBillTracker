using ClosedXML.Excel;
using GlassFactory.BillTracker.Data.Exports;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class ExportServiceTests
{
    [Fact]
    public async Task ExportExcel_ShouldGenerateWorkbookWithRequiredSheetsAndHeaders()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(tempRoot, "data");
        var exportsDir = Path.Combine(dataDir, "exports");
        Directory.CreateDirectory(exportsDir);

        var dbPath = Path.Combine(dataDir, "billtracker.db");

        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var db = new BillTrackerDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                Name = "测试客户导出",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var order = new Order
            {
                OrderNo = "20260302-0001",
                DateTime = new DateTime(2026, 3, 2, 10, 30, 0),
                Customer = customer,
                PaymentMethod = PaymentMethod.微信,
                OrderStatus = OrderStatus.部分收款,
                Note = "导出测试",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                AttachmentPath = "attachments/20260302-0001/test.png",
                Items = new List<OrderItem>
                {
                    new()
                    {
                        GlassLengthMm = 2000m,
                        GlassWidthMm = 1000m,
                        Quantity = 2,
                        GlassUnitPricePerM2 = 100.1234m,
                        WireType = "丝A",
                        WireUnitPrice = 10m,
                        OtherFee = 2m,
                        Note = "明细A"
                    }
                }
            };

            OrderAmountCalculator.ApplyOrderTotal(order);

            await db.Customers.AddAsync(customer);
            await db.Orders.AddAsync(order);
            await db.SaveChangesAsync();
        }

        var exportService = new ExportService(dbPath);
        var result = await exportService.ExportExcelAsync(new ExportOrderFilter(), dataDir);

        Assert.True(File.Exists(result.FilePath));
        var fileInfo = new FileInfo(result.FilePath);
        Assert.True(fileInfo.Length > 0);

        using var workbook = new XLWorkbook(result.FilePath);
        Assert.True(workbook.Worksheets.Count >= 2);
        Assert.NotNull(workbook.Worksheet("Orders"));
        Assert.NotNull(workbook.Worksheet("OrderItems"));

        var orders = workbook.Worksheet("Orders");
        Assert.Equal("订单号 OrderNo", orders.Cell(1, 1).GetString());
        Assert.Equal("日期时间 DateTime", orders.Cell(1, 2).GetString());
        Assert.Equal("客户 CustomerName", orders.Cell(1, 3).GetString());
        Assert.Equal("支付方式 PaymentMethod", orders.Cell(1, 4).GetString());
        Assert.Equal("订单状态 OrderStatus", orders.Cell(1, 5).GetString());

        var orderItems = workbook.Worksheet("OrderItems");
        Assert.Equal("订单号 OrderNo", orderItems.Cell(1, 1).GetString());
        Assert.Equal("长(mm) GlassLengthMm", orderItems.Cell(1, 2).GetString());
        Assert.Equal("宽(mm) GlassWidthMm", orderItems.Cell(1, 3).GetString());
        Assert.Equal("数量 Quantity", orderItems.Cell(1, 4).GetString());
        Assert.Equal("玻璃单价(元/㎡) GlassUnitPricePerM2", orderItems.Cell(1, 5).GetString());

        var totalRow = orders.LastRowUsed()!.RowNumber();
        Assert.Equal("合计", orders.Cell(totalRow, 1).GetString());
        Assert.Equal(result.SumTotalAmount, orders.Cell(totalRow, 6).GetValue<decimal>());
    }
}
