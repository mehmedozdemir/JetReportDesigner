using System.Security.Claims;
using JetReportDesigner.Storage.Tenancy;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>Reads the signed-in user's tenant off the "tenant" claim on their JWT.</summary>
public sealed class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid TenantId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue("tenant");
            return Guid.TryParse(raw, out var id)
                ? id
                : throw new InvalidOperationException("No 'tenant' claim on the current user — is the request authenticated?");
        }
    }
}
