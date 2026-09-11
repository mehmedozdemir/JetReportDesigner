using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JetReportDesigner.Storage.Configuration;

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("Folders");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();

        // ParentFolderId is a plain denormalised reference (no EF-modeled FK/cascade), same
        // convention as StoredSqlQuery.ConnectionId — deletion of a non-empty folder is blocked
        // at the repository level, so no cascade behavior is ever exercised.
        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => new { f.TenantId, f.ParentFolderId });
    }
}
