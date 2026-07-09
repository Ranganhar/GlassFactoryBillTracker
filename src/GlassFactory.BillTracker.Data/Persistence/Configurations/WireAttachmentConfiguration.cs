using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class WireAttachmentConfiguration : IEntityTypeConfiguration<WireAttachment>
{
    public void Configure(EntityTypeBuilder<WireAttachment> builder)
    {
        builder.ToTable("WireAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.WireId);
    }
}
