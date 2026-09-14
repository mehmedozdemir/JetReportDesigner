namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A public, unauthenticated link to a single report's rendered output (preview + export
/// only — never the design surface). Anyone with the token can view/export the report as
/// it currently stands; no login required. <see cref="ReportId"/> is a plain denormalised
/// column (no EF-modeled FK/cascade), matching the existing StoredSqlQuery.ConnectionId
/// convention — a share simply stops resolving once its report is deleted.
/// </summary>
public sealed class ReportShare
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    /// <summary>Unguessable URL segment (32 hex chars — 128 bits of entropy). The only credential.</summary>
    public string Token { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public string? CreatedByEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
