using System.ComponentModel.DataAnnotations;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>Bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Signing key. Must be at least 32 characters (HMAC-SHA256). Set via
    /// <c>Jwt__Secret</c> — never checked into <c>appsettings.json</c>.</summary>
    [Required, MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required] public string Issuer { get; init; } = "JetReportDesigner";

    [Required] public string Audience { get; init; } = "JetReportDesigner";

    [Range(1, 43200)] public int ExpiryMinutes { get; init; } = 480;
}
