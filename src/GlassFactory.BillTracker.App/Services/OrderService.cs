using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;
using GlassFactory.BillTracker.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.IO;

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
        try
        {
            await using var db = AppRuntimeContext.CreateDbContext();

            var query = db.Orders
                .AsNoTracking()
                .AsQueryable();

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }

            if (filter.SelectedOrderIds is { Count: > 0 })
            {
                query = query.Where(x => filter.SelectedOrderIds.Contains(x.Id));
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
            decimal sumTotal;
            if (totalCount == 0)
            {
                sumTotal = 0m;
            }
            else
            {
                var sumScaled = await query.SumAsync(x => (long)(x.TotalAmount * 10000m), cancellationToken);
                sumTotal = Math.Round(sumScaled / 10000m, 4, MidpointRounding.AwayFromZero);
            }

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
        catch (Exception ex)
        {
            Log.Error(ex, "查询订单失败");
            throw new InvalidOperationException("查询订单失败，请稍后重试。", ex);
        }
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

    public async Task<List<OrderExportDto>> QueryOrdersForExportAsync(OrderQueryFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = AppRuntimeContext.CreateDbContext();

        var query = db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .AsQueryable();

        if (filter.SelectedOrderIds is { Count: > 0 })
        {
            query = query.Where(x => filter.SelectedOrderIds.Contains(x.Id));
        }
        else
        {
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

            if (filter.OrderStatus.HasValue)
            {
                query = query.Where(x => x.OrderStatus == filter.OrderStatus.Value);
            }

            if (filter.PaymentMethod.HasValue)
            {
                query = query.Where(x => x.PaymentMethod == filter.PaymentMethod.Value);
            }
        }

        var orders = await query
            .OrderByDescending(x => x.DateTime)
            .ToListAsync(cancellationToken);

        return orders.Select(order => new OrderExportDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            DateTime = order.DateTime,
            CustomerName = order.Customer?.Name ?? string.Empty,
            CustomerPhone = order.Customer?.Phone,
            CustomerAddress = order.Customer?.Address,
            PaymentMethod = order.PaymentMethod,
            OrderStatus = order.OrderStatus,
            TotalAmount = order.TotalAmount,
            Note = order.Note,
            Items = (order.Items ?? Array.Empty<OrderItem>()).Select(item => new OrderExportItemDto
            {
                Model = item.Model,
                GlassLengthMm = item.GlassLengthMm,
                GlassWidthMm = item.GlassWidthMm,
                Quantity = item.Quantity,
                GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
                HoleFee = item.HoleFee,
                OtherFee = item.OtherFee,
                Amount = item.Amount,
                WireType = item.WireType,
                Note = item.Note
            }).ToList()
        }).ToList();
    }

    public async Task<DeleteSelectedResult> DeleteOrdersAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        var normalizedIds = (orderIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return new DeleteSelectedResult
            {
                RequestedCount = 0,
                DeletedCount = 0,
                NotFoundCount = 0,
                FailedCount = 0
            };
        }

        var deletedOrderNos = new List<string>();
        try
        {
            await using var db = AppRuntimeContext.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            var toDelete = await db.Orders
                .Where(x => normalizedIds.Contains(x.Id))
                .Select(x => new { x.Id, x.OrderNo })
                .ToListAsync(cancellationToken);

            var foundIds = toDelete.Select(x => x.Id).ToHashSet();
            var notFoundCount = normalizedIds.Count - foundIds.Count;

            if (toDelete.Count > 0)
            {
                var trackedOrders = await db.Orders
                    .Where(x => foundIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);

                db.Orders.RemoveRange(trackedOrders);
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                deletedOrderNos.AddRange(toDelete.Select(x => x.OrderNo));
            }
            else
            {
                await tx.RollbackAsync(cancellationToken);
            }

            var result = new DeleteSelectedResult
            {
                RequestedCount = normalizedIds.Count,
                DeletedCount = foundIds.Count,
                NotFoundCount = notFoundCount,
                FailedCount = 0
            };

            if (deletedOrderNos.Count > 0)
            {
                var attachmentsRoot = Path.Combine(AppRuntimeContext.DataDir, "attachments");
                foreach (var orderNo in deletedOrderNos)
                {
                    try
                    {
                        var orderDir = Path.Combine(attachmentsRoot, orderNo);
                        if (Directory.Exists(orderDir))
                        {
                            Directory.Delete(orderDir, recursive: true);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        result.AttachmentCleanupFailedCount++;
                        Log.Warning(cleanupEx, "删除订单附件目录失败，OrderNo={OrderNo}", orderNo);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量删除订单失败，Count={Count}", normalizedIds.Count);
            return new DeleteSelectedResult
            {
                RequestedCount = normalizedIds.Count,
                DeletedCount = 0,
                NotFoundCount = 0,
                FailedCount = normalizedIds.Count,
                ErrorMessage = ex.Message
            };
        }
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
                UpdatedAt = now,
                DateTime = orderModel.DateTime,
                CustomerId = orderModel.CustomerId.Value,
                PaymentMethod = orderModel.PaymentMethod,
                OrderStatus = orderModel.OrderStatus,
                Note = orderModel.Note
            };

            foreach (var item in items)
            {
                var newItem = new OrderItem
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    OrderId = entity.Id,
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
                entity.Items.Add(newItem);
            }

            entity.TotalAmount = OrderAmountCalculator.CalculateOrderTotal(entity.Items);
            await db.Orders.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity = await db.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == orderModel.Id, cancellationToken)
                ?? throw new InvalidOperationException("订单不存在。可能已被删除，请刷新后重试。");

            entity.DateTime = orderModel.DateTime;
            entity.CustomerId = orderModel.CustomerId.Value;
            entity.PaymentMethod = orderModel.PaymentMethod;
            entity.OrderStatus = orderModel.OrderStatus;
            entity.Note = orderModel.Note;
            entity.UpdatedAt = now;

            var existingItemById = entity.Items.ToDictionary(x => x.Id, x => x);
            var touchedIds = new HashSet<Guid>();

            foreach (var item in items)
            {
                if (item.Id != Guid.Empty && existingItemById.TryGetValue(item.Id, out var trackedItem))
                {
                    trackedItem.GlassLengthMm = item.GlassLengthMm;
                    trackedItem.GlassWidthMm = item.GlassWidthMm;
                    trackedItem.Quantity = item.Quantity;
                    trackedItem.GlassUnitPricePerM2 = item.GlassUnitPricePerM2;
                    trackedItem.Model = item.Model;
                    trackedItem.WireType = item.WireType;
                    trackedItem.WireUnitPrice = item.WireUnitPrice;
                    trackedItem.HoleFee = item.HoleFee;
                    trackedItem.OtherFee = item.OtherFee;
                    trackedItem.Note = item.Note;
                    OrderAmountCalculator.ApplyAmount(trackedItem);
                    touchedIds.Add(trackedItem.Id);
                    continue;
                }

                var newItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = entity.Id,
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
                entity.Items.Add(newItem);
                touchedIds.Add(newItem.Id);
            }

            var removedItems = entity.Items.Where(x => !touchedIds.Contains(x.Id)).ToList();
            if (removedItems.Count > 0)
            {
                db.OrderItems.RemoveRange(removedItems);
                foreach (var removedItem in removedItems)
                {
                    entity.Items.Remove(removedItem);
                }
            }

            entity.TotalAmount = OrderAmountCalculator.CalculateOrderTotal(entity.Items);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Log.Error(ex, "保存订单发生并发冲突，OrderId={OrderId}, OrderNo={OrderNo}", orderModel.Id, orderModel.OrderNo);
            throw new InvalidOperationException("订单已被修改或删除，请刷新后重试。", ex);
        }

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

        if (string.IsNullOrWhiteSpace(item.Model))
        {
            throw new InvalidOperationException("明细中的型号不能为空。");
        }

        if (item.GlassUnitPricePerM2 < 0 || item.HoleFee < 0 || item.OtherFee < 0)
        {
            throw new InvalidOperationException("明细中的单价与费用不能为负数。");
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
            "TotalAmount" => desc ? query.OrderByDescending(x => (double)x.TotalAmount) : query.OrderBy(x => (double)x.TotalAmount),
            "Note" => desc ? query.OrderByDescending(x => x.Note) : query.OrderBy(x => x.Note),
            _ => desc ? query.OrderByDescending(x => x.DateTime) : query.OrderBy(x => x.DateTime)
        };
    }
}
