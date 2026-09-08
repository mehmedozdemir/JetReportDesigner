using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage;

public sealed class JetReportDbContext(DbContextOptions<JetReportDbContext> options) : DbContext(options)
{
    public DbSet<StoredReport> Reports => Set<StoredReport>();

    public DbSet<StoredConnection> Connections => Set<StoredConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JetReportDbContext).Assembly);

        // Oracle maps an unbounded string to NVARCHAR2(2000); the report JSON and
        // connection secrets need a LOB. SQL Server (nvarchar(max)) and PostgreSQL
        // (text) already map unbounded strings correctly.
        if (Database.ProviderName?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<Entities.StoredReport>().Property(r => r.DefinitionJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Entities.StoredConnection>().Property(c => c.EncryptedConnectionString).HasColumnType("NCLOB");
        }

        base.OnModelCreating(modelBuilder);
    }
}
