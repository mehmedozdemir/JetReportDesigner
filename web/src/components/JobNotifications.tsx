import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, CheckCircle2, Download, X } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { notifyIfBackgrounded } from "../notifications";

const POLL_MS = 5000;
const AUTO_DISMISS_MS = 8000;
// A job finished within this long before we ever saw it (e.g. the tab was reloaded right
// as it completed) still counts as "just finished" — anything older is stale history.
const RECENT_MS = 15000;

interface Toast {
  id: string;
  job: ReportJob;
}

/** Watches every background report job regardless of which Start-screen tab (if any) is
 * open, and surfaces the moment one finishes: an in-app toast (with a one-click download
 * for a success) plus, if the tab is in the background, a real OS notification. Mounted
 * once at the app root — not tied to the Jobs tab's own lifecycle, so leaving that tab
 * doesn't stop watching. */
export function JobNotifications() {
  const { t } = useTranslation();
  const [toasts, setToasts] = useState<Toast[]>([]);
  const knownStatus = useRef<Map<string, ReportJob["status"]>>(new Map());

  useEffect(() => {
    let cancelled = false;

    const poll = async () => {
      let jobs: ReportJob[];
      try {
        // Only the newest handful can have changed status since the last tick.
        jobs = (await api.listJobs({ take: 20 })).items;
      } catch {
        return; // transient network hiccup — just try again next tick
      }
      if (cancelled) return;

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

        setToasts((cur) => [...cur, { id: job.id, job }]);
        notifyIfBackgrounded(
          job.status === "Succeeded" ? t("notifications.reportReady") : t("notifications.reportFailed"),
          job.status === "Succeeded"
            ? t("notifications.finishedRendering", { name: job.reportName })
            : t("notifications.failedRendering", { name: job.reportName }),
        );
      }
    };

    void poll();
    const id = window.setInterval(() => void poll(), POLL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, []);

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
