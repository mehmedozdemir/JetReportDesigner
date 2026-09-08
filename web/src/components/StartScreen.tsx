import { useEffect, useMemo, useState } from "react";
import {
  FileBarChart2,
  FileText,
  Rows3,
  Search,
  SquareDashed,
  Trash2,
  X,
} from "lucide-react";
import type { ReportDefinition, ReportSummary } from "../types";

function timeAgo(iso: string): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return "";
  const s = Math.max(0, (Date.now() - t) / 1000);
  if (s < 60) return "just now";
  const m = s / 60;
  if (m < 60) return `${Math.floor(m)}m ago`;
  const h = m / 60;
  if (h < 24) return `${Math.floor(h)}h ago`;
  const d = h / 24;
  if (d < 7) return `${Math.floor(d)}d ago`;
  return new Date(iso).toLocaleDateString();
}

export function StartScreen({
  reports,
  samples,
  busy,
  onBlank,
  onSample,
  onOpen,
  onDelete,
  onClose,
}: {
  reports: ReportSummary[];
  samples: { name: string; definition: ReportDefinition }[];
  busy: boolean;
  onBlank: (mode: "free" | "banded") => void;
  onSample: (name: string) => void;
  onOpen: (id: string) => void;
  onDelete: (id: string) => void;
  onClose?: () => void;
}) {
  const [query, setQuery] = useState("");
  const [confirmId, setConfirmId] = useState<string | null>(null);

  useEffect(() => {
    if (!onClose) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return [...reports]
      .filter((r) => !q || r.name.toLowerCase().includes(q))
      .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
  }, [reports, query]);

  return (
    <div className="start-screen">
      <header className="start-head">
        <div className="brand">
          <FileBarChart2 size={18} />
          <span>JetReportDesigner</span>
        </div>
        {onClose && (
          <button className="btn icon" onClick={onClose} title="Close" aria-label="Close start screen">
            <X />
          </button>
        )}
      </header>

      <div className="start-scroll">
        <section className="start-section">
          <h3>New</h3>
          <div className="start-cards">
            <button className="start-card" onClick={() => onBlank("free")} disabled={busy}>
              <span className="start-card-icon"><SquareDashed /></span>
              <span className="start-card-title">Blank report</span>
              <span className="start-card-sub">Free layout — place elements anywhere</span>
            </button>
            <button className="start-card" onClick={() => onBlank("banded")} disabled={busy}>
              <span className="start-card-icon"><Rows3 /></span>
              <span className="start-card-title">Blank report</span>
              <span className="start-card-sub">Banded — header, detail, footer</span>
            </button>
            {samples.map((s) => (
              <button
                key={s.name}
                className="start-card tpl"
                onClick={() => onSample(s.name)}
                disabled={busy}
              >
                <span className="start-card-icon"><FileBarChart2 /></span>
                <span className="start-card-title">{s.name}</span>
                <span className="start-card-sub">Sample template</span>
                <span className="start-card-tag">Sample</span>
              </button>
            ))}
          </div>
        </section>

        <section className="start-section">
          <div className="start-list-head">
            <h3>Recent reports</h3>
            <label className="start-search">
              <Search size={14} />
              <input
                value={query}
                placeholder="Search reports"
                onChange={(e) => setQuery(e.target.value)}
              />
            </label>
          </div>

          {filtered.length === 0 ? (
            <div className="start-empty">
              <FileText />
              <div>{reports.length === 0 ? "No reports yet." : "No reports match your search."}</div>
              {reports.length === 0 && <p>Start from a blank report or a sample above.</p>}
            </div>
          ) : (
            <ul className="start-list">
              {filtered.map((r) => (
                <li key={r.id} className="start-row">
                  {confirmId === r.id ? (
                    <div className="start-row-confirm">
                      <span>Delete “{r.name}”?</span>
                      <button
                        className="mini danger"
                        onClick={() => {
                          onDelete(r.id);
                          setConfirmId(null);
                        }}
                      >
                        Delete
                      </button>
                      <button className="mini" onClick={() => setConfirmId(null)}>
                        Cancel
                      </button>
                    </div>
                  ) : (
                    <>
                      <button className="start-row-main" onClick={() => onOpen(r.id)} disabled={busy}>
                        <FileText />
                        <span className="start-row-name">{r.name}</span>
                        <span className="start-row-meta">
                          <span className="chip">{r.layoutMode}</span>
                          <span>{timeAgo(r.updatedAtUtc)}</span>
                        </span>
                      </button>
                      <button
                        className="mini danger"
                        title="Delete report"
                        aria-label={`Delete ${r.name}`}
                        onClick={() => setConfirmId(r.id)}
                      >
                        <Trash2 />
                      </button>
                    </>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
