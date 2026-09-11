using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class TenantInviteConfiguration : IEntityTypeConfiguration<TenantInvite>
{
    public void Configure(EntityTypeBuilder<TenantInvite> builder)
    {
        builder.ToTable("TenantInvites");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Code).HasMaxLength(16).IsRequired();
        builder.Property(i => i.Role).HasMaxLength(16).IsRequired();

        builder.HasIndex(i => i.Code).IsUnique();
        builder.HasIndex(i => i.TenantId);
    }
}
