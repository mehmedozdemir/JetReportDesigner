import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, CheckCircle2, Download, X } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { useJobFeed, useJobFeedPolling } from "../jobFeed";
import { notifyIfBackgrounded } from "../notifications";

const AUTO_DISMISS_MS = 8000;
// A job finished within this long before we ever saw it (e.g. the tab was reloaded right
// as it completed) still counts as "just finished" — anything older is stale history.
const RECENT_MS = 15000;

interface Toast {
  id: string;
  job: ReportJob;
}

/** Surfaces the moment a background job finishes: an in-app toast (with a one-click download
 * for a success) plus, if the tab is in the background, a real OS notification. Reads the
 * shared job feed rather than polling itself, and stays quiet while the tray is open, where
 * the same change is already on screen. Mounted once at the app root — not tied to the Jobs
 * page's lifecycle, so leaving that page doesn't stop watching. */
export function JobNotifications() {
  const { t } = useTranslation();
  const [toasts, setToasts] = useState<Toast[]>([]);
  const knownStatus = useRef<Map<string, ReportJob["status"]>>(new Map());

  useJobFeedPolling();
  const jobs = useJobFeed((s) => s.jobs);
  const trayOpen = useJobFeed((s) => s.trayOpen);
  // Read through a ref: a toast is decided the moment a job's status changes, and whether the
  // tray happened to be open then shouldn't re-run that decision when it later closes.
  const trayOpenNow = useRef(trayOpen);
  trayOpenNow.current = trayOpen;

  useEffect(() => {
    for (const job of jobs) {
      const previous = knownStatus.current.get(job.id);
      knownStatus.current.set(job.id, job.status);

      const justFinished = job.status === "Succeeded" || job.status === "Failed";
      if (!justFinished) continue;
      if (previous === job.status) continue;
      // The first time we ever see a job that's already finished, only notify if it
      // finished recently — e.g. the tab was reloaded right as it completed. Otherwise
      // every job that finished long before this tab opened would "notify" all at once.
      if (previous === undefined) {
        const finishedAt = job.completedAtUtc ? Date.parse(job.completedAtUtc) : NaN;
        if (!(Date.now() - finishedAt < RECENT_MS)) continue;
      }

      // The tray is already showing this job change as it happens; a toast on top of it would
      // say the same thing twice. The OS notification still fires — that one is for when the
      // tab isn't in front at all, where nothing on screen is visible either way.
      if (!trayOpenNow.current) setToasts((cur) => [...cur, { id: job.id, job }]);
      notifyIfBackgrounded(
        job.status === "Succeeded" ? t("notifications.reportReady") : t("notifications.reportFailed"),
        job.status === "Succeeded"
          ? t("notifications.finishedRendering", { name: job.reportName })
          : t("notifications.failedRendering", { name: job.reportName }),
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [jobs]);

  const dismiss = (id: string) => setToasts((cur) => cur.filter((t) => t.id !== id));

  useEffect(() => {
    if (toasts.length === 0) return;
    const timers = toasts.map((toast) => window.setTimeout(() => dismiss(toast.id), AUTO_DISMISS_MS));
    return () => timers.forEach(window.clearTimeout);
  }, [toasts]);

  const download = async (job: ReportJob) => {
    try {
      const blob = await api.downloadJobBlob(job.id);
      downloadBlob(blob, `${job.reportName || "report"}.${job.format}`);
    } catch {
      // The Jobs tab is the fallback if this one-click download ever fails.
    }
  };

  if (toasts.length === 0) return null;

  return (
    <div className="job-toast-stack">
      {toasts.map((toast) => (
        <div key={toast.id} className={`job-toast${toast.job.status === "Failed" ? " job-toast-error" : ""}`}>
          {toast.job.status === "Succeeded" ? <CheckCircle2 size={16} /> : <AlertTriangle size={16} />}
          <div className="job-toast-body">
            <div className="job-toast-title">
              {toast.job.status === "Succeeded" ? t("notifications.reportReady") : t("notifications.reportFailed")}
            </div>
            <div className="job-toast-name">{toast.job.reportName}</div>
          </div>
          {toast.job.status === "Succeeded" && (
            <button className="mini" onClick={() => void download(toast.job)} title={t("common.download")} aria-label={t("common.download")}>
              <Download size={13} />
            </button>
          )}
          <button className="mini ghost" onClick={() => dismiss(toast.id)} title={t("notifications.dismiss")} aria-label={t("notifications.dismiss")}>
            <X size={13} />
          </button>
        </div>
      ))}
    </div>
  );
}
