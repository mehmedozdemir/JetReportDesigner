import { useState } from "react";
import { createPortal } from "react-dom";
import {
  ArrowDown,
  ArrowDownToLine,
  ArrowUp,
  ArrowUpToLine,
  ClipboardPaste,
  Copy,
  CopyPlus,
  Eraser,
  FileStack,
  FunctionSquare,
  Paintbrush2,
  Scissors,
  SlidersHorizontal,
  Trash2,
} from "lucide-react";
import { useDesigner } from "../store";
import type { ReportElement, ReportStyle } from "../types";
import { useCanvasDrag, type ResizeHandle } from "../hooks/useCanvasDrag";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { FormulaDialog } from "./FormulaDialog";
import { ChartPreview } from "./ChartPreview";
import { BarcodePreview } from "./BarcodePreview";
import { MatrixPreview } from "./MatrixPreview";
import { backgroundImageCss, imageSrc } from "../image";

const HANDLES: ResizeHandle[] = ["nw", "n", "ne", "e", "se", "s", "sw", "w"];
const EMPTY_STYLES: Record<string, ReportStyle> = {};

function effectiveStyle(el: ReportElement, styles: Record<string, ReportStyle>): ReportStyle {
  const base = el.styleRef ? styles[el.styleRef] ?? {} : {};
  const inline = el.style ?? {};
  return {
    ...base,
    ...inline,
    font: { ...base.font, ...inline.font },
    border: inline.border ?? base.border,
    padding: inline.padding ?? base.padding,
  };
}

const EDITABLE = new Set(["label", "field", "pageInfo"]);

