import { useState } from "react";
import {
  ChevronDown,
  Eye,
  FileBarChart2,
  FileDown,
  FilePlus2,
  FileSpreadsheet,
  FileText,
  LayoutGrid,
  LogOut,
  PencilRuler,
  Redo2,
  Rows3,
  Save,
  Settings,
  SquareDashed,
  Undo2,
  ZoomIn,
  ZoomOut,
} from "lucide-react";
import { isDesigner, useAuth } from "../auth";
import { useDesigner } from "../store";
import { ContextMenu } from "./ContextMenu";

interface ToolbarProps {
  tab: "design" | "preview";
  busy: boolean;
  onSetTab: (tab: "design" | "preview") => void;
  onNew: () => void;
  onShowStart: () => void;
  onSettings: () => void;
  onSave: () => void;
  onExport: (format: "pdf" | "xlsx") => void;
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
  const undo = useDesigner((s) => s.undo);
  const redo = useDesigner((s) => s.redo);
  const canUndo = useDesigner((s) => s.past.length > 0);
  const canRedo = useDesigner((s) => s.future.length > 0);
  const [exportMenu, setExportMenu] = useState<{ x: number; y: number } | null>(null);
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const canEdit = isDesigner(user);

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
        <button
          className="btn icon"
          onClick={onNew}
          disabled={busy || !canEdit}
          title={canEdit ? "New blank report" : "Viewer role cannot create reports"}
          aria-label="New blank report"
        >
          <FilePlus2 />
        </button>
      </div>

      {report && (
        <>
          <div className="divider" />
          {canEdit ? (
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
          ) : (
            <span className="report-title" title="Report name">
              {report.name}
            </span>
          )}
        </>
      )}

      <div className="divider" />

      {canEdit && (
        <>
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
        </>
      )}

      {report && (
        <span
          className="mode-badge"
          title="Layout mode is chosen when the report is created and cannot be changed here"
        >
          {report.layoutMode === "banded" ? <Rows3 /> : <SquareDashed />}
          {report.layoutMode === "banded" ? "Banded" : "Free"}
        </span>
      )}

      <div className="divider" />

      {canEdit ? (
        <>
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
        </>
      ) : (
        report && (
          <span className="mode-badge" title="Viewers only get the read-only preview">
            <Eye size={14} /> Preview
          </span>
        )
      )}

      <div className="spacer" />

      {user && (
        <span className="user-badge" title={user.email}>
          {user.email}
          <span className="role">{canEdit ? "Designer" : "Viewer"}</span>
        </span>
      )}
      <button className="btn icon" onClick={logout} title="Sign out" aria-label="Sign out">
        <LogOut />
      </button>

      <div className="divider" />

      <button className="btn icon" onClick={onSettings} title="Settings" aria-label="Settings">
        <Settings />
      </button>

      <button
        className="btn outline"
        onClick={(e) => setExportMenu({ x: e.currentTarget.getBoundingClientRect().left, y: e.currentTarget.getBoundingClientRect().bottom + 4 })}
        disabled={busy || !report}
        title="Export"
      >
        <FileDown />
        <span>Export</span>
        <ChevronDown size={13} />
      </button>
      {exportMenu && (
        <ContextMenu
          x={exportMenu.x}
          y={exportMenu.y}
          items={[
            { label: "Export as PDF", icon: FileText, onClick: () => onExport("pdf") },
            { label: "Export as Excel", icon: FileSpreadsheet, onClick: () => onExport("xlsx") },
          ]}
          onClose={() => setExportMenu(null)}
        />
      )}
    </div>
  );
}
