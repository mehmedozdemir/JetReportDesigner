namespace JetReportDesigner.Storage.Tenancy;

/// <summary>The tenant the current request is scoped to. Implemented in the API layer by
/// reading the "tenant" claim off the signed-in user's JWT (Storage itself has no
/// ASP.NET Core dependency and cannot read <c>ClaimsPrincipal</c> directly).</summary>
public interface ICurrentTenant
{
    Guid TenantId { get; }
}
