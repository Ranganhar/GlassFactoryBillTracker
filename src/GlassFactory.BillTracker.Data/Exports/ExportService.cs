using System.Text.Json;
using ClosedXML.Excel;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Exports;

public sealed class ExportService : IExportService
{
    private readonly string _dbPath;

    public ExportService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task<ExportResult> ExportExcelAsync(ExportOrderFilter filter, string dataDir, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        var (orders, items) = await LoadDataAsync(filter, cancellationToken);
        var actualPath = ResolvePath(dataDir, targetPath, ".xlsx", "GlassFactoryBillTracker_Orders");

        using var workbook = new XLWorkbook();

        var ordersSheet = workbook.Worksheets.Add("Orders");
        WriteOrdersSheet(ordersSheet, orders);

        var byCustomerSheet = workbook.Worksheets.Add("ByCustomer");
        WriteByCustomerSheet(byCustomerSheet, orders);

        var itemsSheet = workbook.Worksheets.Add("OrderItems");
        WriteOrderItemsSheet(itemsSheet, items, orders.ToDictionary(x => x.Id, x => x.OrderNo));

        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        workbook.SaveAs(actualPath);

        return new ExportResult
        {
            FilePath = actualPath,
            OrdersCount = orders.Count,
            ItemsCount = items.Count,
            SumTotalAmount = OrderAmountCalculator.Round(orders.Sum(x => x.TotalAmount))
        };
    }

