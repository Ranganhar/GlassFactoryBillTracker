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
        builder.Property(x => x.Model).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WireType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WireUnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.HoleFee).HasPrecision(18, 4);
        builder.Property(x => x.OtherFee).HasPrecision(18, 4);
        builder.Property(x => x.SortIndex).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);

        // Redundant with the composite index below (the composite covers OrderId-prefixed
        // lookups), but InitialCreate physically created IX_OrderItems_OrderId and no migration
        // ever dropped it. This declaration keeps the model in sync with the snapshot + existing
        // databases so later migrations stay additive. Do NOT remove it — doing so makes EF emit a
        // destructive DropIndex on OrderItems in the next migration (see commit 9651c34).
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => new { x.OrderId, x.SortIndex });
    }
}
