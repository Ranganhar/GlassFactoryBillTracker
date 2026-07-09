using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class SampleBlockAttachmentConfiguration : IEntityTypeConfiguration<SampleBlockAttachment>
{
    public void Configure(EntityTypeBuilder<SampleBlockAttachment> builder)
    {
        builder.ToTable("SampleBlockAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.SampleBlockId);
    }
}
