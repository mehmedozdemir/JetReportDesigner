import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { AlertCircle, CheckCircle2, ChevronDown, ChevronUp, Clock, Download, Loader2, X, XCircle } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { closeDock, isActive, setDockCollapsed, useJobFeed, useJobFeedPolling } from "../jobFeed";
import { notifyIfBackgrounded } from "../notifications";

// A job finished within this long before we ever saw it (e.g. the tab was reloaded right as it
// completed) still counts as "just finished" for the OS notification — anything older is history.
const RECENT_MS = 15000;

/**
 * Background report jobs, the way Google Drive shows uploads: a panel in the bottom-right corner
 * that appears when a report starts rendering, shows each one's progress, turns into a Download
 * button when it's ready, and goes away when closed. People who never run jobs never see it.
 * Mounted once at the app root, so it follows you between the Start screen and the designer.
 */
export function JobDock() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  useJobFeedPolling();
  const jobs = useJobFeed((s) => s.jobs);
  const watched = useJobFeed((s) => s.watched);
  const open = useJobFeed((s) => s.dockOpen);
  const collapsed = useJobFeed((s) => s.dockCollapsed);
  const [downloading, setDownloading] = useState<string | null>(null);
  const knownStatus = useRef<Map<string, ReportJob["status"]>>(new Map());

  // A real OS notification when a job finishes while the tab is in the background.
  useEffect(() => {
    for (const job of jobs) {
      const previous = knownStatus.current.get(job.id);
      knownStatus.current.set(job.id, job.status);
      const finished = job.status === "Succeeded" || job.status === "Failed";
      if (!finished || previous === job.status) continue;
      if (previous === undefined) {
        const finishedAt = job.completedAtUtc ? Date.parse(job.completedAtUtc) : NaN;
        if (!(Date.now() - finishedAt < RECENT_MS)) continue;
      }
      notifyIfBackgrounded(
        job.status === "Succeeded" ? t("notifications.reportReady") : t("notifications.reportFailed"),
        job.status === "Succeeded"
          ? t("notifications.finishedRendering", { name: job.reportName })
          : t("notifications.failedRendering", { name: job.reportName }),
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [jobs]);

  const rows = watched.map((id) => jobs.find((j) => j.id === id)).filter((j): j is ReportJob => !!j);
  if (!open || rows.length === 0) return null;

  const running = rows.filter(isActive).length;
  const done = rows.filter((j) => j.status === "Succeeded").length;
  const failed = rows.filter((j) => j.status === "Failed").length;
  const title =
    running > 0
      ? t("dock.preparing", { count: running })
      : failed > 0 && done === 0
        ? t("dock.failed", { count: failed })
        : failed > 0
          ? t("dock.readyWithFailures", { done, failed })
          : t("dock.ready", { count: done || rows.length });

  const download = async (job: ReportJob) => {
    setDownloading(job.id);
    try {
      downloadBlob(await api.downloadJobBlob(job.id), `${job.reportName || "report"}.${job.format}`);
    } catch {
      // The Jobs page is the fallback if this one-click download fails.
    } finally {
      setDownloading(null);
    }
  };

  const cancel = async (job: ReportJob) => {
    try {
      await api.cancelJob(job.id);
    } catch {
      // Almost always "it just finished" — the next poll shows the real status either way.
    }
  };

  return (
    <section className={`job-dock${collapsed ? " collapsed" : ""}`} aria-label={t("tray.title")} aria-live="polite">
      <header className="job-dock-head">
        {running > 0 && <Loader2 size={18} className="spin job-dock-spinner" aria-hidden />}
        <span className="job-dock-title">{title}</span>
        <button
          className="icon-btn"
          onClick={() => setDockCollapsed(!collapsed)}
          aria-label={collapsed ? t("dock.expand") : t("dock.collapse")}
          title={collapsed ? t("dock.expand") : t("dock.collapse")}
          aria-expanded={!collapsed}
        >
          {collapsed ? <ChevronUp size={20} /> : <ChevronDown size={20} />}
        </button>
        <button className="icon-btn" onClick={closeDock} aria-label={t("common.close")} title={t("common.close")}>
          <X size={20} />
        </button>
      </header>

      {!collapsed && (
        <>
          <ul className="job-dock-list">
            {rows.map((j) => (
              <li key={j.id} className="job-dock-row">
                <span className={`job-dock-icon job-dock-icon-${j.status.toLowerCase()}`} aria-hidden>
                  {j.status === "Queued" && <Clock size={18} />}
                  {j.status === "Running" && <Loader2 size={18} className="spin" />}
                  {j.status === "Succeeded" && <CheckCircle2 size={18} />}
                  {j.status === "Failed" && <AlertCircle size={18} />}
                  {j.status === "Cancelled" && <XCircle size={18} />}
                </span>
                <span className="job-dock-text">
                  <span className="job-dock-name" title={j.reportName}>{j.reportName}</span>
                  <span className="job-dock-meta" title={j.status === "Failed" ? j.errorMessage ?? undefined : undefined}>
                    {j.format.toUpperCase()} · {j.status === "Failed" && j.errorMessage ? j.errorMessage : t(`jobs.${j.status.toLowerCase()}`)}
                  </span>
                </span>
                {j.status === "Succeeded" && (
                  <button className="btn text job-dock-action" onClick={() => void download(j)} disabled={downloading === j.id}>
                    {downloading === j.id ? <Loader2 size={16} className="spin" /> : <Download size={16} />} {t("common.download")}
                  </button>
                )}
                {isActive(j) && (
                  <button className="icon-btn" onClick={() => void cancel(j)} aria-label={t("dock.cancel", { name: j.reportName })} title={t("common.cancel")}>
                    <XCircle size={18} />
                  </button>
                )}
              </li>
            ))}
          </ul>
          <button
            className="job-dock-all"
            onClick={() => {
              closeDock();
              navigate("/jobs");
            }}
          >
            {t("tray.seeAll")}
          </button>
        </>
      )}
    </section>
  );
}
