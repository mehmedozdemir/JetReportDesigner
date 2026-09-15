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
internal sealed class ReportJobProcessor(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider clock,
    RunningJobs runningJobs,
    ILogger<ReportJobProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    /// <summary>Days a finished job (and its stored result) is kept — `Jobs:RetentionDays`,
    /// 0 or less to keep everything. Every job row holds a rendered report, so a daily schedule
    /// left running would otherwise grow the database without limit.</summary>
    private int RetentionDays => configuration.GetValue("Jobs:RetentionDays", 30);

    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverStuckJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepExpiredJobsAsync(stoppingToken);

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

    /// <summary>Runs at most every <see cref="SweepInterval"/>, and never lets a failure here
    /// stop jobs from being processed — housekeeping is not worth taking the queue down for.</summary>
    private async Task SweepExpiredJobsAsync(CancellationToken cancellationToken)
    {
        var retentionDays = RetentionDays;
        if (retentionDays <= 0 || clock.GetUtcNow() - _lastSweep < SweepInterval)
        {
            return;
        }

        _lastSweep = clock.GetUtcNow();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IReportJobRepository>();
            var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
            var deleted = await jobs.DeleteFinishedBeforeAsync(cutoff, cancellationToken);
            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} report job(s) finished before {Cutoff:u}", deleted, cutoff);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sweeping expired report jobs failed");
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
        // A token of this job's own, so "cancel this job" can stop the render without taking
        // the whole worker down with it; linked to the host's so shutdown still stops everything.
        //
        // How far cancellation actually reaches: ReportRenderService threads the token through
        // its async phases — resolving data (the SQL/REST fetch that dominates a big report),
        // images and subreports — so a job stuck pulling data stops promptly. The CPU-bound
        // layout and PDF emit that follow don't check it, so a job already past the data stage
        // runs to completion and reports Succeeded. That's why the API answers a cancel on a
        // running job with 202 (asked to stop) rather than 204.
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var tracked = runningJobs.Track(claimed.Id, jobCts);
        var jobToken = jobCts.Token;

        using (CurrentTenant.Use(claimed.TenantId))
        {
            try
            {
                var reports = scope.ServiceProvider.GetRequiredService<IReportRepository>();
                var record = await reports.GetAsync(claimed.ReportId, jobToken);
                if (record is null)
                {
                    await jobs.FailAsync(claimed.Id, "The report no longer exists.", cancellationToken);
                    return true;
                }

                var renderer = scope.ServiceProvider.GetRequiredService<ReportRenderService>();
                var format = claimed.Format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? RenderFormat.Xlsx : RenderFormat.Pdf;
                var result = await renderer.RenderAsync(record.Definition, parameters: null, format, jobToken);

                await jobs.CompleteAsync(claimed.Id, result.Content, result.ContentType, result.FileName, cancellationToken);

                if (claimed.ScheduleId is { } scheduleId)
                {
                    await DistributeAsync(scope, claimed, scheduleId, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Cancelled by a user, not by shutdown — record it as such. (On shutdown the job
                // is deliberately left Running, and RecoverStuckAsync requeues it next start.)
                logger.LogInformation("Report job {JobId} cancelled", claimed.Id);
                await jobs.MarkCancelledAsync(claimed.Id, CancellationToken.None);
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
        var schedules = scope.ServiceProvider.GetRequiredService<IReportScheduleRepository>();
        var settings = await schedules.GetDistributionSettingsAsync(scheduleId, cancellationToken);
        if (settings is null || (!settings.CreateShareLink && string.IsNullOrWhiteSpace(settings.EmailRecipients)))
        {
            // Nothing to distribute — a "history only" schedule never touches
            // LastDistributionAtUtc, so the UI can tell "never attempted" from "succeeded".
            return;
        }

        try
        {
            if (settings.CreateShareLink)
            {
                var shares = scope.ServiceProvider.GetRequiredService<IReportShareRepository>();
                await shares.CreateAsync(settings.ReportId, settings.CreatedByUserId, createdByEmail: null, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(settings.EmailRecipients))
            {
                await EmailResultAsync(scope, claimed, settings, cancellationToken);
            }

            await schedules.RecordDistributionResultAsync(scheduleId, error: null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Distributing schedule {ScheduleId} (job {JobId}) failed", scheduleId, claimed.Id);
            await schedules.RecordDistributionResultAsync(scheduleId, ex.Message, cancellationToken);
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
