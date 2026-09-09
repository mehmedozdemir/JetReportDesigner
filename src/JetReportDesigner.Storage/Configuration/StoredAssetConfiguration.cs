using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class StoredAssetConfiguration : IEntityTypeConfiguration<StoredAsset>
{
    public void Configure(EntityTypeBuilder<StoredAsset> builder)
    {
        builder.ToTable("Assets");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.Content).IsRequired();

        builder.HasIndex(a => a.Sha256).IsUnique();
    }
}
