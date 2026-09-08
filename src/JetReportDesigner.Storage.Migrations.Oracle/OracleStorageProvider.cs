using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Migrations.Oracle;

public sealed class OracleStorageProvider : IStorageProvider
{
    private const string MigrationsAssembly = "JetReportDesigner.Storage.Migrations.Oracle";

    public SqlProvider Provider => SqlProvider.Oracle;

    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseOracle(connectionString, oracle => oracle.MigrationsAssembly(MigrationsAssembly));
}
