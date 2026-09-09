import { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangle } from "lucide-react";
import { api } from "./api";
import { useDesigner } from "./store";
import { usePrefs } from "./prefs";
import { emptyBandedReport, emptyFreeReport, type ReportDefinition, type ReportSummary } from "./types";
import { Canvas } from "./components/Canvas";
import { SettingsDialog } from "./components/SettingsDialog";
import { StartScreen } from "./components/StartScreen";
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
  const [showStart, setShowStart] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  const autoSaveSeconds = usePrefs((s) => s.autoSaveSeconds);

  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
  const load = useDesigner((s) => s.load);
  const markSaved = useDesigner((s) => s.markSaved);
  const inspectorPulse = useDesigner((s) => s.inspectorPulse);
  const rightRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!inspectorPulse) return;
    const el = rightRef.current;
    if (!el) return;
    el.scrollTo({ top: 0 });
    el.classList.remove("flash");
    void el.offsetWidth; // restart the animation
    el.classList.add("flash");
  }, [inspectorPulse]);

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
      setShowStart(false);
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
      setShowStart(false);
    } catch (e) {
      setError(String(e));
    }
  };

  const removeReport = async (id: string) => {
    setError(null);
    try {
      await api.deleteReport(id);
      if (id === reportId) {
        useDesigner.setState({
          report: null,
          reportId: null,
          concurrencyToken: null,
          selectedIds: [],
          selectedBand: null,
          past: [],
          future: [],
          dirty: false,
        });
      }
      await refresh();
    } catch (e) {
      setError(String(e));
    }
  };

  const createReport = async (mode: "free" | "banded" = "free") => {
    const layout = mode;
    setBusy(true);
    setError(null);
    try {
      const name = `Untitled ${new Date().toISOString().slice(0, 16).replace("T", " ")}`;
      const created = await api.createReport(
        layout === "banded" ? emptyBandedReport(name) : emptyFreeReport(name),
      );
      load(created);
      await refresh();
      setTab("design");
      setShowStart(false);
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

  useEffect(() => {
    if (autoSaveSeconds <= 0) return;
    let running = false;
    const id = window.setInterval(async () => {
      if (running) return;
      const s = useDesigner.getState();
      if (!s.dirty || !s.reportId) return;
      running = true;
      try {
        await save();
      } finally {
        running = false;
      }
    }, autoSaveSeconds * 1000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoSaveSeconds]);

  return (
    <div className="app">
      <Toolbar
        tab={tab}
        busy={busy}
        onSetTab={setTab}
        onNew={() => void createReport()}
        onShowStart={() => setShowStart(true)}
        onSettings={() => setShowSettings(true)}
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
        {report && (
          <>
            <div style={{ flex: 1, minHeight: 0, display: tab === "design" ? "flex" : "none" }}>
              <Canvas active={tab === "design"} />
            </div>
            <div style={{ flex: 1, minHeight: 0, display: tab === "preview" ? "flex" : "none" }}>
              <PreviewPane parameters={paramValues} active={tab === "preview"} />
            </div>
          </>
        )}
      </div>

      <div className="right" ref={rightRef}>
        <PropertiesPanel />
      </div>

      {(!report || showStart) && (
        <div className="start-overlay">
          <StartScreen
            reports={reports}
            samples={samples}
            busy={busy}
            onBlank={(mode) => void createReport(mode)}
            onSample={(name) => void createFromSample(name)}
            onOpen={(id) => void open(id)}
            onDelete={(id) => void removeReport(id)}
            onClose={report ? () => setShowStart(false) : undefined}
          />
        </div>
      )}

      {showSettings && <SettingsDialog onClose={() => setShowSettings(false)} />}

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
