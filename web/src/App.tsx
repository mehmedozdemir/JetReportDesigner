import { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangle, Eye, PencilRuler, ZoomIn, ZoomOut } from "lucide-react";
import { api } from "./api";
import { isDesigner, useAuth } from "./auth";
import { useDesigner } from "./store";
import { usePrefs } from "./prefs";
import {
  emptyBandedReport,
  emptyFreeReport,
  type Orientation,
  type PageSize,
  type ReportDefinition,
  type ReportSummary,
} from "./types";
import { Canvas } from "./components/Canvas";
import { LeftSidebar } from "./components/LeftSidebar";
import { LoginScreen } from "./components/LoginScreen";
import { COLLAPSED_WIDTH, ResizablePanel } from "./components/ResizablePanel";
import { SettingsDialog } from "./components/SettingsDialog";
import { StartScreen } from "./components/StartScreen";
import { Toolbar } from "./components/Toolbar";
import { PropertiesPanel } from "./components/PropertiesPanel";
import { PreviewPane } from "./components/PreviewPane";

export function App() {
  const token = useAuth((s) => s.token);
  const canEdit = isDesigner(useAuth((s) => s.user));
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [samples, setSamples] = useState<{ name: string; category: string; definition: ReportDefinition }[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState<"design" | "preview">("design");
  const [paramValues, setParamValues] = useState<Record<string, string>>({});
  const [showStart, setShowStart] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  const autoSaveSeconds = usePrefs((s) => s.autoSaveSeconds);
  const leftPanelWidth = usePrefs((s) => s.leftPanelWidth);
  const rightPanelWidth = usePrefs((s) => s.rightPanelWidth);
  const leftPanelCollapsed = usePrefs((s) => s.leftPanelCollapsed);
  const rightPanelCollapsed = usePrefs((s) => s.rightPanelCollapsed);
  const setPref = usePrefs((s) => s.set);

  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
  const load = useDesigner((s) => s.load);
  const markSaved = useDesigner((s) => s.markSaved);
  const inspectorPulse = useDesigner((s) => s.inspectorPulse);
  const zoom = useDesigner((s) => s.zoom);
  const setZoom = useDesigner((s) => s.setZoom);
  const rightRef = useRef<HTMLDivElement>(null);

  // A Viewer never gets the design surface — always land on (and stay on) Preview.
  useEffect(() => {
    if (!canEdit) setTab("preview");
  }, [canEdit]);

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
    if (!token) return;
    void refresh();
    void api.listSamples().then(setSamples).catch(() => undefined);
  }, [refresh, token]);

  const createFromSample = async (
    name: string,
    page?: { size: PageSize; orientation: Orientation },
    folderId?: string | null,
  ) => {
    const sample = samples.find((s) => s.name === name);
    if (!sample) return;
    setBusy(true);
    setError(null);
    try {
      const { id: _id, ...definition } = sample.definition;
      const created = await api.createReport({
        ...definition,
        name: `${definition.name} ${new Date().toISOString().slice(11, 19)}`,
        page: page ? { ...definition.page, ...page } : definition.page,
      });
      if (folderId) await api.setReportFolder(created.id, folderId);
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
      setTab(canEdit ? "design" : "preview");
      setShowStart(false);
    } catch (e) {
      setError(String(e));
    }
  };

  const moveReportToFolder = async (id: string, folderId: string | null) => {
    setError(null);
    try {
      await api.setReportFolder(id, folderId);
      await refresh();
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
          savedAtUtc: null,
        });
      }
      await refresh();
    } catch (e) {
      setError(String(e));
    }
  };

  const createReport = async (
    mode: "free" | "banded" = "free",
    page?: { size: PageSize; orientation: Orientation },
    folderId?: string | null,
  ) => {
    const layout = mode;
    setBusy(true);
    setError(null);
    try {
      const name = `Untitled ${new Date().toISOString().slice(0, 16).replace("T", " ")}`;
      const definition = layout === "banded" ? emptyBandedReport(name) : emptyFreeReport(name);
      const created = await api.createReport(
        page ? { ...definition, page: { ...definition.page, ...page } } : definition,
      );
      if (folderId) await api.setReportFolder(created.id, folderId);
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

  const exportAs = async (format: "pdf" | "xlsx") => {
    if (!report) return;
    setBusy(true);
    setError(null);
    try {
      const blob =
        format === "xlsx"
          ? await api.renderXlsxBlob(report, paramValues)
          : await api.renderPdfBlob(report, paramValues);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${report.name || "report"}.${format}`;
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

  if (!token) {
    return <LoginScreen />;
  }

  const leftCol = leftPanelCollapsed ? COLLAPSED_WIDTH : leftPanelWidth;
  const rightCol = rightPanelCollapsed ? COLLAPSED_WIDTH : rightPanelWidth;

  return (
    <div
      className={canEdit ? "app designer-toolbar" : "app viewer-mode"}
      style={canEdit ? { gridTemplateColumns: `${leftCol}px 1fr ${rightCol}px` } : undefined}
    >
      <Toolbar
        busy={busy}
        onNew={() => void createReport()}
        onShowStart={() => setShowStart(true)}
        onSettings={() => setShowSettings(true)}
        onSave={() => void save()}
        onExport={(format) => void exportAs(format)}
      />

      {canEdit && (
        <ResizablePanel
          side="left"
          width={leftPanelWidth}
          collapsed={leftPanelCollapsed}
          onWidthChange={(w) => setPref("leftPanelWidth", w)}
          onToggleCollapsed={(c) => setPref("leftPanelCollapsed", c)}
        >
          <LeftSidebar reportId={reportId} />
        </ResizablePanel>
      )}

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
            {canEdit && (
              <div style={{ flex: 1, minHeight: 0, display: tab === "design" ? "flex" : "none" }}>
                <Canvas active={tab === "design"} />
              </div>
            )}
            <div style={{ flex: 1, minHeight: 0, display: tab === "preview" ? "flex" : "none" }}>
              <PreviewPane parameters={paramValues} active={tab === "preview"} />
            </div>
          </>
        )}

        {canEdit && report && (
          <div className="canvas-hud canvas-hud-left">
            <div className="segmented" role="group" aria-label="View">
              <button className={tab === "design" ? "on" : ""} onClick={() => setTab("design")} title="Design view">
                <PencilRuler /> Design
              </button>
              <button className={tab === "preview" ? "on" : ""} onClick={() => setTab("preview")} title="Preview">
                <Eye /> Preview
              </button>
            </div>
          </div>
        )}
        {canEdit && report && tab === "design" && (
          <div className="canvas-hud canvas-hud-right">
            <div className="group">
              <button className="btn icon" onClick={() => setZoom(zoom - 0.1)} title="Zoom out" aria-label="Zoom out">
                <ZoomOut />
              </button>
              <span className="zoom-label" onClick={() => setZoom(1)} title="Reset zoom to 100%" role="button">
                {Math.round(zoom * 100)}%
              </span>
              <button className="btn icon" onClick={() => setZoom(zoom + 0.1)} title="Zoom in" aria-label="Zoom in">
                <ZoomIn />
              </button>
            </div>
          </div>
        )}
      </div>

      {canEdit && (
        <ResizablePanel
          side="right"
          width={rightPanelWidth}
          collapsed={rightPanelCollapsed}
          onWidthChange={(w) => setPref("rightPanelWidth", w)}
          onToggleCollapsed={(c) => setPref("rightPanelCollapsed", c)}
        >
          <div className="right" ref={rightRef}>
            <PropertiesPanel />
          </div>
        </ResizablePanel>
      )}

      {(!report || showStart) && (
        <div className="start-overlay">
          <StartScreen
            reports={reports}
            samples={samples}
            busy={busy}
            onBlank={(mode, page, folderId) => void createReport(mode, page, folderId)}
            onSample={(name, page, folderId) => void createFromSample(name, page, folderId)}
            onOpen={(id) => void open(id)}
            onDelete={(id) => void removeReport(id)}
            onMoveToFolder={(id, folderId) => void moveReportToFolder(id, folderId)}
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
