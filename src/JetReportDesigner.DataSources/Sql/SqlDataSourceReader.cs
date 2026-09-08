using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources.Sql;

/// <summary>Resolves a registered connection id to its provider and (decrypted) connection string.</summary>
public delegate Task<(SqlProvider Provider, string ConnectionString)?> ConnectionStringResolver(
    Guid connectionId,
    CancellationToken cancellationToken);

/// <summary>
/// Reads a SQL data source: a parameterised SELECT against a registered connection.
/// Enforces read-only (<see cref="SqlCommandGuard"/>), a row cap and a command
/// timeout. Parameters are always bound, never concatenated.
/// </summary>
public sealed partial class SqlDataSourceReader(ConnectionStringResolver resolveConnection) : IDataSourceReader
{
    [GeneratedRegex(@"^\{param:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}$")]
    private static partial Regex SingleParam();

    public DataSourceKind Kind => DataSourceKind.Sql;

    public async Task<ResolvedDataSet> ReadAsync(
        DataSourceDefinition definition,
        DataSourceReadContext context,
        CancellationToken cancellationToken)
    {
        var config = definition.Sql;
        if (config is null || string.IsNullOrWhiteSpace(config.CommandText))
        {
            return ResolvedDataSet.Empty;
        }

        SqlCommandGuard.EnsureSelectOnly(config.CommandText);

        var connectionRef = context.Connections.FirstOrDefault(c =>
            string.Equals(c.Name, config.Connection, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The report has no connection named '{config.Connection}'.");

        var resolved = await resolveConnection(connectionRef.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Connection '{config.Connection}' ({connectionRef.ConnectionId}) is not registered.");

        await using var connection = CreateConnection(resolved.Provider, resolved.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = config.CommandText;
        command.CommandTimeout = Math.Clamp(config.TimeoutSeconds, 1, 600);

        foreach (var parameter in config.Parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = parameter.Name.TrimStart('@', ':');
            p.Value = ResolveParameterValue(parameter.Value, context.Parameters) ?? DBNull.Value;
            command.Parameters.Add(p);
        }

        var maxRows = Math.Clamp(config.MaxRows, 1, 1_000_000);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var types = Enumerable.Range(0, reader.FieldCount).Select(reader.GetFieldType).ToArray();

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (rows.Count < maxRows && await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        var fields = columns
            .Select((name, i) => new DataField { Name = name, Type = MapType(types[i]) })
            .ToList();

        return new ResolvedDataSet(rows, fields);
    }

    private static object? ResolveParameterValue(string configuredValue, IReadOnlyDictionary<string, object?> parameters)
    {
        var match = SingleParam().Match(configuredValue ?? string.Empty);
        if (match.Success)
        {
            return parameters.TryGetValue(match.Groups["name"].Value, out var value) ? value : null;
        }

        return ParameterInterpolation.Apply(configuredValue, parameters);
    }

    private static DbConnection CreateConnection(SqlProvider provider, string connectionString) => provider switch
    {
        SqlProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
        SqlProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
        SqlProvider.Oracle => new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString),
        _ => throw new NotSupportedException($"Unsupported SQL provider '{provider}'."),
    };

    private static FieldType MapType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => FieldType.Boolean,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => FieldType.Number,
            TypeCode.DateTime => FieldType.DateTime,
            _ => type == typeof(DateOnly) ? FieldType.Date
                : type == typeof(DateTimeOffset) ? FieldType.DateTime
                : FieldType.String,
        };
    }
}
