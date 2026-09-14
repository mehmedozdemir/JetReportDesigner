using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class ReportJobConfiguration : IEntityTypeConfiguration<ReportJob>
{
    public void Configure(EntityTypeBuilder<ReportJob> builder)
    {
        builder.ToTable("ReportJobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.ReportName).HasMaxLength(256).IsRequired();
        builder.Property(j => j.Format).HasMaxLength(8).IsRequired();
        builder.Property(j => j.Status).HasMaxLength(16).IsRequired();
        builder.Property(j => j.ResultContentType).HasMaxLength(128);
        builder.Property(j => j.ResultFileName).HasMaxLength(256);

        builder.HasIndex(j => j.TenantId);
        builder.HasIndex(j => j.ReportId);
        builder.HasIndex(j => j.Status);
    }
}
