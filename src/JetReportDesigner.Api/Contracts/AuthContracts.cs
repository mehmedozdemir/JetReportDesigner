namespace JetReportDesigner.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAtUtc, UserResponse User);

public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

public sealed record SetRoleRequest(string Role);
