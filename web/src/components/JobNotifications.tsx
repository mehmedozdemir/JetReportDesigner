import { useEffect, useRef, useState } from "react";
import { AlertTriangle, CheckCircle2, Download, X } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { notifyIfBackgrounded } from "../notifications";

const POLL_MS = 5000;
const AUTO_DISMISS_MS = 8000;

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
  const [toasts, setToasts] = useState<Toast[]>([]);
  const knownStatus = useRef<Map<string, ReportJob["status"]>>(new Map());
  const isFirstPoll = useRef(true);

  useEffect(() => {
    let cancelled = false;

    const poll = async () => {
      let jobs: ReportJob[];
      try {
        jobs = await api.listJobs();
      } catch {
        return; // transient network hiccup — just try again next tick
      }
      if (cancelled) return;

      for (const job of jobs) {
        const previous = knownStatus.current.get(job.id);
        knownStatus.current.set(job.id, job.status);

        const justFinished = job.status === "Succeeded" || job.status === "Failed";
        // Skip the very first poll after mount (or load) entirely — otherwise every job
        // that already finished before this tab opened would "notify" all at once — and
        // otherwise only fire on an actual transition into a finished state.
        if (isFirstPoll.current || !justFinished || previous === job.status) continue;

        setToasts((cur) => [...cur, { id: job.id, job }]);
        notifyIfBackgrounded(
          job.status === "Succeeded" ? "Report ready" : "Report failed",
          job.status === "Succeeded" ? `${job.reportName} finished rendering.` : `${job.reportName} failed to render.`,
        );
      }

      isFirstPoll.current = false;
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
    const timers = toasts.map((t) => window.setTimeout(() => dismiss(t.id), AUTO_DISMISS_MS));
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
      {toasts.map((t) => (
        <div key={t.id} className={`job-toast${t.job.status === "Failed" ? " job-toast-error" : ""}`}>
          {t.job.status === "Succeeded" ? <CheckCircle2 size={16} /> : <AlertTriangle size={16} />}
          <div className="job-toast-body">
            <div className="job-toast-title">
              {t.job.status === "Succeeded" ? "Report ready" : "Report failed"}
            </div>
            <div className="job-toast-name">{t.job.reportName}</div>
          </div>
          {t.job.status === "Succeeded" && (
            <button className="mini" onClick={() => void download(t.job)} title="Download" aria-label="Download">
              <Download size={13} />
            </button>
          )}
          <button className="mini ghost" onClick={() => dismiss(t.id)} title="Dismiss" aria-label="Dismiss">
            <X size={13} />
          </button>
        </div>
      ))}
    </div>
  );
}
