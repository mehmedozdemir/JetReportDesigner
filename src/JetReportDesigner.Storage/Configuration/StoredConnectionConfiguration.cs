using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class StoredConnectionConfiguration : IEntityTypeConfiguration<StoredConnection>
{
    public void Configure(EntityTypeBuilder<StoredConnection> builder)
    {
        builder.ToTable("Connections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Provider).HasMaxLength(16).IsRequired();
        builder.Property(c => c.EncryptedConnectionString).IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
    }
}
