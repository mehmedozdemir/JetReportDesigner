import { useEffect, useMemo, useState } from "react";
import { FileBarChart2, FileText, LayoutGrid, Rows3, Search, SquareDashed, Trash2, Users, X } from "lucide-react";
import { isDesigner, useAuth } from "../auth";
import { timeAgo } from "../time";
import type { ReportDefinition, ReportSummary } from "../types";
import { TeamPage } from "./TeamPage";

type View = "reports" | "team";

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
  const [view, setView] = useState<View>("reports");
  const canEdit = isDesigner(useAuth((s) => s.user));

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

        {canEdit && (
          <div className="segmented" role="group" aria-label="Start screen section">
            <button className={view === "reports" ? "on" : ""} onClick={() => setView("reports")}>
              <LayoutGrid size={14} /> Reports
            </button>
            <button className={view === "team" ? "on" : ""} onClick={() => setView("team")}>
              <Users size={14} /> Team
            </button>
          </div>
        )}

        {onClose && (
          <button className="btn icon" onClick={onClose} title="Close" aria-label="Close start screen">
            <X />
          </button>
        )}
      </header>

      <div className="start-scroll">
        {view === "team" ? (
          <TeamPage />
        ) : (
          <>
            <section className="start-section">
              <h3>New</h3>
              {!canEdit && <p className="hint">Viewer role — sign in as a Designer to create reports.</p>}
              <div className="start-cards">
                <button className="start-card" onClick={() => onBlank("free")} disabled={busy || !canEdit}>
                  <span className="start-card-icon"><SquareDashed /></span>
                  <span className="start-card-title">Blank — Free layout</span>
                  <span className="start-card-sub">Place elements anywhere on a fixed canvas</span>
                </button>
                <button className="start-card" onClick={() => onBlank("banded")} disabled={busy || !canEdit}>
                  <span className="start-card-icon"><Rows3 /></span>
                  <span className="start-card-title">Blank — Banded report</span>
                  <span className="start-card-sub">Header / detail / footer bands that repeat per row</span>
                </button>
                {samples.map((s) => (
                  <button
                    key={s.name}
                    className="start-card tpl"
                    onClick={() => onSample(s.name)}
                    disabled={busy || !canEdit}
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
                          {canEdit && (
                            <button
                              className="mini danger"
                              title="Delete report"
                              aria-label={`Delete ${r.name}`}
                              onClick={() => setConfirmId(r.id)}
                            >
                              <Trash2 />
                            </button>
                          )}
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>
    </div>
  );
}
