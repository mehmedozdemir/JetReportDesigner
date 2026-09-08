import { useCallback, useEffect, useState } from "react";
import { api } from "./api";
import { useDesigner } from "./store";
import { emptyFreeReport, type ReportSummary } from "./types";
import { Canvas } from "./components/Canvas";
import { Toolbox } from "./components/Toolbox";
import { DataPanel } from "./components/DataPanel";
import { ParametersPanel } from "./components/ParametersPanel";
import { ProblemsPanel } from "./components/ProblemsPanel";
import { PropertiesPanel } from "./components/PropertiesPanel";
import { PreviewPane } from "./components/PreviewPane";

export function App() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [samples, setSamples] = useState<{ name: string; definition: import("./types").ReportDefinition }[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState<"design" | "preview">("design");
  const [paramValues, setParamValues] = useState<Record<string, string>>({});

  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
  const dirty = useDesigner((s) => s.dirty);
  const zoom = useDesigner((s) => s.zoom);
  const setZoom = useDesigner((s) => s.setZoom);
  const setLayoutMode = useDesigner((s) => s.setLayoutMode);
  const addBand = useDesigner((s) => s.addBand);
  const undo = useDesigner((s) => s.undo);
  const redo = useDesigner((s) => s.redo);
  const canUndo = useDesigner((s) => s.past.length > 0);
  const canRedo = useDesigner((s) => s.future.length > 0);
  const load = useDesigner((s) => s.load);
  const markSaved = useDesigner((s) => s.markSaved);

  const refresh = useCallback(async () => {
    try {
      setReports(await api.listReports());
    } catch (e) {
      setError(String(e));
    }
  }, []);

  useEffect(() => {
    void refresh();
    void api.listSamples().then(setSamples).catch(() => undefined);
  }, [refresh]);

  const createFromSample = async (name: string) => {
    const sample = samples.find((s) => s.name === name);
    if (!sample) return;
    setBusy(true);
    setError(null);
    try {
      const { id, ...definition } = sample.definition;
      void id;
      const created = await api.createReport({ ...definition, name: `${definition.name} ${new Date().toISOString().slice(11, 19)}` });
      load(created);
      await refresh();
      setTab("design");
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const open = async (id: string) => {
    setError(null);
    try {
      load(await api.getReport(id));
      setTab("design");
    } catch (e) {
      setError(String(e));
    }
  };

  const createReport = async () => {
    setBusy(true);
    setError(null);
    try {
      const name = `Untitled ${new Date().toISOString().slice(0, 16).replace("T", " ")}`;
      const created = await api.createReport(emptyFreeReport(name));
      load(created);
      await refresh();
      setTab("design");
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    const state = useDesigner.getState();
    if (!state.report || !state.reportId) return;
    setBusy(true);
    setError(null);
    try {
      const saved = await api.updateReport(state.reportId, state.report, state.concurrencyToken ?? undefined);
      markSaved(saved);
      await refresh();
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const exportPdf = async () => {
    if (!report) return;
    setBusy(true);
    setError(null);
    try {
      const blob = await api.renderPdfBlob(report, paramValues);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${report.name || "report"}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "s") {
        e.preventDefault();
        void save();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="app">
      <div className="topbar">
        <strong>JetReportDesigner</strong>
        <select
          className="report-select"
          value={reportId ?? ""}
          onChange={(e) => e.target.value && void open(e.target.value)}
        >
          <option value="">— open report —</option>
          {reports.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
        <button onClick={createReport} disabled={busy}>New</button>
        {samples.length > 0 && (
          <select
            className="report-select"
            value=""
            onChange={(e) => e.target.value && void createFromSample(e.target.value)}
            disabled={busy}
          >
            <option value="">Sample…</option>
            {samples.map((s) => (
              <option key={s.name} value={s.name}>{s.name}</option>
            ))}
          </select>
        )}
        <button className="primary" onClick={save} disabled={busy || !report || !reportId}>
          Save{dirty ? " *" : ""}
        </button>

        <span className="sep" />
        <button onClick={undo} disabled={!canUndo} title="Undo (Ctrl+Z)">Undo</button>
        <button onClick={redo} disabled={!canRedo} title="Redo (Ctrl+Shift+Z)">Redo</button>

        <span className="sep" />
        <div className="tabs">
          <button
            className={report?.layoutMode === "free" ? "on" : ""}
            disabled={!report}
            onClick={() => setLayoutMode("free")}
          >
            Free
          </button>
          <button
            className={report?.layoutMode === "banded" ? "on" : ""}
            disabled={!report}
            onClick={() => setLayoutMode("banded")}
          >
            Banded
          </button>
        </div>
        {report?.layoutMode === "banded" && (
          <select
            className="add-band"
            value=""
            onChange={(e) => {
              if (e.target.value) addBand(e.target.value as never);
              e.currentTarget.value = "";
            }}
          >
            <option value="">+ band…</option>
            <option value="reportHeader">Report header</option>
            <option value="pageHeader">Page header</option>
            <option value="groupHeader">Group header</option>
            <option value="detail">Detail</option>
            <option value="groupFooter">Group footer</option>
            <option value="pageFooter">Page footer</option>
            <option value="reportFooter">Report footer</option>
          </select>
        )}

        <span className="sep" />
        <button onClick={() => setZoom(zoom - 0.1)} disabled={!report}>−</button>
        <span className="zoom">{Math.round(zoom * 100)}%</span>
        <button onClick={() => setZoom(zoom + 0.1)} disabled={!report}>+</button>

        <span className="sep" />
        <div className="tabs">
          <button className={tab === "design" ? "on" : ""} onClick={() => setTab("design")} disabled={!report}>
            Design
          </button>
          <button className={tab === "preview" ? "on" : ""} onClick={() => setTab("preview")} disabled={!report}>
            Preview
          </button>
        </div>

        <div className="spacer" />
        <button onClick={exportPdf} disabled={busy || !report}>Export PDF</button>
      </div>

      <div className="left">
        <Toolbox />
        <DataPanel key={reportId ?? "none"} />
        <ParametersPanel />
        <ProblemsPanel />
      </div>

      <div className="center">
        {!report && <div className="canvas-wrap empty">Open or create a report to start.</div>}
        {report && (report.parameters?.length ?? 0) > 0 && (
          <div className="param-bar">
            {report.parameters.map((p) => (
              <label key={p.name}>
                {p.label || p.name}
                <input
                  value={paramValues[p.name] ?? ""}
                  placeholder={p.defaultValue == null ? "" : String(p.defaultValue)}
                  onChange={(e) => setParamValues((v) => ({ ...v, [p.name]: e.target.value }))}
                />
              </label>
            ))}
          </div>
        )}
        {report && tab === "design" && <Canvas />}
        {report && tab === "preview" && <PreviewPane parameters={paramValues} />}
      </div>

      <div className="right">
        <PropertiesPanel />
      </div>

      {error && <div className="error toast">{error}</div>}
    </div>
  );
}
