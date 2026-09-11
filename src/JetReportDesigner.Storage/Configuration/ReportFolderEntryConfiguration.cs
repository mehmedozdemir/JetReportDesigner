using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class ReportFolderEntryConfiguration : IEntityTypeConfiguration<ReportFolderEntry>
{
    public void Configure(EntityTypeBuilder<ReportFolderEntry> builder)
    {
        builder.ToTable("ReportFolderEntries");

        builder.HasKey(e => e.ReportId);

        builder.HasIndex(e => e.FolderId);
        builder.HasIndex(e => e.TenantId);
    }
}
