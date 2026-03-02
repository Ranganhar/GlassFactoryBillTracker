using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Infrastructure.Abstractions;

public interface IAttachmentService
{
    Task<string> AddAttachmentAsync(Guid orderId, string filePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderAttachment>> ListAttachmentsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
