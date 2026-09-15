using System.Collections.Concurrent;

namespace JetReportDesigner.Api.Jobs;

/// <summary>
/// The cancellation tokens of jobs currently rendering, so a cancel request can actually stop
/// the work instead of only marking a row. In-memory on purpose: the queue is deliberately a
/// single process talking to its own database (see <see cref="ReportJobProcessor"/>), so the
/// worker and the controller are always in the same process. If that ever stops being true,
/// cancelling a job running on another node would need a flag in the database instead.
/// </summary>
public sealed class RunningJobs
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public IDisposable Track(Guid jobId, CancellationTokenSource cts)
    {
        _running[jobId] = cts;
        return new Registration(this, jobId);
    }

    /// <returns>Whether the job was still running and has now been asked to stop.</returns>
    public bool Cancel(Guid jobId)
    {
        if (!_running.TryGetValue(jobId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        return true;
    }

    private sealed class Registration(RunningJobs owner, Guid jobId) : IDisposable
    {
        public void Dispose() => owner._running.TryRemove(jobId, out _);
    }
}
