using System.Security.Claims;
using JetReportDesigner.Storage.Tenancy;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>
/// Reads the signed-in user's tenant off the "tenant" claim on their JWT. Background work
/// (the report job worker, and eventually the scheduler) has no HTTP request and therefore
/// no claim — but it always knows exactly which tenant it's acting for, so it sets that
/// explicitly via <see cref="Use"/> around the job. Every tenant-scoped repository
/// (reports, assets, connections, …) stays unchanged: it just asks <see cref="ICurrentTenant"/>
/// for the id, unaware of which of the two sources it came from.
/// </summary>
public sealed class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    private static readonly AsyncLocal<Guid?> Ambient = new();

    /// <summary>Sets the tenant for the duration of the returned scope — for background work
    /// with no signed-in HTTP request. Flows across <c>await</c>s like any AsyncLocal; does
    /// not leak into unrelated concurrent work. An HTTP request's own JWT claim always wins
    /// over this, so it's safe even if somehow called during a request.</summary>
    public static IDisposable Use(Guid tenantId)
    {
        var previous = Ambient.Value;
        Ambient.Value = tenantId;
        return new Restorer(previous);
    }

    private sealed class Restorer(Guid? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }

    public Guid TenantId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue("tenant");
            if (Guid.TryParse(raw, out var id))
            {
                return id;
            }

            return Ambient.Value
                ?? throw new InvalidOperationException("No 'tenant' claim on the current user — is the request authenticated?");
        }
    }
}