export function ElementView({ element }: { element: ReportElement }) {
  const selectedIds = useDesigner((s) => s.selectedIds);
  const select = useDesigner((s) => s.select);
  const mutateElement = useDesigner((s) => s.mutateElement);
  const styles = useDesigner((s) => s.report?.styles) ?? EMPTY_STYLES;
  const dataSources = useDesigner((s) => s.report?.dataSources);
  const { beginMove, beginResize } = useCanvasDrag();
  const [editing, setEditing] = useState(false);
  const [lastDown, setLastDown] = useState(0);
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const [formulaOpen, setFormulaOpen] = useState(false);

  const canFormula = EDITABLE.has(element.type);
  const fieldNames = (dataSources ?? []).flatMap((src) => src.fields.map((f) => f.name));

  const isLabel = element.type === "label";
  const currentText = isLabel ? element.text ?? "" : element.value ?? "";
  const commitText = (value: string) => {
    setEditing(false);
    mutateElement(element.id, (el) => {
      if (isLabel) el.text = value;
      else el.value = value;
    });
  };

  const selected = selectedIds.includes(element.id);
  const s = effectiveStyle(element, styles);
  const b = element.bounds;

  const boxStyle: React.CSSProperties = {
    position: "absolute",
    left: b.x,
    top: b.y,
    width: b.width,
    height: element.type === "line"
      ? Math.max(b.height, s.border ? maxEdge(s.border) : 1)
      : element.canGrow ? "auto" : b.height,
    minHeight: element.canGrow && element.type !== "line" ? b.height : undefined,
    boxSizing: "border-box",
    fontFamily: s.font?.family ? `${s.font.family}, sans-serif` : "inherit",
    fontSize: s.font?.size ? `${s.font.size}pt` : undefined,
    fontWeight: s.font?.bold ? 700 : undefined,
    fontStyle: s.font?.italic ? "italic" : undefined,
    color: s.color ?? undefined,
    background:
      element.type === "line"
        ? s.border?.color ?? s.color ?? "currentColor"
        : s.background ?? undefined,
    border:
      element.type === "line"
        ? undefined
        : s.border
          ? `${maxEdge(s.border)}px solid ${s.border.color}`
          : undefined,
    textAlign: (s.align as React.CSSProperties["textAlign"]) ?? "left",
    display: "flex",
    flexDirection: "column",
    justifyContent: s.vAlign === "middle" ? "center" : s.vAlign === "bottom" ? "flex-end" : "flex-start",
    padding: s.padding ? `${s.padding.top}px ${s.padding.right}px ${s.padding.bottom}px ${s.padding.left}px` : undefined,
    overflow: element.canGrow ? "visible" : "hidden",
    whiteSpace: "pre-wrap",
    cursor: "move",
    userSelect: "none",
    outline: selected ? "1px solid var(--accent)" : "1px dashed rgba(148,163,184,.6)",
    outlineOffset: selected ? "0" : "-1px",
    ...(element.type === "line" ? null : backgroundImageCss(s.backgroundImage)),
  };

  const onPointerDown = (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    const now = Date.now();
    if (now - lastDown < 300 && EDITABLE.has(element.type)) {
      setLastDown(0);
      setEditing(true);
      return;
    }
    setLastDown(now);

    if (!selected) select([element.id], e.shiftKey);
    const ids = useDesigner.getState().selectedIds.includes(element.id)
      ? useDesigner.getState().selectedIds
      : [element.id];
    beginMove(e, ids);
  };

  const onContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!selected) select([element.id]);
    setMenu({ x: e.clientX, y: e.clientY });
  };

  const menuItems = (): MenuItem[] => {
    const st = useDesigner.getState();
    return [
      { label: "Cut", icon: Scissors, onClick: () => { st.copySelection(); st.removeSelected(); } },
      { label: "Copy", icon: Copy, onClick: () => st.copySelection() },
      { label: "Paste", icon: ClipboardPaste, onClick: () => st.paste() },
      { label: "Duplicate", icon: CopyPlus, onClick: () => st.duplicateSelection() },
      { sep: true },
      { label: "Bring to front", icon: ArrowUpToLine, onClick: () => st.reorderSelection("front") },
      { label: "Bring forward", icon: ArrowUp, onClick: () => st.reorderSelection("forward") },
      { label: "Send backward", icon: ArrowDown, onClick: () => st.reorderSelection("backward") },
      { label: "Send to back", icon: ArrowDownToLine, onClick: () => st.reorderSelection("back") },
      { sep: true },
      { label: "Delete", icon: Trash2, danger: true, onClick: () => st.removeSelected() },
      { sep: true },
      {
        label: "Format",
        icon: Paintbrush2,
        children: [
          {
            label: "Clear",
            icon: Eraser,
            onClick: () =>
              st.mutateSelected((el) => {
                el.style = null;
                el.styleRef = null;
                el.format = null;
              }),
          },
        ],
      },
      ...(canFormula
        ? [
            {
              label: "Formula",
              icon: FunctionSquare,
              onClick: () => {
                st.select([element.id]);
                setFormulaOpen(true);
              },
            } as MenuItem,
          ]
        : []),
      {
        label: "Properties",
        icon: SlidersHorizontal,
        onClick: () => {
          st.select([element.id]);
          st.revealInspector();
        },
      },
    ];
  };

  return (
    <div
      style={boxStyle}
      onPointerDown={editing ? undefined : onPointerDown}
      onContextMenu={onContextMenu}
      onDoubleClick={() => EDITABLE.has(element.type) && setEditing(true)}
      data-el-id={element.id}
    >
      {EDITABLE.has(element.type) &&
        (editing ? (
          <textarea
            className="inline-edit"
            autoFocus
            defaultValue={currentText}
            onBlur={(e) => commitText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                commitText((e.target as HTMLTextAreaElement).value);
              } else if (e.key === "Escape") {
                setEditing(false);
              }
            }}
            onPointerDown={(e) => e.stopPropagation()}
          />
        ) : (
          <span>{labelText(element)}</span>
        ))}
      {element.type === "image" &&
        (element.image?.source ? (
          <img
            src={imageSrc(element.image.source)}
            alt=""
            draggable={false}
            style={{
              width: "100%",
              height: "100%",
              objectFit: (element.image.fit === "fill" ? "fill" : element.image.fit === "cover" ? "cover" : "contain"),
            }}
          />
        ) : (
          <span className="img-ph">image</span>
        ))}
      {element.type === "chart" && element.chart && (
        <ChartPreview spec={element.chart} width={b.width} height={b.height} />
      )}

      {element.type === "subreport" && (
        <span className="img-ph" style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
          <FileStack size={14} />
          {element.subreport?.reportId ? "Subreport" : "Subreport (no report chosen)"}
        </span>
      )}

      {element.type === "barcode" && element.barcode && (
        <BarcodePreview spec={element.barcode} width={b.width} height={b.height} />
      )}

      {element.type === "matrix" && element.matrix && <MatrixPreview spec={element.matrix} />}

      {element.type === "table" && element.table && (
        <table className="tbl-preview">
          {element.table.showHeader && (
            <thead>
              <tr>
                {element.table.columns.map((c, i) => (
                  <th key={i} style={{ width: c.width, textAlign: c.align }}>{c.header}</th>
                ))}
              </tr>
            </thead>
          )}
          <tbody>
            {[0, 1].map((r) => (
              <tr key={r}>
                {element.table!.columns.map((c, i) => (
                  <td key={i} style={{ width: c.width, textAlign: c.align }}>{c.value}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected &&
        HANDLES.map((h) => (
          <span
            key={h}
            className={`handle handle-${h}`}
            onPointerDown={(e) => beginResize(e, element.id, h)}
          />
        ))}

      {menu && (
        <ContextMenu x={menu.x} y={menu.y} items={menuItems()} onClose={() => setMenu(null)} />
      )}

      {formulaOpen &&
        createPortal(
          <FormulaDialog
            initial={currentText}
            fields={fieldNames}
            onApply={(v) => {
              commitText(v);
              setFormulaOpen(false);
            }}
            onClose={() => setFormulaOpen(false)}
          />,
          document.body,
        )}
    </div>
  );
}

function labelText(el: ReportElement): string {
  if (el.type === "label") return el.text ?? "";
  return el.value ?? "";
}

function maxEdge(b: { top: number; right: number; bottom: number; left: number }): number {
  return Math.max(b.top, b.right, b.bottom, b.left);
}
