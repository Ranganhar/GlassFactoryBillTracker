using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.Services;

public interface IOrderService
{
    Task<OrderListQueryResult> QueryOrdersAsync(OrderQueryFilter filter, CancellationToken cancellationToken = default);
    Task<List<OrderExportDto>> QueryOrdersForExportAsync(OrderQueryFilter filter, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<string> GenerateOrderNoAsync(DateTime dateTime, CancellationToken cancellationToken = default);
    Task<Order> SaveAsync(OrderEditModel orderModel, IReadOnlyList<OrderItem> items, string? newAttachmentSourcePath, bool removeAttachment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid orderId, CancellationToken cancellationToken = default);
}
