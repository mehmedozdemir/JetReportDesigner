using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class ReportShareConfiguration : IEntityTypeConfiguration<ReportShare>
{
    public void Configure(EntityTypeBuilder<ReportShare> builder)
    {
        builder.ToTable("ReportShares");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Token).HasMaxLength(64).IsRequired();

        builder.HasIndex(s => s.Token).IsUnique();
        builder.HasIndex(s => s.ReportId);
        builder.HasIndex(s => s.TenantId);
    }
}
