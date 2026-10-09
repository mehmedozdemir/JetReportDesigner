namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A credential an external application (a desktop app, a service) uses to call the API in place of a
/// signed-in user. Only the SHA-256 of the key is stored — the key itself is shown once, at creation.
/// A key acts inside one tenant with read-and-render rights (the Viewer role) and can be switched off,
/// given an expiry, or deleted at any time.
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Who or what this key is for, e.g. "Warehouse label printer app".</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>SHA-256 of the key, upper-case hex. Unique.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First characters of the key, kept so people can tell keys apart ("jrd_a1b2c3d4"). Not secret.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>The key stops working at this instant. Null = never expires.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByEmail { get; set; }

    /// <summary>Last successful use (updates are throttled to about once a minute).</summary>
    public DateTime? LastUsedAtUtc { get; set; }
}
