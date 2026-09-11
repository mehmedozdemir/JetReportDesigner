namespace JetReportDesigner.Storage.Entities;

/// <summary>An organization/workspace. Every user, report, connection, SQL query and asset
/// belongs to exactly one tenant; nothing is ever visible across tenants.</summary>
public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
