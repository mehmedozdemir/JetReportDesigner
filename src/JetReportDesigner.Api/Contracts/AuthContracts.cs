namespace JetReportDesigner.Api.Contracts;

/// <summary>Exactly one of <see cref="OrganizationName"/> (create a new tenant, becoming its
/// Designer) or <see cref="InviteCode"/> (join an existing tenant with the invite's role) must
/// be supplied.</summary>
public sealed record RegisterRequest(string Email, string Password, string? OrganizationName, string? InviteCode);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAtUtc, UserResponse User);

public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

public sealed record SetRoleRequest(string Role);
