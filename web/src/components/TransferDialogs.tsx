import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  AlertTriangle,
  ArrowRight,
  Check,
  CheckCircle2,
  Database,
  FileArchive,
  FolderTree,
  Image as ImageIcon,
  Loader2,
  PackageOpen,
  PackagePlus,
  ShieldAlert,
  Upload,
  X,
} from "lucide-react";
import {
  api,
  type ExportPlan,
  type ImportAction,
  type ImportPlan,
  type ImportResult,
  type ImportOptions,
} from "../api";
import { downloadBlob } from "../download";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";
import { ChangeChips } from "./VersionHistoryDialog";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** "kind|detail" warning tokens from the server, as a sentence. */
function Warnings({ tokens }: { tokens: string[] }) {
  const { t } = useTranslation();
  if (tokens.length === 0) return null;
  return (
    <ul className="transfer-warnings">
      {tokens.map((token) => {
        const [kind, detail] = token.split("|");
        return (
          <li key={token}>
            <AlertTriangle size={13} /> {t(`transfer.warning.${kind}`, { name: detail, count: Number(detail) })}
          </li>
        );
      })}
    </ul>
  );
}

// ------------------------------------------------------------------------------------------------
// Export
// ------------------------------------------------------------------------------------------------

/** Shows what a package of the chosen reports/folders will hold — including what is pulled in
 * automatically — and downloads it. */
export function ExportPackageDialog({
  reportIds,
  folderIds,
  title,
  onClose,
}: {
  reportIds: string[];
  folderIds: string[];
  title: string;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(onClose);

  const [stripSampleData, setStripSampleData] = useState(false);
  const [plan, setPlan] = useState<ExportPlan | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .planExport({ reportIds, folderIds, stripSampleData })
      .then((p) => !cancelled && setPlan(p))
      .catch((e) => !cancelled && setErr(msg(e)));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stripSampleData]);

  const download = async () => {
    setBusy(true);
    setErr(null);
    try {
      const { blob, fileName } = await api.exportPackage({ reportIds, folderIds, stripSampleData });
      downloadBlob(blob, fileName);
      setDone(true);
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusy(false);
    }
  };

  const dependencies = plan?.reports.filter((r) => r.role === "dependency") ?? [];

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal transfer-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={t("transfer.export.title")}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <PackagePlus /> {t("transfer.export.title")}
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        <div className="transfer-body">
          <p className="hint" style={{ marginTop: 0 }}>{title}</p>

          {!plan && !err && (
            <div className="share-loading">
              <Loader2 size={16} className="spin" />
            </div>
          )}

          {plan && (
            <>
              <div className="transfer-stats">
                <span><FileArchive size={14} /> {t("transfer.stat.reports", { count: plan.reports.length })}</span>
                <span><FolderTree size={14} /> {t("transfer.stat.folders", { count: plan.folders.length })}</span>
                <span><ImageIcon size={14} /> {t("transfer.stat.images", { count: plan.assets })}</span>
              </div>

              <ul className="transfer-list">
                {plan.reports.map((r) => (
                  <li key={r.id}>
                    <strong>{r.name}</strong>
                    <code className="report-code-chip">{r.code}</code>
                    {r.role === "dependency" && <span className="history-chip history-chip-restore">{t("transfer.dependency")}</span>}
                    {r.folder && <span className="hint" style={{ margin: 0 }}>{r.folder}</span>}
                  </li>
                ))}
              </ul>

              {dependencies.length > 0 && <p className="hint">{t("transfer.dependencyHint", { count: dependencies.length })}</p>}

              {plan.connections.length > 0 && (
                <div className="transfer-note">
                  <Database size={14} />
                  <span>
                    {t("transfer.connectionsNote")} <strong>{plan.connections.map((c) => c.name).join(", ")}</strong>
                  </span>
                </div>
              )}

              {plan.redactedValues > 0 && (
                <div className="transfer-note">
                  <ShieldAlert size={14} />
                  <span>{t("transfer.redactedNote", { count: plan.redactedValues })}</span>
                </div>
              )}

              <Warnings tokens={plan.warnings} />

              <label className="check-row">
                <input type="checkbox" checked={stripSampleData} onChange={(e) => setStripSampleData(e.target.checked)} />
                <span>{t("transfer.stripSample")}</span>
              </label>
              <p className="hint" style={{ marginTop: 0 }}>{t("transfer.stripSampleHint")}</p>
            </>
          )}

          {err && (
            <div className="error small">
              <AlertTriangle /> <span>{err}</span>
            </div>
          )}
          {done && (
            <div className="transfer-note transfer-note-ok">
              <CheckCircle2 size={14} /> <span>{t("transfer.export.done")}</span>
            </div>
          )}
        </div>

        <footer className="transfer-footer">
          <button className="btn" onClick={onClose}>{t("common.close")}</button>
          <button className="btn primary" onClick={() => void download()} disabled={!plan || plan.reports.length === 0 || busy}>
            {busy ? <Loader2 size={14} className="spin" /> : <PackagePlus size={14} />} {t("transfer.export.download")}
          </button>
        </footer>
      </div>
    </div>
  );
}

