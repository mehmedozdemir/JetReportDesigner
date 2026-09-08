using JetReportDesigner.Storage.Connections;

namespace JetReportDesigner.Api.Contracts;

public sealed record SqlQueryResponse(Guid Id, Guid ConnectionId, string Name, string CommandText, DateTime CreatedAtUtc)
{
    public static SqlQueryResponse From(SavedSqlQuery q) =>
        new(q.Id, q.ConnectionId, q.Name, q.CommandText, q.CreatedAtUtc);
}

public sealed record CreateSqlQueryRequest(Guid ConnectionId, string Name, string CommandText);

public sealed record UpdateSqlQueryRequest(string Name, string CommandText);
