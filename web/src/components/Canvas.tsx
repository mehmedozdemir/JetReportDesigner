import { useEffect, useRef, useState } from "react";
import { useDesigner } from "../store";
import { pageDimensions, type ElementType } from "../types";
import { ElementView } from "./ElementView";

export function Canvas() {
  const report = useDesigner((s) => s.report);
  const zoom = useDesigner((s) => s.zoom);
  const select = useDesigner((s) => s.select);
  const addElement = useDesigner((s) => s.addElement);
  const removeSelected = useDesigner((s) => s.removeSelected);
  const nudge = useDesigner((s) => s.nudge);
  const undo = useDesigner((s) => s.undo);
  const redo = useDesigner((s) => s.redo);
  const pageRef = useRef<HTMLDivElement>(null);
  const [marquee, setMarquee] = useState<null | { x0: number; y0: number; x1: number; y1: number }>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      if (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT") return;
      if (e.key === "Delete" || e.key === "Backspace") {
        e.preventDefault();
        removeSelected();
      } else if (e.key === "z" && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        e.shiftKey ? redo() : undo();
      } else if (e.key === "y" && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        redo();
      } else if (e.key.startsWith("Arrow")) {
        e.preventDefault();
        const step = e.shiftKey ? 10 : 1;
        nudge(
          e.key === "ArrowLeft" ? -step : e.key === "ArrowRight" ? step : 0,
          e.key === "ArrowUp" ? -step : e.key === "ArrowDown" ? step : 0,
        );
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [removeSelected, nudge, undo, redo]);

  if (!report?.body) {
    return <div className="canvas-wrap empty">Select or create a report</div>;
  }

  const { width, height } = pageDimensions(report.page);

  const toPagePoint = (clientX: number, clientY: number) => {
    const rect = pageRef.current!.getBoundingClientRect();
    return { x: (clientX - rect.left) / zoom, y: (clientY - rect.top) / zoom };
  };

  const onPagePointerDown = (e: React.PointerEvent) => {
    if (e.target !== pageRef.current) return;
    select([]);
    const p = toPagePoint(e.clientX, e.clientY);
    setMarquee({ x0: p.x, y0: p.y, x1: p.x, y1: p.y });
    const onMove = (ev: PointerEvent) => {
      const q = toPagePoint(ev.clientX, ev.clientY);
      setMarquee((m) => (m ? { ...m, x1: q.x, y1: q.y } : m));
    };
    const onUp = () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      setMarquee((m) => {
        if (m && report.body) {
          const [lx, rx] = [Math.min(m.x0, m.x1), Math.max(m.x0, m.x1)];
          const [ty, by] = [Math.min(m.y0, m.y1), Math.max(m.y0, m.y1)];
          const hits = report.body.elements
            .filter((el) => el.bounds.x < rx && el.bounds.x + el.bounds.width > lx && el.bounds.y < by && el.bounds.y + el.bounds.height > ty)
            .map((el) => el.id);
          if (hits.length) select(hits);
        }
        return null;
      });
    };
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
  };

  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const p = toPagePoint(e.clientX, e.clientY);
    const tool = e.dataTransfer.getData("application/x-tool") as ElementType;
    const field = e.dataTransfer.getData("application/x-field");
    if (tool) {
      addElement(tool, Math.round(p.x), Math.round(p.y));
    } else if (field) {
      addElement("field", Math.round(p.x), Math.round(p.y));
      const state = useDesigner.getState();
      const id = state.selectedIds[0];
      state.mutate((r) => {
        const el = r.body?.elements.find((x) => x.id === id);
        if (el) el.value = field;
      });
    }
  };

  return (
    <div className="canvas-wrap" onDragOver={(e) => e.preventDefault()} onDrop={onDrop}>
      <div
        className="page"
        ref={pageRef}
        onPointerDown={onPagePointerDown}
        style={{
          width,
          height,
          transform: `scale(${zoom})`,
          transformOrigin: "top center",
        }}
      >
        <PageMargins report={report} />
        {report.body.elements.map((el) => (
          <ElementView key={el.id} element={el} />
        ))}
        {marquee && (
          <div
            className="marquee"
            style={{
              left: Math.min(marquee.x0, marquee.x1),
              top: Math.min(marquee.y0, marquee.y1),
              width: Math.abs(marquee.x1 - marquee.x0),
              height: Math.abs(marquee.y1 - marquee.y0),
            }}
          />
        )}
      </div>
    </div>
  );
}

function PageMargins({ report }: { report: NonNullable<ReturnType<typeof useDesigner.getState>["report"]> }) {
  const m = report.page.margins;
  return (
    <div
      className="page-margins"
      style={{ inset: `${m.top}px ${m.right}px ${m.bottom}px ${m.left}px` }}
    />
  );
}
