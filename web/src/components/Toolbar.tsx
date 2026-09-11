import { useMemo, useState } from "react";
import {
  ChevronDown,
  FileBarChart2,
  FileDown,
  FilePlus2,
  FileSpreadsheet,
  FileText,
  LayoutGrid,
  LogOut,
  Redo2,
  Save,
  Settings,
  Undo2,
} from "lucide-react";
import { isDesigner, useAuth } from "../auth";
import { useDesigner } from "../store";
import { timeAgo } from "../time";
import type { ReportElement } from "../types";
import { ContextMenu } from "./ContextMenu";
import { AlignPicker, common, FONT_FAMILIES, setFont, setStyle, Toggle, VAlignPicker } from "./PropertiesPanel";

interface ToolbarProps {
  busy: boolean;
  onNew: () => void;
  onShowStart: () => void;
  onSettings: () => void;
  onSave: () => void;
  onExport: (format: "pdf" | "xlsx") => void;
}

export function Toolbar({ busy, onNew, onShowStart, onSettings, onSave, onExport }: ToolbarProps) {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const canEdit = isDesigner(user);

  const report = useDesigner((s) => s.report);
  const reportId = useDesigner((s) => s.reportId);
  const dirty = useDesigner((s) => s.dirty);
  const savedAtUtc = useDesigner((s) => s.savedAtUtc);
  const mutate = useDesigner((s) => s.mutate);
  const [exportMenu, setExportMenu] = useState<{ x: number; y: number } | null>(null);
  const [userMenu, setUserMenu] = useState<{ x: number; y: number } | null>(null);

  const exportButton = (
    <>
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
    </>
  );

  const userChip = user && (
    <>
      <button
        className="btn user-chip"
        onClick={(e) => setUserMenu({ x: e.currentTarget.getBoundingClientRect().right, y: e.currentTarget.getBoundingClientRect().bottom + 4 })}
        title={user.email}
      >
        <span className="user-avatar">{user.email[0]?.toUpperCase()}</span>
        <span className="user-email">{user.email}</span>
        <ChevronDown size={12} />
      </button>
      {userMenu && (
        <ContextMenu
          x={userMenu.x}
          y={userMenu.y}
          items={[
            { label: user.email, disabled: true },
            { label: canEdit ? "Designer" : "Viewer", disabled: true },
            { sep: true },
            { label: "Sign out", icon: LogOut, onClick: logout, danger: true },
          ]}
          onClose={() => setUserMenu(null)}
        />
      )}
    </>
  );

  if (!canEdit) {
    // Viewer: a single, minimal row — no document actions, just navigation + export.
    return (
      <div className="toolbar">
        <div className="brand">
          <FileBarChart2 size={18} />
          <span>JetReportDesigner</span>
        </div>
        <div className="divider" />
        <button className="btn" onClick={onShowStart} title="Start screen — open a report" aria-label="Start screen">
          <LayoutGrid />
          <span>Start</span>
        </button>
        {report && (
          <>
            <div className="divider" />
            <span className="report-title" title="Report name">
              {report.name}
            </span>
          </>
        )}
        <div className="spacer" />
        {userChip}
        <div className="divider" />
        {exportButton}
      </div>
    );
  }

  return (
    <div className="toolbar toolbar-2row">
      <div className="toolbar-row">
        <div className="brand">
          <FileBarChart2 size={18} />
          <span>JetReportDesigner</span>
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
            <SaveStatus dirty={dirty} savedAtUtc={savedAtUtc} />
          </>
        )}

        <div className="spacer" />

        <button className="btn icon" onClick={onSettings} title="Settings" aria-label="Settings">
          <Settings />
        </button>
        <div className="divider" />
        {userChip}
      </div>

      <div className="toolbar-row">
        <div className="group">
          <button className="btn" onClick={onShowStart} title="Start screen — open or create a report" aria-label="Start screen">
            <LayoutGrid />
            <span>Start</span>
          </button>
          <button className="btn icon" onClick={onNew} disabled={busy} title="New blank report" aria-label="New blank report">
            <FilePlus2 />
          </button>
        </div>

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
          <UndoRedoGroup />
        </div>

        <TextFormatGroup />

        <div className="spacer" />

        {exportButton}
      </div>
    </div>
  );
}

