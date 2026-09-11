using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class StoredReportConfiguration : IEntityTypeConfiguration<StoredReport>
{
    public void Configure(EntityTypeBuilder<StoredReport> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.LayoutMode).HasMaxLength(16).IsRequired();

        // Unbounded: nvarchar(max) on SQL Server, text on PostgreSQL, NCLOB on Oracle.
        builder.Property(r => r.DefinitionJson).IsRequired();

        builder.Property(r => r.ConcurrencyToken).IsConcurrencyToken();

        builder.HasIndex(r => r.Name);
        builder.HasIndex(r => r.TenantId);
    }
}
