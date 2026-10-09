using System.Security.Claims;
using System.Text.Encodings.Web;
using JetReportDesigner.Storage.ApiKeys;
using JetReportDesigner.Storage.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JetReportDesigner.Api.Infrastructure;

public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// Authenticates a machine client by its <c>X-Api-Key</c> header. The key is looked up (by hash) among the
/// keys created on the API keys screen; it must be active and not expired. A key acts inside its own
/// tenant as a Viewer — it can list and render that tenant's reports but cannot create, edit or delete
/// anything, and it cannot manage API keys.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        var keys = Context.RequestServices.GetRequiredService<IApiKeyRepository>();
        var key = await keys.AuthenticateAsync(header.ToString().Trim(), Context.RequestAborted);
        if (key is null)
        {
            return AuthenticateResult.Fail("Invalid, disabled or expired API key.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, $"apikey:{key.Id}"),
                new Claim(ClaimTypes.Name, key.Name),
                new Claim("tenant", key.TenantId.ToString()),
                new Claim(ClaimTypes.Role, AppRole.Viewer),
            ],
            ApiKeyDefaults.Scheme);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.Scheme));
    }
}
