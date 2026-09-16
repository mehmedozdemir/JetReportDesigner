import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { AlertTriangle, CheckCircle2, Clock, Download, Loader2, XCircle } from "lucide-react";
import { api, type ReportJob } from "../api";
import { downloadBlob } from "../download";
import { holdTrayOpen, isActive, setTrayOpen, useActiveJobCount, useJobFeed, useJobFeedPolling } from "../jobFeed";
import { timeAgo } from "../time";
import { useEscapeKey } from "../useEscapeKey";

/** How many rows the panel shows. The rest is what the Jobs page is for. */
const VISIBLE = 10;

/** Kept in step with .job-tray-panel's width, which the placement maths needs to know. */
const PANEL_WIDTH = 320;
const MARGIN = 8;

/** The background-job tray: a button in the app chrome with a panel under it, like a browser's
 * downloads button. Sending a report to the background used to navigate you to the Jobs page,
 * which defeated the purpose — this is what you watch instead, from wherever you were.
 *
 * The button sits in both chromes (the designer toolbar and the Start screen's nav rail), but
 * the panel itself is portalled to the body and positioned from the button's rect: the nav rail
 * scrolls, and anything rendered inside it gets clipped at its edge — which is exactly what
 * happened to the first version of this. */
export function JobTray() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  useJobFeedPolling();
  const jobs = useJobFeed((s) => s.jobs);
  const loaded = useJobFeed((s) => s.loaded);
  const open = useJobFeed((s) => s.trayOpen);
  const { count, capped } = useActiveJobCount();
  const [downloading, setDownloading] = useState<string | null>(null);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);
  const wrap = useRef<HTMLDivElement>(null);
  const panel = useRef<HTMLDivElement>(null);

  /** Right-aligned to the button where there's room — anchored under it, never off-screen. */
  const place = useCallback(() => {
    const r = wrap.current?.getBoundingClientRect();
    if (!r) return;
    const left = Math.max(MARGIN, Math.min(r.right - PANEL_WIDTH, window.innerWidth - PANEL_WIDTH - MARGIN));
    setPos({ top: r.bottom + 6, left });
  }, []);

  useEffect(() => {
    if (!open) return;
    place();
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
    };
  }, [open, place]);

  useEscapeKey(() => setTrayOpen(false));

  // Clicking anywhere else dismisses the panel. onBlur alone wouldn't: clicking a plain,
  // non-focusable part of the page moves no focus, so the panel would just sit there.
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node;
      if (!wrap.current?.contains(t) && !panel.current?.contains(t)) setTrayOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  const download = async (job: ReportJob) => {
    setDownloading(job.id);
    try {
      const blob = await api.downloadJobBlob(job.id);
      downloadBlob(blob, `${job.reportName || "report"}.${job.format}`);
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

  const goToJobs = () => {
    setTrayOpen(false);
    navigate("/jobs");
  };

  return (
    <div
      className="job-tray"
      ref={wrap}
      onMouseEnter={holdTrayOpen}
    >
      <button
        className={`btn icon job-tray-button${count > 0 ? " busy" : ""}`}
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={count > 0 ? t("tray.buttonBusy", { count }) : t("tray.button")}
        title={count > 0 ? t("tray.buttonBusy", { count }) : t("tray.button")}
        onClick={() => setTrayOpen(!open)}
      >
        {count > 0 ? <Loader2 className="spin" /> : <Clock />}
        {count > 0 && <span className="nav-badge">{capped ? `${count}+` : count}</span>}
      </button>

      {open && pos &&
        createPortal(
          <div className="job-tray-panel" role="dialog" aria-label={t("tray.title")} ref={panel} style={pos}>
          <div className="job-tray-head">{t("tray.title")}</div>

          {jobs.length === 0 ? (
            <div className="job-tray-empty">{loaded ? t("tray.empty") : t("common.loading")}</div>
          ) : (
            <ul className="job-tray-list">
              {jobs.slice(0, VISIBLE).map((j) => (
                <li key={j.id} className="job-tray-item">
                  <span className={`job-tray-icon${j.status === "Failed" ? " error" : ""}`}>
                    {j.status === "Queued" && <Clock size={14} />}
                    {j.status === "Running" && <Loader2 size={14} className="spin" />}
                    {j.status === "Succeeded" && <CheckCircle2 size={14} />}
                    {j.status === "Failed" && <AlertTriangle size={14} />}
                    {j.status === "Cancelled" && <XCircle size={14} />}
                  </span>
                  <span className="job-tray-text">
                    <span className="job-tray-name">{j.reportName}</span>
                    <span className="job-tray-meta">
                      {j.format.toUpperCase()} · {t(`jobs.${j.status.toLowerCase()}`)} ·{" "}
                      {timeAgo(j.createdAtUtc, i18n.language)}
                    </span>
                    {j.status === "Failed" && j.errorMessage && (
                      <span className="job-tray-error" title={j.errorMessage}>
                        {j.errorMessage}
                      </span>
                    )}
                  </span>
                  {j.status === "Succeeded" && (
                    <button
                      className="mini"
                      onClick={() => void download(j)}
                      disabled={downloading === j.id}
                      title={t("common.download")}
                      aria-label={t("common.download")}
                    >
                      <Download size={13} />
                    </button>
                  )}
                  {isActive(j) && (
                    <button
                      className="mini"
                      onClick={() => void cancel(j)}
                      title={t("common.cancel")}
                      aria-label={t("common.cancel")}
                    >
                      <XCircle size={13} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

            <button className="job-tray-all" onClick={goToJobs}>
              {t("tray.seeAll")}
            </button>
          </div>,
          document.body,
        )}
    </div>
  );
}
