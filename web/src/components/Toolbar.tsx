import {
  Eye,
  FileBarChart2,
  FileDown,
  FilePlus2,
  LayoutGrid,
  PencilRuler,
  Plus,
  Redo2,
  Rows3,
  Save,
  Settings,
  SquareDashed,
  Undo2,
  ZoomIn,
  ZoomOut,
} from "lucide-react";
import { useDesigner } from "../store";
import type { BandType } from "../types";

const BAND_OPTIONS: { value: BandType; label: string }[] = [
  { value: "reportHeader", label: "Report header" },
  { value: "pageHeader", label: "Page header" },
  { value: "groupHeader", label: "Group header" },
  { value: "detail", label: "Detail" },
  { value: "groupFooter", label: "Group footer" },
  { value: "pageFooter", label: "Page footer" },
  { value: "reportFooter", label: "Report footer" },
];

interface ToolbarProps {
  tab: "design" | "preview";
  busy: boolean;
  onSetTab: (tab: "design" | "preview") => void;
  onNew: () => void;
  onShowStart: () => void;
  onSettings: () => void;
  onSave: () => void;
  onExport: () => void;
}

export function Toolbar({
  tab,
  busy,
  onSetTab,
  onNew,
  onShowStart,
  onSettings,
  onSave,
  onExport,
}: ToolbarProps) {
  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
  const dirty = useDesigner((s) => s.dirty);
  const mutate = useDesigner((s) => s.mutate);
  const zoom = useDesigner((s) => s.zoom);
  const setZoom = useDesigner((s) => s.setZoom);
  const setLayoutMode = useDesigner((s) => s.setLayoutMode);
  const addBand = useDesigner((s) => s.addBand);
  const undo = useDesigner((s) => s.undo);
  const redo = useDesigner((s) => s.redo);
  const canUndo = useDesigner((s) => s.past.length > 0);
  const canRedo = useDesigner((s) => s.future.length > 0);

  const hasReport = !!report;

  return (
    <div className="toolbar">
      <div className="brand">
        <FileBarChart2 size={18} />
        <span>JetReportDesigner</span>
      </div>

      <div className="divider" />

      <div className="group">
        <button className="btn" onClick={onShowStart} title="Start screen — open or create a report" aria-label="Start screen">
          <LayoutGrid />
          <span>Start</span>
        </button>
        <button className="btn icon" onClick={onNew} disabled={busy} title="New blank report" aria-label="New blank report">
          <FilePlus2 />
        </button>
      </div>

      {report && (
        <>
          <div className="divider" />
          <input
            className="report-title"
            value={report.name}
            title="Report name"
            aria-label="Report name"
            placeholder="Untitled report"
            onChange={(e) => mutate((r) => (r.name = e.target.value), false)}
            onBlur={(e) => {
              if (!e.target.value.trim()) mutate((r) => (r.name = "Untitled report"), false);
            }}
          />
        </>
      )}

      <div className="divider" />

      <button
        className="btn primary"
        onClick={onSave}
        disabled={busy || !report || !reportId}
        title="Save (Ctrl+S)"
      >
        <Save />
        <span>Save</span>
        {dirty && <span className="dot" aria-label="unsaved changes" />}
      </button>

      <div className="divider" />

      <div className="group">
        <button className="btn icon" onClick={undo} disabled={!canUndo} title="Undo (Ctrl+Z)" aria-label="Undo">
          <Undo2 />
        </button>
        <button className="btn icon" onClick={redo} disabled={!canRedo} title="Redo (Ctrl+Shift+Z)" aria-label="Redo">
          <Redo2 />
        </button>
      </div>

      <div className="divider" />

      <div className="segmented" role="group" aria-label="Layout mode">
        <button
          className={report?.layoutMode === "free" ? "on" : ""}
          onClick={() => setLayoutMode("free")}
          disabled={!hasReport}
          title="Free layout"
        >
          <SquareDashed /> Free
        </button>
        <button
          className={report?.layoutMode === "banded" ? "on" : ""}
          onClick={() => setLayoutMode("banded")}
          disabled={!hasReport}
          title="Banded layout"
        >
          <Rows3 /> Banded
        </button>
      </div>

      {report?.layoutMode === "banded" && (
        <label className="menu-select" title="Add a band">
          <Plus />
          <select
            value=""
            onChange={(e) => {
              if (e.target.value) addBand(e.target.value as BandType);
              e.currentTarget.value = "";
            }}
            aria-label="Add band"
          >
            <option value="">Band…</option>
            {BAND_OPTIONS.map((b) => (
              <option key={b.value} value={b.value}>{b.label}</option>
            ))}
          </select>
        </label>
      )}

      <div className="divider" />

      <div className="group">
        <button className="btn icon" onClick={() => setZoom(zoom - 0.1)} disabled={!hasReport} title="Zoom out" aria-label="Zoom out">
          <ZoomOut />
        </button>
        <span
          className="zoom-label"
          onClick={() => setZoom(1)}
          title="Reset zoom to 100%"
          role="button"
        >
          {Math.round(zoom * 100)}%
        </span>
        <button className="btn icon" onClick={() => setZoom(zoom + 0.1)} disabled={!hasReport} title="Zoom in" aria-label="Zoom in">
          <ZoomIn />
        </button>
      </div>

      <div className="divider" />

      <div className="segmented" role="group" aria-label="View">
        <button
          className={tab === "design" ? "on" : ""}
          onClick={() => onSetTab("design")}
          disabled={!hasReport}
          title="Design view"
        >
          <PencilRuler /> Design
        </button>
        <button
          className={tab === "preview" ? "on" : ""}
          onClick={() => onSetTab("preview")}
          disabled={!hasReport}
          title="Preview"
        >
          <Eye /> Preview
        </button>
      </div>

      <div className="spacer" />

      <button className="btn icon" onClick={onSettings} title="Settings" aria-label="Settings">
        <Settings />
      </button>

      <button className="btn outline" onClick={onExport} disabled={busy || !report} title="Export as PDF">
        <FileDown />
        <span>Export PDF</span>
      </button>
    </div>
  );
}
