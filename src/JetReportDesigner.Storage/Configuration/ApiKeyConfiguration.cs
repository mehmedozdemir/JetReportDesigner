using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.Name).HasMaxLength(200).IsRequired();
        builder.Property(k => k.Description).HasMaxLength(1000);
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(24).IsRequired();
        builder.Property(k => k.CreatedByEmail).HasMaxLength(256);

        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.TenantId);
    }
}
