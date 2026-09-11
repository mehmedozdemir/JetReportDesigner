namespace JetReportDesigner.Api.Infrastructure;

/// <summary>Authorization policy names. <see cref="Designer"/> gates every mutating endpoint
/// (create/update/delete reports, connections, SQL queries, assets, role changes); a plain
/// <c>[Authorize]</c> — any authenticated Designer or Viewer — gates everything else.</summary>
public static class AuthPolicies
{
    public const string Designer = "Designer";
}
