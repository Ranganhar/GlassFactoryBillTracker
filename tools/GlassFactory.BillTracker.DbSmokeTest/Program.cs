using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

var dataDir = args.FirstOrDefault();
if (string.IsNullOrWhiteSpace(dataDir))
{
    dataDir = Path.Combine(Environment.CurrentDirectory, "smoke-data");
}

Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "billtracker.db");

var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using var db = new BillTrackerDbContext(options);
await db.Database.MigrateAsync();

var customer = new Customer
{
    Name = "测试客户A",
    Phone = "13800000000",
    Address = "测试地址",
    CreatedAt = DateTime.Now,
    UpdatedAt = DateTime.Now
};

var order = new Order
{
    OrderNo = BuildUniqueSmokeOrderNo(),
    DateTime = DateTime.Now,
    Customer = customer,
    PaymentMethod = PaymentMethod.现金,
    OrderStatus = OrderStatus.未收款,
    Note = "Step B Smoke Test",
    CreatedAt = DateTime.Now,
    UpdatedAt = DateTime.Now,
    Items = new List<OrderItem>
    {
        new()
        {
            GlassLengthMm = 2000.0m,
            GlassWidthMm = 1000.0m,
            Quantity = 2,
            GlassUnitPricePerM2 = 120m,
            Model = "SM-A",
            WireType = "丝A",
            HoleFee = 16m,
            OtherFee = 3m,
            Note = "明细1"
        },
        new()
        {
            GlassLengthMm = 1500.0m,
            GlassWidthMm = 800.0m,
            Quantity = 3,
            GlassUnitPricePerM2 = 99m,
            Model = "SM-B",
            WireType = "丝B",
            HoleFee = 12m,
            OtherFee = 1m,
            Note = "明细2"
        }
    }
};

OrderAmountCalculator.ApplyOrderTotal(order);

await db.Customers.AddAsync(customer);
await db.Orders.AddAsync(order);
await db.SaveChangesAsync();

var savedOrder = await db.Orders
    .AsNoTracking()
    .Include(x => x.Items)
    .Include(x => x.Customer)
    .FirstAsync(x => x.Id == order.Id);

var expected = OrderAmountCalculator.CalculateOrderTotal(savedOrder.Items);

Console.WriteLine($"DB_PATH={dbPath}");
Console.WriteLine($"ORDER_NO={savedOrder.OrderNo}");
Console.WriteLine($"ITEM_COUNT={savedOrder.Items.Count}");
Console.WriteLine($"EXPECTED_TOTAL={expected:F2}");
Console.WriteLine($"DB_TOTAL={savedOrder.TotalAmount:F2}");

if (savedOrder.TotalAmount != expected)
{
    throw new InvalidOperationException($"TotalAmount mismatch, expected {expected:F2}, got {savedOrder.TotalAmount:F2}");
}

Console.WriteLine("SMOKE_TEST_PASS");

static string BuildUniqueSmokeOrderNo()
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var suffix = Guid.NewGuid().ToString("N")[..8];
    return $"SMOKE-{timestamp}-{suffix}";
}
