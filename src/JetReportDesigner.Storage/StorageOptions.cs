using JetReportDesigner.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage;

/// <summary>Bound from the <c>Storage</c> configuration section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public SqlProvider Provider { get; set; } = SqlProvider.SqlServer;

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Apply pending EF Core migrations on startup. Convenient for dev; disable for controlled deployments.</summary>
    public bool MigrateOnStartup { get; set; }
}

/// <summary>
/// Implemented once per provider assembly (SqlServer, PostgreSql, Oracle) so the
/// provider-specific EF Core packages stay out of the core Storage project.
/// </summary>
public interface IStorageProvider
{
    SqlProvider Provider { get; }

    void Configure(DbContextOptionsBuilder builder, string connectionString);
}
