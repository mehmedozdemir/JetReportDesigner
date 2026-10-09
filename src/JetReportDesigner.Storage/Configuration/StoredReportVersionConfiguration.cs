using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class StoredReportVersionConfiguration : IEntityTypeConfiguration<StoredReportVersion>
{
    public void Configure(EntityTypeBuilder<StoredReportVersion> builder)
    {
        builder.ToTable("ReportVersions");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.DefinitionJson).IsRequired();
        builder.Property(v => v.SavedByEmail).HasMaxLength(256);
        builder.Property(v => v.Changes).HasMaxLength(400);
        builder.Property(v => v.SavedByEmail).HasMaxLength(256);
        builder.Property(v => v.Changes).HasMaxLength(400);

        builder.HasIndex(v => new { v.ReportId, v.Version }).IsUnique();
    }
}
