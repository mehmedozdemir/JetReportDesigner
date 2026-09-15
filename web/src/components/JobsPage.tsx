import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, Bell, CalendarClock, CheckCircle2, Clock, Download, Loader2, User, XCircle } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { notificationPermission, requestNotificationPermission } from "../notifications";
import { timeAgo } from "../time";
import { PageHeader } from "./PageHeader";
import { SortableTh, useSort } from "./SortableTh";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Background report jobs — tenant-wide, auto-refreshing while any are still in flight.
 * Reached from the Start screen like Team/Email: not tied to whatever report you have open.
 * (The actual notify-when-done logic lives in JobNotifications, mounted app-wide — this
 * page just offers the permission opt-in and the full history/status table.) */
export function JobsPage() {
  const { t } = useTranslation();
  const [jobs, setJobs] = useState<ReportJob[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const { sort, toggle, apply } = useSort<"reportName" | "format" | "status" | "createdAtUtc">({
    key: "createdAtUtc",
    dir: "desc",
  });
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
        title={t("jobs.title")}
        description={t("jobs.description")}
        actions={
          <>
            {notifyPermission === "default" && (
              <button className="mini" onClick={() => void enableNotifications()}>
                <Bell size={13} /> {t("jobs.enableNotifications")}
              </button>
            )}
            {notifyPermission === "denied" && (
              <span
                className="hint"
                style={{ margin: 0 }}
                title={t("jobs.notificationsBlockedHelp")}
              >
                {t("jobs.notificationsBlocked")}
              </span>
            )}
            {notifyPermission === "granted" && (
              <span className="hint" style={{ margin: 0, display: "flex", alignItems: "center", gap: 4 }}>
                <Bell size={13} /> {t("jobs.notificationsOn")}
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
          <div>{t("jobs.empty")}</div>
          <p>{t("jobs.emptyHint")}</p>
        </div>
      ) : (
        <div className="data-card">
        <table className="drive-table">
          <thead>
            <tr>
              <SortableTh column="reportName" sort={sort} onToggle={toggle}>{t("jobs.report")}</SortableTh>
              <SortableTh column="format" sort={sort} onToggle={toggle}>{t("jobs.format")}</SortableTh>
              <SortableTh column="status" sort={sort} onToggle={toggle}>{t("jobs.status")}</SortableTh>
              <th>{t("jobs.source")}</th>
              <SortableTh column="createdAtUtc" sort={sort} onToggle={toggle}>{t("jobs.created")}</SortableTh>
              <th />
            </tr>
          </thead>
          <tbody>
            {apply(jobs, (j, key) => j[key]).map((j) => (
              <tr key={j.id}>
                <td className="drive-table-name">{j.reportName}</td>
                <td><span className="chip">{j.format}</span></td>
                <td>
                  {j.status === "Queued" && (
                    <span className="job-status">
                      <Clock size={13} /> {t("jobs.queued")}
                    </span>
                  )}
                  {j.status === "Running" && (
                    <span className="job-status job-status-running">
                      <Loader2 size={13} className="spin" /> {t("jobs.running")}
                    </span>
                  )}
                  {j.status === "Succeeded" && (
                    <span className="job-status job-status-ok">
                      <CheckCircle2 size={13} /> {t("jobs.succeeded")}
                    </span>
                  )}
                  {j.status === "Failed" && (
                    <span className="job-status job-status-error" title={j.errorMessage ?? undefined}>
                      <AlertTriangle size={13} /> {t("jobs.failed")}
                    </span>
                  )}
                  {j.status === "Cancelled" && (
                    <span className="job-status">
                      <XCircle size={13} /> {t("jobs.cancelled")}
                    </span>
                  )}
                </td>
                <td>
                  <span className="job-status" title={j.scheduleId ? t("jobs.startedBySchedule") : t("jobs.startedByHand")}>
                    {j.scheduleId ? <CalendarClock size={13} /> : <User size={13} />}
                    {j.scheduleId ? t("jobs.fromSchedule") : t("jobs.manual")}
                  </span>
                </td>
                <td>{timeAgo(j.createdAtUtc)}</td>
                <td>
                  {j.status === "Succeeded" && (
                    <button className="mini" onClick={() => void download(j)} disabled={downloading === j.id}>
                      <Download size={13} /> {downloading === j.id ? "…" : t("common.download")}
                    </button>
                  )}
                  {(j.status === "Queued" || j.status === "Running") && (
                    <button className="mini" onClick={() => void cancel(j)} disabled={cancelling === j.id}>
                      <XCircle size={13} /> {cancelling === j.id ? "…" : t("common.cancel")}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      )}

      {jobs.length >= 50 && (
        <p className="hint">
          {t("jobs.listLimit")}
        </p>
      )}
    </section>
  );
}
