using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.GlassLengthMm).HasPrecision(18, 4);
        builder.Property(x => x.GlassWidthMm).HasPrecision(18, 4);
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.GlassUnitPricePerM2).HasPrecision(18, 4);
        builder.Property(x => x.WireType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WireUnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.OtherFee).HasPrecision(18, 4);
        builder.Property(x => x.LineAmount).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
    }
}
