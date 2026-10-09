namespace JetReportDesigner.Api.Contracts;

/// <summary>Exactly one of <see cref="OrganizationName"/> (create a new tenant, becoming its
/// Designer) or <see cref="InviteCode"/> (join an existing tenant with the invite's role) must
/// be supplied.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string? OrganizationName,
    string? InviteCode,
    string? DisplayName = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAtUtc, UserResponse User);

/// <param name="LockedOut">True while the account is locked after too many wrong passwords.</param>
public sealed record UserResponse(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    string? DisplayName = null,
    DateTime? LastLoginAtUtc = null,
    bool LockedOut = false);

public sealed record SetRoleRequest(string Role);

public sealed record UpdateProfileRequest(string? DisplayName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>A link that lets one person set a new password, made by a Designer for a teammate.</summary>
public sealed record ResetLinkResponse(string Url, DateTime ExpiresAtUtc);

/// <summary>What the "join" page shows before the person signs up with an invite.</summary>
public sealed record InvitePreviewResponse(string OrganizationName, string Role, string? Email, DateTime ExpiresAtUtc);
