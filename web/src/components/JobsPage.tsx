import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, Bell, CalendarClock, CheckCircle2, ChevronLeft, ChevronRight, Clock, Download, Loader2, User, XCircle } from "lucide-react";
import { api, type JobSortKey, type ReportJob, type ReportJobStatus } from "../api";
import { downloadBlob } from "../download";
import { notificationPermission, requestNotificationPermission } from "../notifications";
import { timeAgo } from "../time";
import { PageHeader } from "./PageHeader";
import { SortableTh, useSort } from "./SortableTh";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

const PAGE_SIZE = 25;

const STATUS_FILTERS: (ReportJobStatus | "all")[] = [
  "all",
  "Queued",
  "Running",
  "Succeeded",
  "Failed",
  "Cancelled",
];

/** Background report jobs — tenant-wide, auto-refreshing while any are still in flight.
 * Reached from the Start screen like Team/Email: not tied to whatever report you have open.
 * (The actual notify-when-done logic lives in JobNotifications, mounted app-wide — this
 * page just offers the permission opt-in and the full history/status table.) */
export function JobsPage() {
  const { t, i18n } = useTranslation();
  const [jobs, setJobs] = useState<ReportJob[] | null>(null);
  const [total, setTotal] = useState(0);
  const [status, setStatus] = useState<ReportJobStatus | "all">("all");
  const [skip, setSkip] = useState(0);
  const [err, setErr] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const { sort, toggle } = useSort<JobSortKey>({ key: "createdAtUtc", dir: "desc" });
  const [notifyPermission, setNotifyPermission] = useState(notificationPermission());

  // Filtering, sorting and paging all happen server-side: this page holds at most one page of
  // rows, and sorting only those would quietly sort a history it can't see.
  useEffect(() => {
    let cancelled = false;
    const load = () =>
      api
        .listJobs({
          statuses: status === "all" ? undefined : [status],
          sort: sort.key,
          desc: sort.dir === "desc",
          skip,
          take: PAGE_SIZE,
        })
        .then((page) => {
          if (cancelled) return;
          setJobs(page.items);
          setTotal(page.total);
          // A job finishing under a status filter (or the retention sweep) can shrink the list
          // out from under the page you're on — step back rather than show an empty table.
          if (page.items.length === 0 && skip > 0) setSkip(Math.max(0, skip - PAGE_SIZE));
        })
        .catch((e) => {
          if (!cancelled) setErr(msg(e));
        });
    void load();
    const id = window.setInterval(() => void load(), 3000);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, [status, sort.key, sort.dir, skip]);

  const changeStatus = (next: ReportJobStatus | "all") => {
    setStatus(next);
    setSkip(0);
  };

  const changeSort = (key: JobSortKey) => {
    toggle(key);
    setSkip(0);
  };

  const enableNotifications = async () => {
    setNotifyPermission(await requestNotificationPermission());
  };

  const cancel = async (job: ReportJob) => {
    setCancelling(job.id);
    setErr(null);
    try {
      await api.cancelJob(job.id);
      // The 3s poll picks the new status up; cancelling a running job isn't instant anyway.
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

      <div className="filter-bar" role="group" aria-label={t("jobs.status")}>
        {STATUS_FILTERS.map((f) => (
          <button
            key={f}
            className={`mini${status === f ? " on" : ""}`}
            aria-pressed={status === f}
            onClick={() => changeStatus(f)}
          >
            {f === "all" ? t("jobs.filterAll") : t(`jobs.${f.toLowerCase()}`)}
          </button>
        ))}
      </div>

      {jobs.length === 0 ? (
        <div className="start-empty">
          <Clock />
          <div>{status === "all" ? t("jobs.empty") : t("jobs.noMatch")}</div>
          <p>{status === "all" ? t("jobs.emptyHint") : t("jobs.noMatchHint")}</p>
        </div>
      ) : (
        <div className="data-card">
        <table className="drive-table">
          <thead>
            <tr>
              <SortableTh column="reportName" sort={sort} onToggle={changeSort}>{t("jobs.report")}</SortableTh>
              <SortableTh column="format" sort={sort} onToggle={changeSort}>{t("jobs.format")}</SortableTh>
              <SortableTh column="status" sort={sort} onToggle={changeSort}>{t("jobs.status")}</SortableTh>
              <th>{t("jobs.source")}</th>
              <SortableTh column="createdAtUtc" sort={sort} onToggle={changeSort}>{t("jobs.created")}</SortableTh>
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
                <td>{timeAgo(j.createdAtUtc, i18n.language)}</td>
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

      {total > 0 && (
        <div className="table-pager">
          <span className="hint">
            {t("jobs.range", { from: skip + 1, to: skip + jobs.length, total })}
          </span>
          <button className="mini" onClick={() => setSkip(Math.max(0, skip - PAGE_SIZE))} disabled={skip === 0}>
            <ChevronLeft size={13} /> {t("common.previous")}
          </button>
          <button
            className="mini"
            onClick={() => setSkip(skip + PAGE_SIZE)}
            disabled={skip + jobs.length >= total}
          >
            {t("common.next")} <ChevronRight size={13} />
          </button>
        </div>
      )}

      <p className="hint">{t("jobs.retention")}</p>
    </section>
  );
}
