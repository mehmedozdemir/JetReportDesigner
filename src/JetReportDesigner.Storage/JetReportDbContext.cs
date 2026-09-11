using JetReportDesigner.Storage.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage;

public sealed class JetReportDbContext(DbContextOptions<JetReportDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<StoredReport> Reports => Set<StoredReport>();

    public DbSet<StoredReportVersion> ReportVersions => Set<StoredReportVersion>();

    public DbSet<StoredConnection> Connections => Set<StoredConnection>();

    public DbSet<StoredSqlQuery> SqlQueries => Set<StoredSqlQuery>();

    public DbSet<StoredAsset> Assets => Set<StoredAsset>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantInvite> TenantInvites => Set<TenantInvite>();

    public DbSet<Folder> Folders => Set<Folder>();

    public DbSet<ReportFolderEntry> ReportFolderEntries => Set<ReportFolderEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JetReportDbContext).Assembly);

        // Rename the default "AspNetX" Identity tables to match this app's PascalCase,
        // unprefixed naming (Reports, Connections, …).
        modelBuilder.Entity<AppUser>().ToTable("Users");
        modelBuilder.Entity<AppUser>().Property(u => u.TenantId).IsRequired();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.TenantId);
        modelBuilder.Entity<AppRole>().ToTable("Roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        // Oracle maps an unbounded string to NVARCHAR2(2000); the report JSON and
        // connection secrets need a LOB. SQL Server (nvarchar(max)) and PostgreSQL
        // (text) already map unbounded strings correctly.
        if (Database.ProviderName?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<Entities.StoredReport>().Property(r => r.DefinitionJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Entities.StoredReportVersion>().Property(v => v.DefinitionJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Entities.StoredConnection>().Property(c => c.EncryptedConnectionString).HasColumnType("NCLOB");
            modelBuilder.Entity<Entities.StoredSqlQuery>().Property(q => q.CommandText).HasColumnType("NCLOB");
            modelBuilder.Entity<Entities.StoredAsset>().Property(a => a.Content).HasColumnType("BLOB");
        }
    }
}
