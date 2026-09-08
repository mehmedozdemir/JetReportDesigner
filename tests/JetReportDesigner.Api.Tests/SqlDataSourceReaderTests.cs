using System.Data.Common;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Sql;

namespace JetReportDesigner.Api.Tests;

/// <summary>
/// Exercises <see cref="SqlDataSourceReader"/> against the real container databases
/// used by the fixtures. Skips when Docker is unavailable.
/// </summary>
public abstract class SqlDataSourceReaderTestsBase(DatabaseFixture fixture, SqlProvider provider)
{
    private ConnectionStringResolver Resolver(Guid id) =>
        (requestedId, _) => Task.FromResult<(SqlProvider, string)?>(
            requestedId == id ? (provider, fixture.ConnectionString) : null);

    private static DataSourceDefinition SqlSource(string command, params SqlSourceParameter[] parameters) => new()
    {
        Name = "data",
        Kind = DataSourceKind.Sql,
        Sql = new SqlSourceConfig
        {
            Connection = "primary",
            CommandText = command,
            Parameters = [.. parameters],
            TimeoutSeconds = 15,
            MaxRows = 10,
        },
    };

    private async Task SeedAsync(DbConnection connection)
    {
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = "DROP TABLE reader_test";
        try
        {
            await drop.ExecuteNonQueryAsync();
        }
        catch (DbException)
        {
            // table did not exist
        }

        await using var create = connection.CreateCommand();
        create.CommandText = provider == SqlProvider.PostgreSql
            ? "CREATE TABLE reader_test (id int, name varchar(50), total numeric(10,2))"
            : "CREATE TABLE reader_test (id int, name varchar(50), total decimal(10,2))";
        await create.ExecuteNonQueryAsync();

        foreach (var (id, name, total) in new[] { (1, "Acme", 100m), (2, "Borg", 250m), (3, "Cyberdyne", 75m) })
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = $"INSERT INTO reader_test (id, name, total) VALUES ({id}, '{name}', {total})";
            await insert.ExecuteNonQueryAsync();
        }
    }

    private DbConnection NewConnection() => provider switch
    {
        SqlProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString),
        SqlProvider.PostgreSql => new Npgsql.NpgsqlConnection(fixture.ConnectionString),
        _ => throw new NotSupportedException(),
    };

    [Fact]
    public async Task Reads_Rows_With_A_Bound_Parameter()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using (var seed = NewConnection())
        {
            await SeedAsync(seed);
        }

        var id = Guid.NewGuid();
        var reader = new SqlDataSourceReader(Resolver(id));
        var paramName = provider == SqlProvider.PostgreSql ? "@min" : "@min";
        var context = new DataSourceReadContext(
            new Dictionary<string, object?> { ["min"] = 90 },
            [new ConnectionRef { Name = "primary", ConnectionId = id, Provider = provider }]);

        var set = await reader.ReadAsync(
            SqlSource($"SELECT id, name, total FROM reader_test WHERE total >= {paramName} ORDER BY id",
                new SqlSourceParameter { Name = "min", Value = "{param:min}" }),
            context,
            CancellationToken.None);

        Assert.Equal(2, set.Rows.Count);
        Assert.Equal("Acme", Convert.ToString(set.Rows[0]["name"]));
        Assert.Equal(FieldType.Number, set.Fields.Single(f => f.Name == "total").Type);
    }

    [Fact]
    public async Task Refuses_A_Non_Select_Command()
    {
        if (!fixture.Available)
        {
            return;
        }

        var id = Guid.NewGuid();
        var reader = new SqlDataSourceReader(Resolver(id));
        var context = new DataSourceReadContext(
            new Dictionary<string, object?>(),
            [new ConnectionRef { Name = "primary", ConnectionId = id, Provider = provider }]);

        await Assert.ThrowsAsync<UnsafeSqlCommandException>(() =>
            reader.ReadAsync(SqlSource("DELETE FROM reader_test"), context, CancellationToken.None));
    }
}

public sealed class SqlServerSqlReaderTests(SqlServerDatabaseFixture fixture)
    : SqlDataSourceReaderTestsBase(fixture, SqlProvider.SqlServer), IClassFixture<SqlServerDatabaseFixture>
{
}

public sealed class PostgreSqlSqlReaderTests(PostgreSqlDatabaseFixture fixture)
    : SqlDataSourceReaderTestsBase(fixture, SqlProvider.PostgreSql), IClassFixture<PostgreSqlDatabaseFixture>
{
}