function SaveStatus({ dirty, savedAtUtc }: { dirty: boolean; savedAtUtc: string | null }) {
  if (dirty) {
    return (
      <span className="save-status" title="Changes not saved yet">
        Unsaved changes
      </span>
    );
  }
  if (savedAtUtc) {
    return (
      <span className="save-status" title={new Date(savedAtUtc).toLocaleString()}>
        Saved {timeAgo(savedAtUtc)}
      </span>
    );
  }
  return null;
}

function UndoRedoGroup() {
  const undo = useDesigner((s) => s.undo);
  const redo = useDesigner((s) => s.redo);
  const canUndo = useDesigner((s) => s.past.length > 0);
  const canRedo = useDesigner((s) => s.future.length > 0);
  return (
    <>
      <button className="btn icon" onClick={undo} disabled={!canUndo} title="Undo (Ctrl+Z)" aria-label="Undo">
        <Undo2 />
      </button>
      <button className="btn icon" onClick={redo} disabled={!canRedo} title="Redo (Ctrl+Shift+Z)" aria-label="Redo">
        <Redo2 />
      </button>
    </>
  );
}

/**
 * Quick-access text formatting — font, size, bold/italic, alignment, colors — shown only
 * when the current selection includes at least one text-bearing element (label/field/page
 * info), mirroring the same rule and the same store mutations as the Properties Panel's
 * "Common" section, so the two stay in sync automatically.
 */
function TextFormatGroup() {
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const mutateSelected = useDesigner((s) => s.mutateSelected);

  const elements = useMemo(() => {
    if (!report) return [] as ReportElement[];
    const all = [...(report.body?.elements ?? []), ...report.bands.flatMap((b) => b.elements)];
    const set = new Set(selectedIds);
    return all.filter((e) => set.has(e.id));
  }, [report, selectedIds]);

  const textCount = elements.filter((e) => e.type === "label" || e.type === "field" || e.type === "pageInfo").length;
  if (textCount === 0) return null;

  const fontFamily = common(elements, (e) => e.style?.font?.family ?? null);
  const fontSize = common(elements, (e) => e.style?.font?.size ?? null);
  const bold = common(elements, (e) => !!e.style?.font?.bold);
  const italic = common(elements, (e) => !!e.style?.font?.italic);
  const align = common(elements, (e) => e.style?.align ?? "left");
  const vAlign = common(elements, (e) => e.style?.vAlign ?? "top");
  const color = common(elements, (e) => e.style?.color ?? null);
  const background = common(elements, (e) => e.style?.background ?? null);

  return (
    <>
      <div className="divider" />
      <div className="group format-group">
        <select
          className="mini-select"
          title="Font"
          value={fontFamily ?? ""}
          onChange={(e) => mutateSelected((el) => setFont(el, "family", e.target.value || null))}
        >
          <option value="">{fontFamily && !FONT_FAMILIES.includes(fontFamily) ? fontFamily : "Font"}</option>
          {FONT_FAMILIES.map((f) => (
            <option key={f} value={f}>{f}</option>
          ))}
        </select>
        <input
          className="mini-num"
          type="number"
          title="Font size (pt)"
          value={fontSize ?? ""}
          placeholder="pt"
          onChange={(e) => e.target.value && mutateSelected((el) => setFont(el, "size", Number(e.target.value)))}
        />
        <Toggle label="B" active={!!bold} onClick={() => mutateSelected((e) => setFont(e, "bold", !bold))} />
        <Toggle label="I" active={!!italic} onClick={() => mutateSelected((e) => setFont(e, "italic", !italic))} />
      </div>

      <div className="divider" />
      <div className="group format-group">
        <AlignPicker value={align ?? "left"} onChange={(v) => mutateSelected((e) => setStyle(e, "align", v))} />
        <VAlignPicker value={vAlign ?? "top"} onChange={(v) => mutateSelected((e) => setStyle(e, "vAlign", v))} />
      </div>

      <div className="divider" />
      <div className="group format-group">
        <input
          className="mini-color"
          type="color"
          title="Text color"
          value={color ?? "#111827"}
          onChange={(e) => mutateSelected((el) => setStyle(el, "color", e.target.value))}
        />
        <input
          className="mini-color"
          type="color"
          title="Background color"
          value={background ?? "#ffffff"}
          onChange={(e) => mutateSelected((el) => setStyle(el, "background", e.target.value))}
        />
      </div>
    </>
  );
}
