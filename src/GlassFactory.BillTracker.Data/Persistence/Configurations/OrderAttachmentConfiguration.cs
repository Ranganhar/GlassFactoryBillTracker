using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class OrderAttachmentConfiguration : IEntityTypeConfiguration<OrderAttachment>
{
    public void Configure(EntityTypeBuilder<OrderAttachment> builder)
    {
        builder.ToTable("OrderAttachments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
