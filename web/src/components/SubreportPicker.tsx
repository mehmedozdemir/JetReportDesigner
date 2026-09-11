import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { FileStack, X } from "lucide-react";
import { api } from "../api";
import { useDesigner } from "../store";
import type { ReportParameter, ReportSummary, SubreportSpec } from "../types";
import { FormulaField } from "./FormulaDialog";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function SubreportPicker({
  value,
  fieldNames,
  onChange,
}: {
  value: SubreportSpec;
  fieldNames: string[];
  onChange: (next: SubreportSpec) => void;
}) {
  const currentReportId = useDesigner((s) => s.reportId);
  const [open, setOpen] = useState(false);
  const [childName, setChildName] = useState<string | null>(null);
  const [childParams, setChildParams] = useState<ReportParameter[] | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!value.reportId) {
      setChildName(null);
      setChildParams(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    api
      .getReport(value.reportId)
      .then((r) => {
        if (cancelled) return;
        setChildName(r.definition.name);
        setChildParams(r.definition.parameters ?? []);
      })
      .catch(() => {
        if (!cancelled) {
          setChildName("(report not found)");
          setChildParams([]);
        }
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [value.reportId]);

  const setParam = (name: string, expr: string) =>
    onChange({ ...value, parameters: { ...value.parameters, [name]: expr } });

  return (
    <div className="field">
      <span>Report</span>
      <div className="format-input">
        <input
          readOnly
          value={loading ? "Loading…" : (childName ?? "")}
          placeholder="Choose a saved report…"
          onClick={() => setOpen(true)}
        />
        <button className="mini" onClick={() => setOpen(true)}>Choose</button>
      </div>

      {childParams && childParams.length > 0 && (
        <>
          <h3>Parameters</h3>
          {childParams.map((p) => (
            <FormulaField
              key={p.name}
              label={p.label || p.name}
              value={value.parameters[p.name] ?? ""}
              fields={fieldNames}
              onChange={(v) => setParam(p.name, v)}
            />
          ))}
        </>
      )}

      {open && (
        <SubreportDialog
          excludeReportId={currentReportId}
          onPick={(id) => onChange({ reportId: id, parameters: {} })}
          onClose={() => setOpen(false)}
        />
      )}
    </div>
  );
}

function SubreportDialog({
  excludeReportId,
  onPick,
  onClose,
}: {
  excludeReportId: string | null;
  onPick: (reportId: string) => void;
  onClose: () => void;
}) {
  const [reports, setReports] = useState<ReportSummary[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [query, setQuery] = useState("");

  useEffect(() => {
    api.listReports().then(setReports).catch((e) => {
      setErr(msg(e));
      setReports([]);
    });
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const q = query.trim().toLowerCase();
  const filtered = (reports ?? []).filter(
    (r) => r.id !== excludeReportId && (!q || r.name.toLowerCase().includes(q)),
  );

  return createPortal(
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-label="Choose report"
        style={{ width: "min(420px, 92vw)" }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><FileStack size={16} /> Choose report</h2>
          <button className="mini" onClick={onClose} aria-label="Close"><X size={14} /></button>
        </header>

        <div style={{ display: "flex", flexDirection: "column", gap: 8, padding: "4px 2px" }}>
          <input
            value={query}
            placeholder="Search reports"
            onChange={(e) => setQuery(e.target.value)}
            autoFocus
          />

          {reports === null ? (
            <div className="imgpick-hint">Loading…</div>
          ) : filtered.length === 0 ? (
            <div className="imgpick-hint">No reports found.</div>
          ) : (
            <ul className="start-list" style={{ maxHeight: 280, overflowY: "auto" }}>
              {filtered.map((r) => (
                <li key={r.id} className="start-row">
                  <button className="start-row-main" onClick={() => { onPick(r.id); onClose(); }}>
                    <FileStack size={14} />
                    <span className="start-row-name">{r.name}</span>
                    <span className="start-row-meta"><span className="chip">{r.layoutMode}</span></span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          {err && <div className="imgpick-err">{err}</div>}
        </div>
      </div>
    </div>,
    document.body,
  );
}
