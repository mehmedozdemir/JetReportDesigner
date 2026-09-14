import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Clock, Download, Loader2 } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { timeAgo } from "../time";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Background report jobs — tenant-wide, auto-refreshing while any are still in flight.
 * Reached from the Start screen like Team/Email: not tied to whatever report you have open. */
export function JobsPage() {
  const [jobs, setJobs] = useState<ReportJob[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const refresh = () => api.listJobs().then(setJobs).catch((e) => setErr(msg(e)));

  useEffect(() => {
    void refresh();
    const id = window.setInterval(() => void refresh(), 3000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const download = async (job: ReportJob) => {
    setDownloading(job.id);
    setErr(null);
    try {
      const blob = await api.downloadJobBlob(job.id);
      downloadBlob(blob, `${job.reportName || "report"}.${job.format}`);
    } catch (e) {
      setErr(msg(e));
    } finally {
      setDownloading(null);
    }
  };

  if (!jobs) {
    return (
      <section className="start-section">
        <div className="share-loading">
          <Loader2 size={16} className="spin" />
        </div>
      </section>
    );
  }

  return (
    <section className="start-section">
      <h3>Background jobs</h3>
      <p className="hint">Reports exported without waiting — status updates automatically.</p>

      {err && (
        <div className="error small">
          <AlertTriangle /> <span>{err}</span>
        </div>
      )}

      {jobs.length === 0 ? (
        <div className="start-empty">
          <Clock />
          <div>No background jobs yet.</div>
          <p>Right-click a report in the list and choose "Run in background" to see one here.</p>
        </div>
      ) : (
        <table className="drive-table">
          <thead>
            <tr>
              <th>Report</th>
              <th>Format</th>
              <th>Status</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {jobs.map((j) => (
              <tr key={j.id}>
                <td className="drive-table-name">{j.reportName}</td>
                <td><span className="chip">{j.format}</span></td>
                <td>
                  {j.status === "Queued" && (
                    <span className="job-status">
                      <Clock size={13} /> Queued
                    </span>
                  )}
                  {j.status === "Running" && (
                    <span className="job-status job-status-running">
                      <Loader2 size={13} className="spin" /> Running
                    </span>
                  )}
                  {j.status === "Succeeded" && (
                    <span className="job-status job-status-ok">
                      <CheckCircle2 size={13} /> Succeeded
                    </span>
                  )}
                  {j.status === "Failed" && (
                    <span className="job-status job-status-error" title={j.errorMessage ?? undefined}>
                      <AlertTriangle size={13} /> Failed
                    </span>
                  )}
                </td>
                <td>{timeAgo(j.createdAtUtc)}</td>
                <td>
                  {j.status === "Succeeded" && (
                    <button className="mini" onClick={() => void download(j)} disabled={downloading === j.id}>
                      <Download size={13} /> {downloading === j.id ? "…" : "Download"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
