using Microsoft.AspNetCore.Identity;

namespace JetReportDesigner.Storage.Entities;

/// <summary>An application user. Authentication is username/password (via Identity's
/// password hasher) exchanged for a JWT — no cookie sign-in is used.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>The tenant (organization) this user belongs to. Set once at registration —
    /// either a brand-new tenant, or the tenant of the invite code they registered with.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The person's name as shown to the team ("Ayşe Yılmaz"). Null for accounts made before names existed.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Last successful sign-in.</summary>
    public DateTime? LastLoginAtUtc { get; set; }
}
