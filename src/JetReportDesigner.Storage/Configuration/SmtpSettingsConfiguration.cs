using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class SmtpSettingsConfiguration : IEntityTypeConfiguration<SmtpSettings>
{
    public void Configure(EntityTypeBuilder<SmtpSettings> builder)
    {
        builder.ToTable("SmtpSettings");

        builder.HasKey(s => s.TenantId);
        builder.Property(s => s.TenantId).ValueGeneratedNever();

        builder.Property(s => s.Host).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Security).HasMaxLength(16).IsRequired();
        builder.Property(s => s.Username).HasMaxLength(256).IsRequired();
        builder.Property(s => s.FromEmail).HasMaxLength(320).IsRequired();
        builder.Property(s => s.FromName).HasMaxLength(256);
    }
}
