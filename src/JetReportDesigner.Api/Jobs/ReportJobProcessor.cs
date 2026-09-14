using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Api.Infrastructure.Email;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Email;
using JetReportDesigner.Storage.Jobs;
using JetReportDesigner.Storage.Repositories;
using JetReportDesigner.Storage.Sharing;
using JetReportDesigner.Storage.Schedules;

namespace JetReportDesigner.Api.Jobs;

/// <summary>
/// Polls <see cref="IReportJobRepository"/> for queued render jobs and runs them one at a
/// time — the "queue this instead of making the caller wait" mechanism, and the execution
/// engine a future report scheduler reuses. A DB-backed queue rather than a message broker:
/// this is one process talking to its own database, not cross-service messaging, so the
/// broker's guarantees (routing, competing consumers, replay) buy nothing here — see
/// docs/01, "Rapor zamanlama/dağıtım" for the reasoning.
/// </summary>
internal sealed class ReportJobProcessor(IServiceScopeFactory scopeFactory, ILogger<ReportJobProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverStuckJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork;
            try
            {
                didWork = await ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The supervisor loop must never die — a single bad job or a transient DB
                // blip should not stop every future job from ever running.
                logger.LogError(ex, "Report job processing loop failed");
                didWork = false;
            }

            if (!didWork)
            {
                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RecoverStuckJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IReportJobRepository>();
        await jobs.RecoverStuckAsync(cancellationToken);
    }

    /// <returns>Whether a job was claimed (so the caller can skip its idle delay and drain the queue).</returns>
    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IReportJobRepository>();

        var claimed = await jobs.ClaimNextAsync(cancellationToken);
        if (claimed is null)
        {
            return false;
        }

        // Everything resolved from here down goes through the normal, tenant-scoped
        // repositories (reports, assets for images, connections for SQL data sources, …)
        // exactly as an HTTP request would — CurrentTenant.Use makes them see this job's
        // tenant with no special-casing anywhere else in the render pipeline.
        using (CurrentTenant.Use(claimed.TenantId))
        {
            try
            {
                var reports = scope.ServiceProvider.GetRequiredService<IReportRepository>();
                var record = await reports.GetAsync(claimed.ReportId, cancellationToken);
                if (record is null)
                {
                    await jobs.FailAsync(claimed.Id, "The report no longer exists.", cancellationToken);
                    return true;
                }

                var renderer = scope.ServiceProvider.GetRequiredService<ReportRenderService>();
                var format = claimed.Format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? RenderFormat.Xlsx : RenderFormat.Pdf;
                var result = await renderer.RenderAsync(record.Definition, parameters: null, format, cancellationToken);

                await jobs.CompleteAsync(claimed.Id, result.Content, result.ContentType, result.FileName, cancellationToken);

                if (claimed.ScheduleId is { } scheduleId)
                {
                    await DistributeAsync(scope, claimed, scheduleId, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report job {JobId} failed", claimed.Id);
                await jobs.FailAsync(claimed.Id, ex.Message, cancellationToken);
            }
        }

        return true;
    }

    /// <summary>A schedule-triggered job's post-success actions — never lets a distribution
    /// failure (bad SMTP creds, a revoked share, …) undo the job's own success.</summary>
    private async Task DistributeAsync(IServiceScope scope, ClaimedReportJob claimed, Guid scheduleId, CancellationToken cancellationToken)
    {
        try
        {
            var schedules = scope.ServiceProvider.GetRequiredService<IReportScheduleRepository>();
            var settings = await schedules.GetDistributionSettingsAsync(scheduleId, cancellationToken);
            if (settings is null)
            {
                return;
            }

            if (settings.CreateShareLink)
            {
                var shares = scope.ServiceProvider.GetRequiredService<IReportShareRepository>();
                await shares.CreateAsync(settings.ReportId, settings.CreatedByUserId, createdByEmail: null, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(settings.EmailRecipients))
            {
                await EmailResultAsync(scope, claimed, settings, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Distributing schedule {ScheduleId} (job {JobId}) failed", scheduleId, claimed.Id);
        }
    }

    private async Task EmailResultAsync(IServiceScope scope, ClaimedReportJob claimed, ScheduleDistributionSettings settings, CancellationToken cancellationToken)
    {
        var smtp = scope.ServiceProvider.GetRequiredService<ISmtpSettingsRepository>();
        var account = await smtp.GetForTenantAsync(claimed.TenantId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("Schedule wants to email its result but no mail account is configured for tenant {TenantId}", claimed.TenantId);
            return;
        }

        var jobs = scope.ServiceProvider.GetRequiredService<IReportJobRepository>();
        var result = await jobs.GetResultAsync(claimed.Id, cancellationToken);
        if (result is null)
        {
            return;
        }

        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var attachment = new EmailAttachment(result.FileName, result.ContentType, result.Content);
        var addresses = settings.EmailRecipients!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var to in addresses)
        {
            var email = new OutgoingEmail(
                to,
                $"Scheduled report — {settings.ReportName}",
                $"<p>Your scheduled report &ldquo;{System.Net.WebUtility.HtmlEncode(settings.ReportName)}&rdquo; is attached.</p>",
                [attachment]);
            await sender.SendAsync(account, email, cancellationToken);
        }
    }
}
