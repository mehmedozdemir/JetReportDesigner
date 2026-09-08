namespace JetReportDesigner.Storage.Entities;

/// <summary>A reusable SELECT query saved against a registered <see cref="StoredConnection"/>.</summary>
public sealed class StoredSqlQuery
{
    public Guid Id { get; set; }

    public Guid ConnectionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CommandText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
