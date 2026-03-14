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
            WireUnitPrice = 16m,
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
            WireUnitPrice = 12m,
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

// Simulate edit-existing + copy-row save flow.
await using (var editDb = new BillTrackerDbContext(options))
{
    var existing = await editDb.Orders
        .Include(x => x.Items)
        .FirstAsync(x => x.Id == order.Id);

    var incoming = existing.Items
        .Select(x => new OrderItem
        {
            Id = x.Id,
            GlassLengthMm = x.GlassLengthMm,
            GlassWidthMm = x.GlassWidthMm,
            Quantity = x.Quantity,
            GlassUnitPricePerM2 = x.GlassUnitPricePerM2,
            Model = x.Model,
            WireType = x.WireType,
            WireUnitPrice = x.WireUnitPrice,
            HoleFee = x.HoleFee,
            OtherFee = x.OtherFee,
            Note = x.Note
        })
        .ToList();

    var source = incoming.First();
    incoming.Add(new OrderItem
    {
        Id = Guid.Empty,
        GlassLengthMm = source.GlassLengthMm,
        GlassWidthMm = source.GlassWidthMm,
        Quantity = source.Quantity,
        GlassUnitPricePerM2 = source.GlassUnitPricePerM2,
        Model = source.Model,
        WireType = source.WireType,
        WireUnitPrice = source.WireUnitPrice,
        HoleFee = source.HoleFee,
        OtherFee = source.OtherFee,
        Note = source.Note
    });

    var existingById = existing.Items.ToDictionary(x => x.Id, x => x);
    var originalExistingIds = existingById.Keys.ToHashSet();
    var retainedExistingIds = new HashSet<Guid>();

    foreach (var item in incoming)
    {
        if (item.Id != Guid.Empty && existingById.TryGetValue(item.Id, out var tracked))
        {
            var recalculatedAmount = OrderAmountCalculator.CalculateAmount(item);
            var changed =
                tracked.GlassLengthMm != item.GlassLengthMm ||
                tracked.GlassWidthMm != item.GlassWidthMm ||
                tracked.Quantity != item.Quantity ||
                tracked.GlassUnitPricePerM2 != item.GlassUnitPricePerM2 ||
                !string.Equals(tracked.Model, item.Model, StringComparison.Ordinal) ||
                !string.Equals(tracked.WireType, item.WireType, StringComparison.Ordinal) ||
                tracked.WireUnitPrice != item.WireUnitPrice ||
                tracked.HoleFee != item.HoleFee ||
                tracked.OtherFee != item.OtherFee ||
                !string.Equals(tracked.Note ?? string.Empty, item.Note ?? string.Empty, StringComparison.Ordinal) ||
                tracked.Amount != recalculatedAmount;

            if (changed)
            {
                tracked.GlassLengthMm = item.GlassLengthMm;
                tracked.GlassWidthMm = item.GlassWidthMm;
                tracked.Quantity = item.Quantity;
                tracked.GlassUnitPricePerM2 = item.GlassUnitPricePerM2;
                tracked.Model = item.Model;
                tracked.WireType = item.WireType;
                tracked.WireUnitPrice = item.WireUnitPrice;
                tracked.HoleFee = item.HoleFee;
                tracked.OtherFee = item.OtherFee;
                tracked.Note = item.Note;
                tracked.Amount = recalculatedAmount;
            }

            retainedExistingIds.Add(tracked.Id);
            continue;
        }

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = existing.Id,
            GlassLengthMm = item.GlassLengthMm,
            GlassWidthMm = item.GlassWidthMm,
            Quantity = item.Quantity,
            GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
            Model = item.Model,
            WireType = item.WireType,
            WireUnitPrice = item.WireUnitPrice,
            HoleFee = item.HoleFee,
            OtherFee = item.OtherFee,
            Note = item.Note
        };

        OrderAmountCalculator.ApplyAmount(newItem);
        existing.Items.Add(newItem);
        editDb.Entry(newItem).State = EntityState.Added;
    }

    var removed = existing.Items
        .Where(x => originalExistingIds.Contains(x.Id) && !retainedExistingIds.Contains(x.Id))
        .ToList();
    foreach (var old in removed)
    {
        existing.Items.Remove(old);
    }

    existing.TotalAmount = OrderAmountCalculator.CalculateOrderTotal(existing.Items);
    await editDb.SaveChangesAsync();
}

var savedOrder = await db.Orders
    .AsNoTracking()
    .Include(x => x.Items)
    .Include(x => x.Customer)
    .FirstAsync(x => x.Id == order.Id);

var expected = OrderAmountCalculator.CalculateOrderTotal(savedOrder.Items);

Console.WriteLine($"DB_PATH={dbPath}");
Console.WriteLine($"ORDER_NO={savedOrder.OrderNo}");
Console.WriteLine($"ITEM_COUNT={savedOrder.Items.Count}");
Console.WriteLine($"EXPECTED_TOTAL={expected:F0}");
Console.WriteLine($"DB_TOTAL={savedOrder.TotalAmount:F0}");

if (savedOrder.Items.Count != 3)
{
    throw new InvalidOperationException($"Copy-row edit save failed, expected 3 items, got {savedOrder.Items.Count}");
}

if (savedOrder.TotalAmount != expected)
{
    throw new InvalidOperationException($"TotalAmount mismatch, expected {expected:F0}, got {savedOrder.TotalAmount:F0}");
}

Console.WriteLine("SMOKE_TEST_PASS");

static string BuildUniqueSmokeOrderNo()
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var suffix = Guid.NewGuid().ToString("N")[..8];
    return $"SMOKE-{timestamp}-{suffix}";
}
