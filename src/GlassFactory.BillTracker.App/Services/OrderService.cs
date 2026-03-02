using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;
using GlassFactory.BillTracker.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.App.Services;

public sealed class OrderService : IOrderService
{
    private readonly IAttachmentService _attachmentService;

    public OrderService(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    public async Task<OrderListQueryResult> QueryOrdersAsync(OrderQueryFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();

        var query = db.Orders
            .AsNoTracking()
            .AsQueryable();

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
                                         || (x.Note ?? string.Empty).Contains(keyword)
                                         || x.Customer.Name.Contains(keyword)
                                         || x.Items.Any(i => i.WireType.Contains(keyword)));
            }
            else
            {
                query = query.Where(x => x.OrderNo.Contains(keyword)
                                         || (x.Note ?? string.Empty).Contains(keyword)
                                         || x.Customer.Name.Contains(keyword));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sumTotal = totalCount == 0 ? 0m : await query.SumAsync(x => x.TotalAmount, cancellationToken);

        query = ApplySorting(query, filter.SortBy, filter.SortDescending);

        var rows = await query
            .Select(x => new OrderListRowDto
            {
                Id = x.Id,
                OrderNo = x.OrderNo,
                DateTime = x.DateTime,
                CustomerName = x.Customer.Name,
                PaymentMethod = x.PaymentMethod,
                OrderStatus = x.OrderStatus,
                TotalAmount = x.TotalAmount,
                Note = x.Note,
                AttachmentPath = x.AttachmentPath
            })
            .ToListAsync(cancellationToken);

        return new OrderListQueryResult
        {
            Rows = rows,
            TotalCount = totalCount,
            SumTotalAmount = sumTotal
        };
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        return await db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    public async Task<string> GenerateOrderNoAsync(DateTime dateTime, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        var prefix = dateTime.ToString("yyyyMMdd");

        var lastOrderNo = await db.Orders
            .AsNoTracking()
            .Where(x => x.OrderNo.StartsWith(prefix + "-"))
            .OrderByDescending(x => x.OrderNo)
            .Select(x => x.OrderNo)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrWhiteSpace(lastOrderNo))
        {
            var parts = lastOrderNo.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var seq))
            {
                next = seq + 1;
            }
        }

        return $"{prefix}-{next:D4}";
    }

    public async Task<Order> SaveAsync(
        OrderEditModel orderModel,
        IReadOnlyList<OrderItem> items,
        string? newAttachmentSourcePath,
        bool removeAttachment,
        CancellationToken cancellationToken = default)
    {
        if (orderModel.CustomerId is null || orderModel.CustomerId == Guid.Empty)
        {
            throw new InvalidOperationException("请选择客户。");
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("至少需要一条订单明细。");
        }

        foreach (var item in items)
        {
            ValidateItem(item);
        }

        await using var db = AppRuntimeContext.CreateDbContext();
        var now = DateTime.Now;

        Order entity;
        if (orderModel.Id == Guid.Empty)
        {
            entity = new Order
            {
                Id = Guid.NewGuid(),
                OrderNo = string.IsNullOrWhiteSpace(orderModel.OrderNo)
                    ? await GenerateOrderNoAsync(orderModel.DateTime, cancellationToken)
                    : orderModel.OrderNo,
                CreatedAt = now,
                UpdatedAt = now
            };

            await db.Orders.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity = await db.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == orderModel.Id, cancellationToken)
                ?? throw new InvalidOperationException("订单不存在。");

            entity.UpdatedAt = now;
        }

        entity.DateTime = orderModel.DateTime;
        entity.CustomerId = orderModel.CustomerId.Value;
        entity.PaymentMethod = orderModel.PaymentMethod;
        entity.OrderStatus = orderModel.OrderStatus;
        entity.Note = orderModel.Note;

        db.OrderItems.RemoveRange(entity.Items);
        entity.Items.Clear();

        foreach (var item in items)
        {
            var newItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = entity.Id,
                GlassLengthMm = item.GlassLengthMm,
                GlassWidthMm = item.GlassWidthMm,
                Quantity = item.Quantity,
                GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
                WireType = item.WireType,
                WireUnitPrice = item.WireUnitPrice,
                OtherFee = item.OtherFee,
                Note = item.Note
            };

            OrderAmountCalculator.ApplyLineAmount(newItem);
            entity.Items.Add(newItem);
        }

        entity.TotalAmount = OrderAmountCalculator.CalculateOrderTotal(entity.Items);
        await db.SaveChangesAsync(cancellationToken);

        if (removeAttachment)
        {
            var current = await _attachmentService.ListAttachmentsAsync(entity.Id, cancellationToken);
            foreach (var attachment in current)
            {
                await _attachmentService.RemoveAttachmentAsync(attachment.Id, cancellationToken);
            }

            await using var cleanDb = AppRuntimeContext.CreateDbContext();
            var cleanOrder = await cleanDb.Orders.FirstAsync(x => x.Id == entity.Id, cancellationToken);
            cleanOrder.AttachmentPath = null;
            cleanOrder.UpdatedAt = DateTime.Now;
            await cleanDb.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(newAttachmentSourcePath))
        {
            var existing = await _attachmentService.ListAttachmentsAsync(entity.Id, cancellationToken);
            foreach (var attachment in existing)
            {
                await _attachmentService.RemoveAttachmentAsync(attachment.Id, cancellationToken);
            }

            await _attachmentService.AddAttachmentAsync(entity.Id, newAttachmentSourcePath, cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken) ?? entity;
    }

    public async Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();
        var order = await db.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException("订单不存在。");

        db.Orders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateItem(OrderItem item)
    {
        if (item.GlassLengthMm <= 0)
        {
            throw new InvalidOperationException("明细中的长(mm)必须大于0。");
        }

        if (item.GlassWidthMm <= 0)
        {
            throw new InvalidOperationException("明细中的宽(mm)必须大于0。");
        }

        if (item.Quantity <= 0)
        {
            throw new InvalidOperationException("明细中的数量必须大于0。");
        }

        if (item.GlassUnitPricePerM2 < 0 || item.WireUnitPrice < 0 || item.OtherFee < 0)
        {
            throw new InvalidOperationException("明细中的单价与费用不能为负数。");
        }

        if (string.IsNullOrWhiteSpace(item.WireType))
        {
            throw new InvalidOperationException("明细中的丝织品类型不能为空。");
        }
    }

    private static IQueryable<Order> ApplySorting(IQueryable<Order> query, string sortBy, bool desc)
    {
        return sortBy switch
        {
            "OrderNo" => desc ? query.OrderByDescending(x => x.OrderNo) : query.OrderBy(x => x.OrderNo),
            "CustomerName" => desc ? query.OrderByDescending(x => x.Customer.Name) : query.OrderBy(x => x.Customer.Name),
            "PaymentMethod" => desc ? query.OrderByDescending(x => x.PaymentMethod) : query.OrderBy(x => x.PaymentMethod),
            "OrderStatus" => desc ? query.OrderByDescending(x => x.OrderStatus) : query.OrderBy(x => x.OrderStatus),
            "TotalAmount" => desc ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            "Note" => desc ? query.OrderByDescending(x => x.Note) : query.OrderBy(x => x.Note),
            _ => desc ? query.OrderByDescending(x => x.DateTime) : query.OrderBy(x => x.DateTime)
        };
    }
}
