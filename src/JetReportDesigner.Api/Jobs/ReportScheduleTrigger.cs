using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Jobs;
using JetReportDesigner.Storage.Schedules;

namespace JetReportDesigner.Api.Jobs;

/// <summary>
/// Checks every 30s for <see cref="Storage.Entities.ReportSchedule"/>s whose time has come
/// and enqueues a <see cref="ReportJob"/> for each — the schedule itself does nothing else;
/// <see cref="ReportJobProcessor"/> renders the job and, seeing it carries a schedule id,
/// handles distribution once it succeeds.
/// </summary>
internal sealed class ReportScheduleTrigger(IServiceScopeFactory scopeFactory, ILogger<ReportScheduleTrigger> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TriggerDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report schedule trigger failed");
            }

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

    private async Task TriggerDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var schedules = scope.ServiceProvider.GetRequiredService<IReportScheduleRepository>();
        var due = await schedules.ClaimDueAsync(cancellationToken);

        foreach (var schedule in due)
        {
            using (CurrentTenant.Use(schedule.TenantId))
            {
                try
                {
                    var jobs = scope.ServiceProvider.GetRequiredService<IReportJobRepository>();
                    var job = await jobs.EnqueueAsync(schedule.ReportId, schedule.Format, schedule.CreatedByUserId, cancellationToken, schedule.Id);
                    if (job is null)
                    {
                        logger.LogWarning("Schedule {ScheduleId} fired but its report no longer exists", schedule.Id);
                    }
                    else
                    {
                        // "Last run" tracks the last time the schedule fired, not the last
                        // success — a failed job should still show up, not hide behind a stale date.
                        await schedules.RecordRunAsync(schedule.Id, job.Id, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Enqueueing schedule {ScheduleId}'s job failed", schedule.Id);
                }
            }
        }
    }
}
