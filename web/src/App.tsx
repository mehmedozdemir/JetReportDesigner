import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, FileText } from "lucide-react";
import { api } from "./api";
import { useDesigner } from "./store";
import { emptyFreeReport, type ReportDefinition, type ReportSummary } from "./types";
import { Canvas } from "./components/Canvas";
import { Toolbar } from "./components/Toolbar";
import { Toolbox } from "./components/Toolbox";
import { DataPanel } from "./components/DataPanel";
import { ParametersPanel } from "./components/ParametersPanel";
import { ProblemsPanel } from "./components/ProblemsPanel";
import { PropertiesPanel } from "./components/PropertiesPanel";
import { PreviewPane } from "./components/PreviewPane";

export function App() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [samples, setSamples] = useState<{ name: string; definition: ReportDefinition }[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState<"design" | "preview">("design");
  const [paramValues, setParamValues] = useState<Record<string, string>>({});

  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
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
      const { id: _id, ...definition } = sample.definition;
      const created = await api.createReport({
        ...definition,
        name: `${definition.name} ${new Date().toISOString().slice(11, 19)}`,
      });
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
      <Toolbar
        reports={reports}
        samples={samples}
        tab={tab}
        busy={busy}
        onSetTab={setTab}
        onOpen={(id) => void open(id)}
        onNew={() => void createReport()}
        onSample={(name) => void createFromSample(name)}
        onSave={() => void save()}
        onExport={() => void exportPdf()}
      />

      <div className="left">
        <Toolbox />
        <DataPanel key={reportId ?? "none"} />
        <ParametersPanel />
        <ProblemsPanel />
      </div>

      <div className="center">
        {!report && (
          <div className="canvas-wrap empty">
            <div className="empty-hint-block">
              <FileText />
              <div>Open a report, start a new one, or pick a sample.</div>
            </div>
          </div>
        )}
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

      {error && (
        <div className="toast" role="alert">
          <div className="error">
            <AlertTriangle />
            <span>{error}</span>
          </div>
        </div>
      )}
    </div>
  );
}
