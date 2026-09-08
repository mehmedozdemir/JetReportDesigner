import { useCallback, useEffect, useState } from "react";
import { api } from "./api";
import { emptyFreeReport, type ReportResponse, type ReportSummary } from "./types";

export function App() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [selected, setSelected] = useState<ReportResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(async () => {
    try {
      setReports(await api.listReports());
    } catch (e) {
      setError(String(e));
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const open = async (id: string) => {
    setError(null);
    try {
      setSelected(await api.getReport(id));
    } catch (e) {
      setError(String(e));
    }
  };

  const createReport = async () => {
    setBusy(true);
    setError(null);
    try {
      const name = `Untitled ${new Date().toISOString().slice(0, 19).replace("T", " ")}`;
      const created = await api.createReport(emptyFreeReport(name));
      await refresh();
      setSelected(created);
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    if (!selected) return;
    setBusy(true);
    setError(null);
    try {
      const saved = await api.updateReport(
        selected.id,
        selected.definition,
        selected.concurrencyToken,
      );
      setSelected(saved);
      await refresh();
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="app">
      <div className="topbar">
        <span>JetReportDesigner</span>
        <span style={{ color: "var(--muted)", fontWeight: 400 }}>Phase 0 skeleton</span>
        <div style={{ flex: 1 }} />
        <button className="primary" onClick={createReport} disabled={busy}>
          New report
        </button>
        <button onClick={save} disabled={busy || !selected}>
          Save
        </button>
      </div>

      <div className="sidebar">
        <h2>Reports ({reports.length})</h2>
        <ul className="report-list">
          {reports.map((r) => (
            <li
              key={r.id}
              className={selected?.id === r.id ? "active" : ""}
              onClick={() => void open(r.id)}
            >
              <div>{r.name}</div>
              <div className="meta">
                {r.layoutMode} · {new Date(r.updatedAtUtc).toLocaleString()}
              </div>
            </li>
          ))}
        </ul>
      </div>

      <div className="canvas-wrap">
        <div className="page">
          <div className="empty-hint">
            {selected
              ? `${selected.definition.name} — empty ${selected.definition.layoutMode} canvas (designer arrives in Phase 1)`
              : "Select or create a report"}
          </div>
        </div>
      </div>

      {error && <div className="error">{error}</div>}
    </div>
  );
}