    public async Task<ExportResult> ExportJsonAsync(ExportOrderFilter filter, string dataDir, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        var (orders, _) = await LoadDataAsync(filter, cancellationToken);
        var actualPath = ResolvePath(dataDir, targetPath, ".json", "GlassFactoryBillTracker_Orders");

        var payload = orders.Select(x => new
        {
            x.OrderNo,
            DateTime = x.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            CustomerName = x.Customer.Name,
            PaymentMethod = x.PaymentMethod.ToString(),
            OrderStatus = x.OrderStatus.ToString(),
            TotalAmount = OrderAmountCalculator.Round(x.TotalAmount),
            x.Note,
            x.AttachmentPath
        }).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        await File.WriteAllTextAsync(actualPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return new ExportResult
        {
            FilePath = actualPath,
            OrdersCount = orders.Count,
            ItemsCount = 0,
            SumTotalAmount = OrderAmountCalculator.Round(orders.Sum(x => x.TotalAmount))
        };
    }

    private async Task<(List<Order> Orders, List<OrderItem> Items)> LoadDataAsync(ExportOrderFilter filter, CancellationToken cancellationToken)
    {
        await using var db = CreateDbContext();
        var query = ApplyFilter(db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .AsQueryable(), filter);

        var orders = await query
            .OrderByDescending(x => x.DateTime)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(x => x.Id).ToList();
        var items = orderIds.Count == 0
            ? new List<OrderItem>()
            : await db.OrderItems
                .AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId))
                .OrderBy(x => x.OrderId)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        return (orders, items);
    }

    private static IQueryable<Order> ApplyFilter(IQueryable<Order> query, ExportOrderFilter filter)
    {
        if (filter.SelectedOrderIds is { Count: > 0 })
        {
            query = query.Where(x => filter.SelectedOrderIds.Contains(x.Id));
            return query;
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(x => x.DateTime >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(x => x.DateTime <= filter.EndDate.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount <= filter.MaxAmount.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(x => x.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.OrderStatus.HasValue)
        {
            query = query.Where(x => x.OrderStatus == filter.OrderStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            if (filter.IncludeWireTypeInKeyword)
            {
                query = query.Where(x => x.OrderNo.Contains(keyword)
                                         || x.Customer.Name.Contains(keyword)
                                         || (x.Note ?? string.Empty).Contains(keyword)
                                         || x.Items.Any(i => i.WireType.Contains(keyword)));
            }
            else
            {
                query = query.Where(x => x.OrderNo.Contains(keyword)
                                         || x.Customer.Name.Contains(keyword)
                                         || (x.Note ?? string.Empty).Contains(keyword));
            }
        }

        return query;
    }

    private static void WriteOrdersSheet(IXLWorksheet sheet, IReadOnlyList<Order> orders)
    {
        var headers = new[]
        {
            "订单号 OrderNo",
            "日期时间 DateTime",
            "客户 CustomerName",
            "支付方式 PaymentMethod",
            "订单状态 OrderStatus",
            "总金额 TotalAmount",
            "备注 Note",
            "附件 AttachmentPath"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var order in orders)
        {
            sheet.Cell(row, 1).Value = order.OrderNo;
            sheet.Cell(row, 2).Value = order.DateTime;
            sheet.Cell(row, 3).Value = order.Customer.Name;
            sheet.Cell(row, 4).Value = order.PaymentMethod.ToString();
            sheet.Cell(row, 5).Value = order.OrderStatus.ToString();
            sheet.Cell(row, 6).Value = OrderAmountCalculator.Round(order.TotalAmount);
            sheet.Cell(row, 7).Value = order.Note ?? string.Empty;
            sheet.Cell(row, 8).Value = order.AttachmentPath ?? string.Empty;
            row++;
        }

        var totalRow = row;
        sheet.Cell(totalRow, 1).Value = "合计";
        sheet.Cell(totalRow, 6).Value = OrderAmountCalculator.Round(orders.Sum(x => x.TotalAmount));

        ApplySheetStyle(sheet, totalRow, 8, amountColumns: new[] { 6 }, dateColumns: new[] { 2 });
    }

    private static void WriteOrderItemsSheet(IXLWorksheet sheet, IReadOnlyList<OrderItem> items, IReadOnlyDictionary<Guid, string> orderNoMap)
    {
        var headers = new[]
        {
            "订单号 OrderNo",
            "型号 Model",
            "长(mm) GlassLengthMm",
            "宽(mm) GlassWidthMm",
            "数量 Quantity",
            "玻璃单价(元/㎡) GlassUnitPricePerM2",
            "面积(㎡) AreaM2",
            "玻璃费用 GlassCost",
            "丝织品类型 WireType",
            "打孔费 HoleFee",
            "其他费用 OtherFee",
            "金额 Amount",
            "备注 Note"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var item in items)
        {
            var area = OrderAmountCalculator.CalculateAreaM2(item.GlassLengthMm, item.GlassWidthMm);
            var glassCost = OrderAmountCalculator.CalculateGlassCost(item);
            var amount = OrderAmountCalculator.Round(item.Amount);

            sheet.Cell(row, 1).Value = orderNoMap.TryGetValue(item.OrderId, out var orderNo) ? orderNo : string.Empty;
            sheet.Cell(row, 2).Value = item.Model;
            sheet.Cell(row, 3).Value = Math.Round(item.GlassLengthMm, 1, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 4).Value = Math.Round(item.GlassWidthMm, 1, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 5).Value = item.Quantity;
            sheet.Cell(row, 6).Value = Math.Round(item.GlassUnitPricePerM2, 0, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 7).Value = Math.Round(area, 6, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 8).Value = OrderAmountCalculator.Round(glassCost);
            sheet.Cell(row, 9).Value = item.WireType;
            sheet.Cell(row, 10).Value = Math.Round(item.HoleFee, 0, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 11).Value = Math.Round(item.OtherFee, 0, MidpointRounding.AwayFromZero);
            sheet.Cell(row, 12).Value = amount;
            sheet.Cell(row, 13).Value = item.Note ?? string.Empty;

            row++;
        }

        ApplySheetStyle(sheet, row - 1, 13, amountColumns: new[] { 6, 8, 10, 11, 12 }, areaColumns: new[] { 7 });
        sheet.Column(3).Style.NumberFormat.Format = "0.0";
        sheet.Column(4).Style.NumberFormat.Format = "0.0";
        sheet.Column(5).Style.NumberFormat.Format = "0";
        sheet.Column(6).Style.NumberFormat.Format = "0";
        sheet.Column(10).Style.NumberFormat.Format = "0";
        sheet.Column(11).Style.NumberFormat.Format = "0";
    }

    private static void WriteByCustomerSheet(IXLWorksheet sheet, IReadOnlyList<Order> orders)
    {
        var row = 1;
        var groups = orders
            .GroupBy(x => x.Customer.Name)
            .OrderBy(x => x.Key)
            .ToList();

        if (groups.Count == 0)
        {
            sheet.Cell(row, 1).Value = "无可导出客户数据";
            return;
        }

        foreach (var group in groups)
        {
            var customer = group.First().Customer;
            sheet.Range(row, 1, row, 8).Merge();
            sheet.Cell(row, 1).Value = $"客户: {group.Key}    电话: {customer.Phone ?? string.Empty}    地址: {customer.Address ?? string.Empty}";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
            row++;

            sheet.Cell(row, 1).Value = "订单号";
            sheet.Cell(row, 2).Value = "日期时间";
            sheet.Cell(row, 3).Value = "支付方式";
            sheet.Cell(row, 4).Value = "订单状态";
            sheet.Cell(row, 5).Value = "总金额";
            sheet.Cell(row, 6).Value = "备注";
            sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            row++;

            foreach (var order in group.OrderByDescending(x => x.DateTime))
            {
                sheet.Cell(row, 1).Value = order.OrderNo;
                sheet.Cell(row, 2).Value = order.DateTime;
                sheet.Cell(row, 3).Value = order.PaymentMethod.ToString();
                sheet.Cell(row, 4).Value = order.OrderStatus.ToString();
                sheet.Cell(row, 5).Value = OrderAmountCalculator.Round(order.TotalAmount);
                sheet.Cell(row, 6).Value = order.Note ?? string.Empty;
                row++;
            }

            sheet.Cell(row, 4).Value = "小计";
            sheet.Cell(row, 5).Value = OrderAmountCalculator.Round(group.Sum(x => x.TotalAmount));
            sheet.Range(row, 4, row, 5).Style.Font.Bold = true;
            row += 2;
        }

        ApplySheetStyle(sheet, row - 1, 8, amountColumns: new[] { 5 }, dateColumns: new[] { 2 });
    }

    private static void ApplySheetStyle(
        IXLWorksheet sheet,
        int lastDataRow,
        int lastColumn,
        IReadOnlyList<int>? amountColumns = null,
        IReadOnlyList<int>? dateColumns = null,
        IReadOnlyList<int>? areaColumns = null)
    {
        var headerRange = sheet.Range(1, 1, 1, lastColumn);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);

        sheet.SheetView.FreezeRows(1);

        var tableRange = sheet.Range(1, 1, Math.Max(1, lastDataRow), lastColumn);
        tableRange.SetAutoFilter();

        if (amountColumns is not null)
        {
            foreach (var col in amountColumns)
            {
                sheet.Column(col).Style.NumberFormat.Format = "0.00";
            }
        }

        if (dateColumns is not null)
        {
            foreach (var col in dateColumns)
            {
                sheet.Column(col).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
            }
        }

        if (areaColumns is not null)
        {
            foreach (var col in areaColumns)
            {
                sheet.Column(col).Style.NumberFormat.Format = "0.000000";
            }
        }

        sheet.Columns(1, lastColumn).AdjustToContents(1, Math.Min(Math.Max(lastDataRow, 1), 200));
    }

    private static string ResolvePath(string dataDir, string? targetPath, string extension, string filePrefix)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            return targetPath;
        }

        var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
        return Path.Combine(dataDir, "exports", fileName);
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        return new BillTrackerDbContext(optionsBuilder.Options);
    }
}
