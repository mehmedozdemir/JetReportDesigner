using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Connections;

namespace JetReportDesigner.Api.Contracts;

/// <summary>Public view of a registered connection — no secret.</summary>
public sealed record ConnectionResponse(Guid Id, string Name, string Provider, DateTime CreatedAtUtc)
{
    public static ConnectionResponse From(RegisteredConnection c) =>
        new(c.Id, c.Name, ProviderString(c.Provider), c.CreatedAtUtc);

    private static string ProviderString(SqlProvider p) => p switch
    {
        SqlProvider.PostgreSql => "postgreSql",
        SqlProvider.Oracle => "oracle",
        _ => "sqlServer",
    };
}

public sealed record CreateConnectionRequest(string Name, string Provider, string ConnectionString);

/// <summary>A null <see cref="ConnectionString"/> leaves the stored secret unchanged.</summary>
public sealed record UpdateConnectionRequest(string Name, string Provider, string? ConnectionString);
