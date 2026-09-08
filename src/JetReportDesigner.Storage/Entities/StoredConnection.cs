namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A registered database connection usable by SQL data sources. The connection
/// string is stored encrypted (ASP.NET Data Protection) and never returned in clear
/// text by the API.
/// </summary>
public sealed class StoredConnection
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>"sqlServer" | "postgreSql" | "oracle".</summary>
    public string Provider { get; set; } = string.Empty;

    public string EncryptedConnectionString { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
