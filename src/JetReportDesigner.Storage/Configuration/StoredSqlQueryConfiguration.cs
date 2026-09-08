using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class StoredSqlQueryConfiguration : IEntityTypeConfiguration<StoredSqlQuery>
{
    public void Configure(EntityTypeBuilder<StoredSqlQuery> builder)
    {
        builder.ToTable("SqlQueries");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.Name).HasMaxLength(200).IsRequired();
        builder.Property(q => q.CommandText).IsRequired();

        builder.HasIndex(q => q.ConnectionId);
        builder.HasIndex(q => new { q.ConnectionId, q.Name }).IsUnique();
    }
}
