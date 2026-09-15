using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Localization;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Api.Infrastructure.Email;
using JetReportDesigner.Storage.Email;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>The tenant's outgoing-mail account, for scheduled-report distribution.
/// Designer-only, like every other tenant-wide setting (connections, invites).</summary>
[ApiController]
[Route("api/email-settings")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.Designer)]
public sealed class EmailSettingsController(ISmtpSettingsRepository settings, IEmailSender sender, ICurrentTenant tenant, IApiStrings strings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SmtpSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var found = await settings.GetAsync(cancellationToken);
        return found is null ? NotFound() : Ok(SmtpSettingsResponse.From(found));
    }

    [HttpPut]
    public async Task<ActionResult<SmtpSettingsResponse>> Set([FromBody] SetSmtpSettingsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.FromEmail))
        {
            return ValidationProblem(strings["email.hostAndFromRequired"]);
        }

        if (request.Port is < 1 or > 65535)
        {
            return ValidationProblem(strings["email.portRange"]);
        }

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);
        var result = await settings.SetAsync(
            request.Host.Trim(),
            request.Port,
            request.Security,
            request.Username.Trim(),
            request.Password,
            request.FromEmail.Trim(),
            request.FromName,
            userId,
            cancellationToken);

        return Ok(SmtpSettingsResponse.From(result));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken) =>
        await settings.DeleteAsync(cancellationToken) ? NoContent() : NotFound();

    /// <summary>Sends a real message — through the saved account, or through
    /// <paramref name="request"/>'s own fields when it carries a <c>Host</c>, so the settings
    /// screen can test what's in the form before Save.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailRequest request, CancellationToken cancellationToken)
    {
        SmtpSettingsForSending? forSending;
        if (!string.IsNullOrWhiteSpace(request.Host))
        {
            var password = request.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                var saved = await settings.GetForTenantAsync(tenant.TenantId, cancellationToken);
                password = saved?.Password;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return ValidationProblem(strings["email.passwordNeededForTest"]);
            }

            forSending = new SmtpSettingsForSending(
                request.Host.Trim(),
                request.Port ?? 587,
                request.Security ?? "StartTls",
                (request.Username ?? string.Empty).Trim(),
                password,
                (request.FromEmail ?? string.Empty).Trim(),
                request.FromName);
        }
        else
        {
            forSending = await settings.GetForTenantAsync(tenant.TenantId, cancellationToken);
        }

        if (forSending is null)
        {
            return ValidationProblem(strings["email.saveBeforeTest"]);
        }

        var email = new OutgoingEmail(
            request.ToEmail,
            "JetReportDesigner — test email",
            "<p>This is a test email from your JetReportDesigner mail account settings.</p>");

        try
        {
            await sender.SendAsync(forSending, email, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately broad: this endpoint's entire purpose is to surface *why* a
            // third-party mail server rejected the connection (bad host, auth, TLS, timeout —
            // MailKit/the OS network stack throw a wide variety of exception types for these),
            // as an actionable message instead of a bare 500. The client's problemMessage()
            // helper prefers "title" over "detail", so the actionable text goes in title.
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: strings.Format("email.testFailed", ex.Message));
        }
    }
}
