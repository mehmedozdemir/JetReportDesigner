import { useState } from "react";
import { useDesigner } from "../store";
import type { ReportElement, ReportStyle } from "../types";
import { useCanvasDrag, type ResizeHandle } from "../hooks/useCanvasDrag";

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
  const { beginMove, beginResize } = useCanvasDrag();
  const [editing, setEditing] = useState(false);
  const [lastDown, setLastDown] = useState(0);

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
    height: element.type === "line" ? Math.max(b.height, s.border ? maxEdge(s.border) : 1) : b.height,
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
    overflow: "hidden",
    whiteSpace: "pre-wrap",
    cursor: "move",
    userSelect: "none",
    outline: selected ? "1px solid var(--accent)" : "1px dashed rgba(148,163,184,.6)",
    outlineOffset: selected ? "0" : "-1px",
  };

  const onPointerDown = (e: React.PointerEvent) => {
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

  return (
    <div
      style={boxStyle}
      onPointerDown={editing ? undefined : onPointerDown}
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
      {element.type === "image" && <span className="img-ph">image</span>}
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
