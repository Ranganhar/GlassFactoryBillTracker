using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class SampleBlockConfiguration : IEntityTypeConfiguration<SampleBlock>
{
    public void Configure(EntityTypeBuilder<SampleBlock> builder)
    {
        builder.ToTable("SampleBlocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Price).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();
        builder.HasIndex(x => x.WireId);
        // WireId -> Wire 关系在 WireConfiguration 单侧配置
    }
}
