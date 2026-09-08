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
        base.OnModelCreating(modelBuilder);
    }
}
