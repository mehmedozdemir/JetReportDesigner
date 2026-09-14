using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("ReportSchedules");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.ReportName).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Format).HasMaxLength(8).IsRequired();
        builder.Property(s => s.Frequency).HasMaxLength(16).IsRequired();
        builder.Property(s => s.EmailRecipients).HasMaxLength(2000);

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.ReportId);
        builder.HasIndex(s => new { s.Enabled, s.NextRunAtUtc });
    }
}
