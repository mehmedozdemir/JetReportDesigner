import { useEffect, useState } from "react";
import { AlertTriangle, Bell, CheckCircle2, Clock, Download, Loader2, XCircle } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { notificationPermission, requestNotificationPermission } from "../notifications";
import { timeAgo } from "../time";
import { PageHeader } from "./PageHeader";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Background report jobs — tenant-wide, auto-refreshing while any are still in flight.
 * Reached from the Start screen like Team/Email: not tied to whatever report you have open.
 * (The actual notify-when-done logic lives in JobNotifications, mounted app-wide — this
 * page just offers the permission opt-in and the full history/status table.) */
export function JobsPage() {
  const [jobs, setJobs] = useState<ReportJob[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const [notifyPermission, setNotifyPermission] = useState(notificationPermission());

  const refresh = () => api.listJobs().then(setJobs).catch((e) => setErr(msg(e)));

  useEffect(() => {
    void refresh();
    const id = window.setInterval(() => void refresh(), 3000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const enableNotifications = async () => {
    setNotifyPermission(await requestNotificationPermission());
  };

  const cancel = async (job: ReportJob) => {
    setCancelling(job.id);
    setErr(null);
    try {
      await api.cancelJob(job.id);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setCancelling(null);
    }
  };

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
      <PageHeader
        title="Jobs"
        description="Reports exported without waiting — status updates automatically."
        actions={
          <>
            {notifyPermission === "default" && (
              <button className="mini" onClick={() => void enableNotifications()}>
                <Bell size={13} /> Enable browser notifications
              </button>
            )}
            {notifyPermission === "denied" && (
              <span className="hint" style={{ margin: 0 }}>
                Browser notifications blocked — allow them for this site to get notified.
              </span>
            )}
            {notifyPermission === "granted" && (
              <span className="hint" style={{ margin: 0, display: "flex", alignItems: "center", gap: 4 }}>
                <Bell size={13} /> Notifications on
              </span>
            )}
          </>
        }
      />

      {err && (
        <div className="error small">
          <AlertTriangle /> <span>{err}</span>
        </div>
      )}

      {jobs.length === 0 ? (
        <div className="start-empty">
          <Clock />
          <div>No background jobs yet.</div>
          <p>Open a report's ⋮ menu (or right-click it) in Reports and choose "Run in background".</p>
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
                  {j.status === "Cancelled" && (
                    <span className="job-status">
                      <XCircle size={13} /> Cancelled
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
                  {(j.status === "Queued" || j.status === "Running") && (
                    <button className="mini" onClick={() => void cancel(j)} disabled={cancelling === j.id}>
                      <XCircle size={13} /> {cancelling === j.id ? "…" : "Cancel"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {jobs.length >= 50 && (
        <p className="hint">
          Showing the 50 most recent. Older finished jobs are removed automatically after 30 days.
        </p>
      )}
    </section>
  );
}
