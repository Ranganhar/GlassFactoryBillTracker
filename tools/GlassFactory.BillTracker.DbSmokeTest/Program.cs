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
            GlassLengthMm = 2000m,
            GlassWidthMm = 1000m,
            Quantity = 2,
            GlassUnitPricePerM2 = 120.1234m,
            Model = "SM-A",
            WireType = "丝A",
            HoleFee = 15.5000m,
            OtherFee = 3.2500m,
            Note = "明细1"
        },
        new()
        {
            GlassLengthMm = 1500m,
            GlassWidthMm = 800m,
            Quantity = 3,
            GlassUnitPricePerM2 = 98.8888m,
            Model = "SM-B",
            WireType = "丝B",
            HoleFee = 12.0000m,
            OtherFee = 1.0000m,
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
Console.WriteLine($"EXPECTED_TOTAL={expected:F4}");
Console.WriteLine($"DB_TOTAL={savedOrder.TotalAmount:F4}");

if (savedOrder.TotalAmount != expected)
{
    throw new InvalidOperationException($"TotalAmount mismatch, expected {expected:F4}, got {savedOrder.TotalAmount:F4}");
}

Console.WriteLine("SMOKE_TEST_PASS");

static string BuildUniqueSmokeOrderNo()
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var suffix = Guid.NewGuid().ToString("N")[..8];
    return $"SMOKE-{timestamp}-{suffix}";
}
