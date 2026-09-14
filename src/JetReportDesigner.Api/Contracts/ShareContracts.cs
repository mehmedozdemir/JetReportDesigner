using JetReportDesigner.Storage.Sharing;

namespace JetReportDesigner.Api.Contracts;

public sealed record ShareResponse(string Token, DateTime CreatedAtUtc, string? CreatedByEmail)
{
    public static ShareResponse From(ReportShareInfo s) => new(s.Token, s.CreatedAtUtc, s.CreatedByEmail);
}

/// <summary>What an anonymous visitor with a share link is allowed to know: just enough to
/// render a preview/export page. Never the definition itself, never other tenant data.</summary>
public sealed record SharedReportResponse(string ReportName);
