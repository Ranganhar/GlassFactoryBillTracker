using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNo)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.DateTime).IsRequired();
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<int>();
        builder.Property(x => x.OrderStatus).HasConversion<int>();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.AttachmentPath).HasMaxLength(1000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.OrderNo).IsUnique();
        builder.HasIndex(x => x.DateTime);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.OrderStatus);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
