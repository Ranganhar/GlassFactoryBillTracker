using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class WireConfiguration : IEntityTypeConfiguration<Wire>
{
    public void Configure(EntityTypeBuilder<Wire> builder)
    {
        builder.ToTable("Wires");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Price).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Wire)
            .HasForeignKey(x => x.WireId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