// ------------------------------------------------------------------------------------------------
// Import
// ------------------------------------------------------------------------------------------------

const ACTION_ORDER: ImportAction[] = ["create", "update", "copy", "skip"];

/**
 * Import in three steps: pick the file → review exactly what it would do (nothing is changed yet) →
 * apply. Existing reports are overwritten as a new version, so an import is never a one-way door.
 */
export function ImportPackageDialog({
  folderOptions,
  initialFolderId,
  onClose,
  onImported,
}: {
  folderOptions: { id: string; label: string }[];
  initialFolderId: string | null;
  onClose: () => void;
  onImported: () => void;
}) {
  const { t, i18n } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(() => {
    if (!applying) onClose();
  });

  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [plan, setPlan] = useState<ImportPlan | null>(null);
  const [actions, setActions] = useState<Record<string, ImportAction>>({});
  const [targetFolder, setTargetFolder] = useState<string | null>(initialFolderId);
  const [analyzing, setAnalyzing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const optionsFor = (folder: string | null, chosen: Record<string, ImportAction>): ImportOptions => ({
    targetFolderId: folder,
    decisions: Object.entries(chosen).map(([code, action]) => ({ code, action })),
  });

  const analyze = async (picked: File, folder = targetFolder, chosen: Record<string, ImportAction> = {}) => {
    setAnalyzing(true);
    setErr(null);
    try {
      const p = await api.previewImport(picked, optionsFor(folder, chosen));
      setPlan(p);
      setActions(Object.fromEntries(p.items.map((i) => [i.code, i.action])));
    } catch (e) {
      setPlan(null);
      setErr(msg(e));
    } finally {
      setAnalyzing(false);
    }
  };

  const pick = (picked: File | undefined | null) => {
    if (!picked) return;
    setFile(picked);
    setResult(null);
    void analyze(picked);
  };

  const apply = async () => {
    if (!file || !plan) return;
    setApplying(true);
    setErr(null);
    try {
      setResult(await api.importPackage(file, optionsFor(targetFolder, actions)));
      onImported();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setApplying(false);
    }
  };

  const willChange = useMemo(() => Object.values(actions).filter((a) => a !== "skip").length, [actions]);
  const overwrites = useMemo(() => Object.values(actions).filter((a) => a === "update").length, [actions]);
  const hasInvalid = plan?.items.some((i) => i.status === "invalid") ?? false;

  return (
    <div className="modal-backdrop" onMouseDown={() => !applying && onClose()}>
      <div
        ref={dialogRef}
        className="modal transfer-dialog transfer-dialog-wide"
        role="dialog"
        aria-modal="true"
        aria-label={t("transfer.import.title")}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <PackageOpen /> {t("transfer.import.title")}
          </h2>
          <button className="mini ghost" onClick={onClose} disabled={applying} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        <div className="transfer-body">
          {/* ---- step 1: choose the package ---- */}
          {!plan && !result && (
            <div
              className={`transfer-drop${dragging ? " on" : ""}`}
              onDragOver={(e) => {
                e.preventDefault();
                setDragging(true);
              }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragging(false);
                pick(e.dataTransfer.files[0]);
              }}
            >
              {analyzing ? (
                <>
                  <Loader2 size={22} className="spin" />
                  <strong>{t("transfer.import.checking")}</strong>
                </>
              ) : (
                <>
                  <Upload size={24} />
                  <strong>{t("transfer.import.drop")}</strong>
                  <span className="hint" style={{ margin: 0 }}>{t("transfer.import.dropHint")}</span>
                  <button className="btn" onClick={() => fileInput.current?.click()}>
                    {t("transfer.import.choose")}
                  </button>
                </>
              )}
              <input
                ref={fileInput}
                type="file"
                accept=".jrdpkg,.zip,application/zip"
                hidden
                onChange={(e) => {
                  pick(e.target.files?.[0]);
                  e.target.value = "";
                }}
              />
            </div>
          )}

          {/* ---- step 2: review ---- */}
          {plan && !result && (
            <>
              <div className="transfer-source">
                <FileArchive size={16} />
                <div>
                  <strong>{file?.name}</strong>
                  <span className="hint" style={{ margin: 0 }}>
                    {plan.source.environment ? `${plan.source.environment} · ` : ""}
                    {plan.source.exportedBy ? `${plan.source.exportedBy} · ` : ""}
                    {new Date(plan.source.exportedAtUtc).toLocaleString(i18n.language)}
                  </span>
                </div>
                <button className="mini" style={{ marginLeft: "auto" }} onClick={() => { setPlan(null); setFile(null); }}>
                  {t("transfer.import.other")}
                </button>
              </div>

              <label className="field transfer-target">
                <span>{t("transfer.import.targetFolder")}</span>
                <select
                  value={targetFolder ?? ""}
                  onChange={(e) => {
                    const folder = e.target.value || null;
                    setTargetFolder(folder);
                    if (file) void analyze(file, folder, actions);
                  }}
                >
                  <option value="">{t("reports.menu.root")}</option>
                  {folderOptions.map((f) => (
                    <option key={f.id} value={f.id}>{f.label}</option>
                  ))}
                </select>
              </label>

              <div className="data-card">
                <table className="drive-table transfer-table">
                  <thead>
                    <tr>
                      <th>{t("transfer.col.report")}</th>
                      <th>{t("transfer.col.status")}</th>
                      <th>{t("transfer.col.action")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {plan.items.map((item) => (
                      <tr key={item.code} className={actions[item.code] === "skip" ? "key-row-off" : undefined}>
                        <td>
                          <div className="key-name">
                            <strong>{item.name}</strong>
                            <span className="hint">
                              <code className="report-code-chip" style={{ marginLeft: 0 }}>{item.code}</code>
                              {item.folder ? ` · ${item.folder}` : ""}
                              {item.role === "dependency" ? ` · ${t("transfer.dependency")}` : ""}
                            </span>
                          </div>
                        </td>
                        <td>
                          <span className={`transfer-status transfer-status-${item.status}`}>
                            {t(`transfer.status.${item.status}`)}
                          </span>
                          {item.status === "changed" && (
                            <span className="history-chips" style={{ marginTop: 4 }}>
                              <ChangeChips changes={item.changes} />
                            </span>
                          )}
                          {item.status === "invalid" && (
                            <ul className="transfer-errors">
                              {item.errors.map((e) => <li key={e}>{e}</li>)}
                            </ul>
                          )}
                        </td>
                        <td>
                          <select
                            value={actions[item.code]}
                            disabled={item.allowedActions.length < 2}
                            onChange={(e) => setActions((a) => ({ ...a, [item.code]: e.target.value as ImportAction }))}
                            aria-label={t("transfer.col.action")}
                          >
                            {ACTION_ORDER.filter((a) => item.allowedActions.includes(a)).map((a) => (
                              <option key={a} value={a}>{t(`transfer.action.${a}`)}</option>
                            ))}
                          </select>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="transfer-checks">
                {plan.connections.length > 0 && (
                  <div>
                    <strong>{t("transfer.connections")}</strong>
                    <ul>
                      {plan.connections.map((c) => (
                        <li key={c.name} className={c.status === "found" ? "ok" : "bad"}>
                          {c.status === "found" ? <Check size={13} /> : <AlertTriangle size={13} />}
                          <code>{c.name}</code> — {t(`transfer.connection.${c.status}`)}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                <div className="hint" style={{ margin: 0 }}>
                  {t("transfer.imagesSummary", { added: plan.assetsNew, reused: plan.assetsReused })}
                </div>
              </div>

              <Warnings tokens={plan.warnings.filter((w) => !w.startsWith("connectionMissing") && !w.startsWith("connectionProviderMismatch"))} />

              {overwrites > 0 && (
                <div className="transfer-note">
                  <PackageOpen size={14} />
                  <span>{t("transfer.overwriteNote", { count: overwrites })}</span>
                </div>
              )}
              {hasInvalid && (
                <div className="transfer-note">
                  <AlertTriangle size={14} />
                  <span>{t("transfer.invalidNote")}</span>
                </div>
              )}
            </>
          )}

          {/* ---- step 3: result ---- */}
          {result && (
            <div className="transfer-result">
              <CheckCircle2 size={28} />
              <strong>{t("transfer.result.title")}</strong>
              <div className="transfer-stats">
                {result.created > 0 && <span>{t("transfer.result.created", { count: result.created })}</span>}
                {result.updated > 0 && <span>{t("transfer.result.updated", { count: result.updated })}</span>}
                {result.copies > 0 && <span>{t("transfer.result.copies", { count: result.copies })}</span>}
                {result.skipped > 0 && <span>{t("transfer.result.skipped", { count: result.skipped })}</span>}
              </div>
              <p className="hint">{t("transfer.result.undoHint")}</p>
              <Warnings tokens={result.warnings} />
            </div>
          )}

          {err && (
            <div className="error small">
              <AlertTriangle /> <span>{err}</span>
            </div>
          )}
        </div>

        <footer className="transfer-footer">
          {result ? (
            <button className="btn primary" onClick={onClose}>
              <Check size={14} /> {t("transfer.result.done")}
            </button>
          ) : (
            <>
              <button className="btn" onClick={onClose} disabled={applying}>{t("common.cancel")}</button>
              <button className="btn primary" onClick={() => void apply()} disabled={!plan || analyzing || applying || willChange === 0}>
                {applying ? <Loader2 size={14} className="spin" /> : <ArrowRight size={14} />}{" "}
                {t("transfer.import.apply", { count: willChange })}
              </button>
            </>
          )}
        </footer>
      </div>
    </div>
  );
}
