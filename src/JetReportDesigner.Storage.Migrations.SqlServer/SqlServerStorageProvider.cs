using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Migrations.SqlServer;

public sealed class SqlServerStorageProvider : IStorageProvider
{
    private const string MigrationsAssembly = "JetReportDesigner.Storage.Migrations.SqlServer";

    public SqlProvider Provider => SqlProvider.SqlServer;

    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(MigrationsAssembly));
}
