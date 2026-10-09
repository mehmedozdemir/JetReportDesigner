namespace JetReportDesigner.Api.Contracts;

public sealed record TenantResponse(Guid Id, string Name, DateTime CreatedAtUtc);

/// <param name="Email">When given and the organization has a mail account, the invite is emailed there.</param>
public sealed record CreateInviteRequest(string Role, int? ExpiresInHours, string? Email = null);

public sealed record RenameTenantRequest(string Name);

/// <summary>What the organization can do right now — the UI hides email-only options when there is no mail account.</summary>
public sealed record TenantCapabilitiesResponse(bool EmailConfigured);

public sealed record InviteResponse(string Code, string Role, DateTime ExpiresAtUtc, string Link, string? Email = null, bool EmailSent = false);

public sealed record PendingInviteResponse(string Code, string Role, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string Link, string? Email = null);
