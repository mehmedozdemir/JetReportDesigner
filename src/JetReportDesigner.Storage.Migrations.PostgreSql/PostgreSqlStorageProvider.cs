using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Migrations.PostgreSql;

public sealed class PostgreSqlStorageProvider : IStorageProvider
{
    private const string MigrationsAssembly = "JetReportDesigner.Storage.Migrations.PostgreSql";

    public SqlProvider Provider => SqlProvider.PostgreSql;

    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(MigrationsAssembly));
}
