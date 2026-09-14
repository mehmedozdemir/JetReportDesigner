using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Jobs;
using JetReportDesigner.Storage.Repositories;

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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report job {JobId} failed", claimed.Id);
                await jobs.FailAsync(claimed.Id, ex.Message, cancellationToken);
            }
        }

        return true;
    }
}
