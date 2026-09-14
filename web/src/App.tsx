import { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangle, Eye, Loader2, PencilRuler, ZoomIn, ZoomOut } from "lucide-react";
import { Navigate, useLocation, useNavigate, useParams } from "react-router-dom";
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
import { JobNotifications } from "./components/JobNotifications";
import { LeftSidebar } from "./components/LeftSidebar";
import { LoginScreen } from "./components/LoginScreen";
import { COLLAPSED_WIDTH, ResizablePanel } from "./components/ResizablePanel";
import { SettingsDialog } from "./components/SettingsDialog";
import { StartScreen } from "./components/StartScreen";
import { Toolbar } from "./components/Toolbar";
import { PropertiesPanel } from "./components/PropertiesPanel";
import { PreviewPane } from "./components/PreviewPane";

/** Every "list" screen (Reports/Jobs/Team/Email/Schedules) and /settings resolve here too —
 * they all render the same <App/>, which branches on the current route below. Simpler than
 * splitting shared handlers (save, export, create, …) across several route components, and the
 * zustand store already carries report state across any remount that causes. */
export function App() {
  const token = useAuth((s) => s.token);
  const canEdit = isDesigner(useAuth((s) => s.user));
  const navigate = useNavigate();
  const location = useLocation();
  const { id: routeReportId } = useParams<{ id: string }>();

  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [samples, setSamples] = useState<{ name: string; category: string; definition: ReportDefinition }[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [paramValues, setParamValues] = useState<Record<string, string>>({});
  const [loadingReport, setLoadingReport] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
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

  const isDesignerRoute = routeReportId != null;
  const isSettingsRoute = location.pathname === "/settings";
  const tab = location.pathname.endsWith("/preview") ? "preview" : "design";

  useEffect(() => {
    if (!inspectorPulse) return;
    const el = rightRef.current;
    if (!el) return;
    el.scrollTo({ top: 0 });
    el.classList.remove("flash");
    void el.offsetWidth; // restart the animation
    el.classList.add("flash");
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  // Deep-link loading: whenever the URL names a report the store doesn't already have loaded
  // (a fresh tab, a bookmark, or "Open"/"New report" navigating here), fetch and load it. This
  // is the one path both direct navigation and in-app "open" go through. Reads the store
  // imperatively (not via a reactive selector) and only depends on routeReportId — load()'s own
  // store update re-renders this component with a new `reportId`, and subscribing to that here
  // too raced the effect's cleanup against its own in-flight fetch, occasionally leaving
  // loadingReport stuck true.
  useEffect(() => {
    if (!routeReportId || useDesigner.getState().reportId === routeReportId) return;
    let cancelled = false;
    setLoadingReport(true);
    setLoadError(null);
    api
      .getReport(routeReportId)
      .then((r) => {
        if (!cancelled) load(r);
      })
      .catch((e) => {
        if (!cancelled) setLoadError(String(e));
      })
      .finally(() => {
        if (!cancelled) setLoadingReport(false);
      });
    return () => {
      cancelled = true;
    };
  }, [routeReportId, load]);

  // A Viewer never gets the design surface — bounce straight to Preview for the same report.
  useEffect(() => {
    if (isDesignerRoute && !canEdit && tab === "design") {
      navigate(`/reports/${routeReportId}/preview`, { replace: true });
    }
  }, [isDesignerRoute, canEdit, tab, routeReportId, navigate]);

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
      navigate(`/reports/${created.id}/design`);
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const open = (id: string) => navigate(`/reports/${id}/design`);

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
        navigate("/reports");
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
      navigate(`/reports/${created.id}/design`);
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

  if (isSettingsRoute) {
    if (!canEdit) return <Navigate to="/reports" replace />;
    return (
      <>
        <SettingsDialog onClose={() => navigate(-1)} />
        <JobNotifications />
      </>
    );
  }

  if (!isDesignerRoute) {
    const view =
      location.pathname === "/jobs"
        ? "jobs"
        : location.pathname === "/team"
          ? "team"
          : location.pathname === "/email-settings"
            ? "email"
            : location.pathname === "/schedules"
              ? "schedules"
              : "reports";
    return (
      <>
        <StartScreen
          view={view}
          reports={reports}
          samples={samples}
          busy={busy}
          onBlank={(mode, page, folderId) => void createReport(mode, page, folderId)}
          onSample={(name, page, folderId) => void createFromSample(name, page, folderId)}
          onOpen={open}
          onDelete={(id) => void removeReport(id)}
          onMoveToFolder={(id, folderId) => void moveReportToFolder(id, folderId)}
          onSettings={() => navigate("/settings")}
        />
        <JobNotifications />
        {error && (
          <div className="toast" role="alert">
            <div className="error">
              <AlertTriangle />
              <span>{error}</span>
            </div>
          </div>
        )}
      </>
    );
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
        onShowStart={() => navigate("/reports")}
        onSettings={() => navigate("/settings")}
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
        {loadingReport || loadError ? (
          <div className="start-empty" style={{ margin: "auto" }}>
            {loadError ? (
              <>
                <AlertTriangle />
                <div>{loadError}</div>
                <p>
                  <a href="#" onClick={(e) => { e.preventDefault(); navigate("/reports"); }}>
                    Back to Reports
                  </a>
                </p>
              </>
            ) : (
              <div className="share-loading">
                <Loader2 size={20} className="spin" />
              </div>
            )}
          </div>
        ) : (
          <>
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
                  <button
                    className={tab === "design" ? "on" : ""}
                    onClick={() => navigate(`/reports/${routeReportId}/design`)}
                    title="Design view"
                  >
                    <PencilRuler /> Design
                  </button>
                  <button
                    className={tab === "preview" ? "on" : ""}
                    onClick={() => navigate(`/reports/${routeReportId}/preview`)}
                    title="Preview"
                  >
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
          </>
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

      <JobNotifications />

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
