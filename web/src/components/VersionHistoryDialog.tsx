import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, Check, Download, History, Loader2, RotateCcw, Undo2, X } from "lucide-react";
import { api, type ReportVersionDetail, type ReportVersionInfo } from "../api";
import { downloadBlob } from "../download";
import { timeAgo } from "../time";
import type { ReportResponse } from "../types";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** One change token from the server ("added:3", "page", …) as words. */
export function ChangeChips({ changes }: { changes: string[] }) {
  const { t } = useTranslation();
  return (
    <>
      {changes.map((token) => {
        const [kind, count] = token.split(":");
        return (
          <span key={token} className="history-chip">
            {count ? t(`history.change.${kind}`, { count: Number(count) }) : t(`history.change.${kind}`)}
          </span>
        );
      })}
    </>
  );
}

/**
 * Every saved version of a report: look at what changed, run an old version (preview or PDF) without
 * touching the current one, and make an old version the new current one. Restoring never deletes
 * anything — it saves the old version again as a new version, so the one it replaced stays in the list
 * and the restore itself can be undone.
 */
export function VersionHistoryDialog({
  report,
  canRestore,
  unsavedChanges = false,
  onRestored,
  onClose,
}: {
  report: { id: string; name: string };
  canRestore: boolean;
  /** The designer has edits that are not saved; restoring would discard them. */
  unsavedChanges?: boolean;
  onRestored: (restored: ReportResponse) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(onClose);

  const [versions, setVersions] = useState<ReportVersionInfo[] | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [html, setHtml] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState<"restore" | "pdf" | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const details = useRef(new Map<number, ReportVersionDetail>());

  useEffect(() => {
    let cancelled = false;
    api
      .listVersions(report.id)
      .then((list) => {
        if (cancelled) return;
        setVersions(list);
        // Open on the version before the current one — the usual "what did I just break?" question.
        setSelected((list[1] ?? list[0])?.version ?? null);
      })
      .catch((e) => !cancelled && setErr(msg(e)));
    return () => {
      cancelled = true;
    };
  }, [report.id]);

  const current = versions?.[0]?.version ?? null;
  const info = useMemo(() => versions?.find((v) => v.version === selected) ?? null, [versions, selected]);

  const detailOf = async (version: number) => {
    const cached = details.current.get(version);
    if (cached) return cached;
    const fetched = await api.getVersion(report.id, version);
    details.current.set(version, fetched);
    return fetched;
  };

  // Render the selected version as it would run today (server-side), without opening it in the designer.
  useEffect(() => {
    if (selected === null) return;
    let cancelled = false;
    setConfirming(false);
    setHtml(null);
    setPreviewing(true);
    setErr(null);
    detailOf(selected)
      .then((d) => api.renderHtml(d.definition))
      .then((h) => !cancelled && setHtml(h))
      .catch((e) => !cancelled && setErr(msg(e)))
      .finally(() => !cancelled && setPreviewing(false));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected, report.id]);

  const downloadPdf = async () => {
    if (selected === null) return;
    setBusy("pdf");
    setErr(null);
    try {
      const d = await detailOf(selected);
      downloadBlob(await api.renderPdfBlob(d.definition), `${report.name || "report"}-v${selected}.pdf`);
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusy(null);
    }
  };

  const restore = async () => {
    if (selected === null) return;
    setBusy("restore");
    setErr(null);
    try {
      onRestored(await api.restoreVersion(report.id, selected));
    } catch (e) {
      setErr(msg(e));
      setBusy(null);
    }
  };

  const when = (iso: string) => new Date(iso).toLocaleString(i18n.language);

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal history-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={t("history.title", { name: report.name })}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <History /> {t("history.title", { name: report.name })}
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        {versions === null && !err ? (
          <div className="preview-dialog-loading">
            <Loader2 size={16} className="spin" />
          </div>
        ) : (
          <div className="history-body">
            <ol className="history-list" aria-label={t("history.versions")}>
              {(versions ?? []).map((v) => (
                <li key={v.version}>
                  <button
                    className={`history-item${v.version === selected ? " on" : ""}`}
                    aria-current={v.version === selected}
                    onClick={() => setSelected(v.version)}
                  >
                    <span className="history-item-top">
                      <strong>v{v.version}</strong>
                      {v.version === current && <span className="history-badge">{t("history.current")}</span>}
                      <span className="history-when" title={when(v.savedAtUtc)}>
                        {timeAgo(v.savedAtUtc, i18n.language)}
                      </span>
                    </span>
                    <span className="history-by">{v.savedByEmail ?? t("history.unknownAuthor")}</span>
                    <span className="history-chips">
                      {v.restoredFromVersion != null && (
                        <span className="history-chip history-chip-restore">
                          <Undo2 size={11} /> {t("history.restoredFrom", { version: v.restoredFromVersion })}
                        </span>
                      )}
                      {v.changes.length > 0 ? (
                        <ChangeChips changes={v.changes} />
                      ) : (
                        v.restoredFromVersion == null &&
                        v.version === 1 && <span className="history-chip">{t("history.first")}</span>
                      )}
                    </span>
                  </button>
                </li>
              ))}
            </ol>

            <div className="history-detail">
              {info && (
                <div className="history-detail-head">
                  <div>
                    <strong>
                      v{info.version}
                      {info.version === current && ` · ${t("history.current")}`}
                    </strong>
                    <span className="hint" style={{ margin: "0 0 0 8px" }}>
                      {when(info.savedAtUtc)} · {info.savedByEmail ?? t("history.unknownAuthor")}
                    </span>
                  </div>
                </div>
              )}

              <div className="history-preview">
                {previewing ? (
                  <div className="preview-dialog-loading">
                    <Loader2 size={16} className="spin" /> {t("preview.rendering")}
                  </div>
                ) : html !== null ? (
                  <iframe title={t("history.previewTitle")} className="preview-frame" srcDoc={html} />
                ) : null}
              </div>

              {err && (
                <div className="error small history-error">
                  <AlertTriangle /> <span>{err}</span>
                </div>
              )}

              <footer className="history-footer">
                <button className="mini" onClick={() => void downloadPdf()} disabled={selected === null || busy !== null}>
                  <Download size={13} /> {busy === "pdf" ? t("preview.exporting") : t("preview.exportPdf")}
                </button>

                {canRestore && selected !== null && selected !== current && !confirming && (
                  <button className="btn primary history-restore" onClick={() => setConfirming(true)} disabled={busy !== null}>
                    <RotateCcw size={14} /> {t("history.restore", { version: selected })}
                  </button>
                )}
                {canRestore && selected !== null && selected === current && (
                  <span className="hint" style={{ margin: "0 0 0 auto" }}>
                    {t("history.alreadyCurrent")}
                  </span>
                )}
              </footer>

              {confirming && selected !== null && (
                <div className="history-confirm" role="alert">
                  <p>
                    {t("history.confirm", { version: selected, next: (current ?? 0) + 1, current })}
                    {unsavedChanges && <strong> {t("history.unsavedWarning")}</strong>}
                  </p>
                  <div className="history-confirm-actions">
                    <button className="btn" onClick={() => setConfirming(false)} disabled={busy !== null}>
                      {t("common.cancel")}
                    </button>
                    <button className="btn primary" onClick={() => void restore()} disabled={busy !== null}>
                      {busy === "restore" ? <Loader2 size={14} className="spin" /> : <Check size={14} />} {t("history.confirmRestore")}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
