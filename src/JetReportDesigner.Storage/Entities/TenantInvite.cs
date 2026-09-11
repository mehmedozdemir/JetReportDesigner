namespace JetReportDesigner.Storage.Entities;

/// <summary>A one-time code a Designer generates so someone else can join their tenant
/// (with a chosen role) via <c>POST /api/auth/register</c> instead of creating a new tenant.</summary>
public sealed class TenantInvite
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Short, unique, case-insensitive code shared out-of-band (email, chat, …).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The role — <c>AppRole.Designer</c> or <c>AppRole.Viewer</c> — the joining user gets.</summary>
    public string Role { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set once the invite is redeemed; a used invite can never be redeemed again.</summary>
    public DateTime? UsedAtUtc { get; set; }

    public Guid? UsedByUserId { get; set; }
}
