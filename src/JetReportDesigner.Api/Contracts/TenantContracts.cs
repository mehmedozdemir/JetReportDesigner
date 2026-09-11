namespace JetReportDesigner.Api.Contracts;

public sealed record TenantResponse(Guid Id, string Name, DateTime CreatedAtUtc);

public sealed record CreateInviteRequest(string Role, int? ExpiresInHours);

public sealed record InviteResponse(string Code, string Role, DateTime ExpiresAtUtc);

public sealed record PendingInviteResponse(string Code, string Role, DateTime CreatedAtUtc, DateTime ExpiresAtUtc);
