import { useEffect, useRef, useState } from "react";
import { useDesigner, type ElementLocation } from "../store";
import { pageDimensions, type Band, type ElementType } from "../types";
import { ElementView } from "./ElementView";

export function Canvas() {
  const layoutMode = useDesigner((s) => s.report?.layoutMode);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement;
      if (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable) return;
      const s = useDesigner.getState();
      const mod = e.ctrlKey || e.metaKey;

      if (e.key === "Delete" || e.key === "Backspace") {
        e.preventDefault();
        s.removeSelected();
      } else if (mod && e.key.toLowerCase() === "z") {
        e.preventDefault();
        e.shiftKey ? s.redo() : s.undo();
      } else if (mod && e.key.toLowerCase() === "y") {
        e.preventDefault();
        s.redo();
      } else if (mod && e.key.toLowerCase() === "c") {
        s.copySelection();
      } else if (mod && e.key.toLowerCase() === "v") {
        e.preventDefault();
        s.paste();
      } else if (mod && e.key.toLowerCase() === "d") {
        e.preventDefault();
        s.duplicateSelection();
      } else if (mod && e.key === "]") {
        e.preventDefault();
        s.reorderSelection(e.shiftKey ? "front" : "forward");
      } else if (mod && e.key === "[") {
        e.preventDefault();
        s.reorderSelection(e.shiftKey ? "back" : "backward");
      } else if (e.key.startsWith("Arrow")) {
        e.preventDefault();
        const step = e.shiftKey ? 10 : 1;
        s.nudge(
          e.key === "ArrowLeft" ? -step : e.key === "ArrowRight" ? step : 0,
          e.key === "ArrowUp" ? -step : e.key === "ArrowDown" ? step : 0,
        );
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  if (!layoutMode) return <div className="canvas-wrap empty">Select or create a report</div>;
  return layoutMode === "free" ? <FreeCanvas /> : <BandedCanvas />;
}

function useDropHandler(pageRef: React.RefObject<HTMLDivElement | null>, location: ElementLocation) {
  const zoom = useDesigner((s) => s.zoom);
  const addElement = useDesigner((s) => s.addElement);
  const mutateElement = useDesigner((s) => s.mutateElement);

  return (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    const rect = pageRef.current!.getBoundingClientRect();
    const x = Math.round((e.clientX - rect.left) / zoom);
    const y = Math.round((e.clientY - rect.top) / zoom);
    const tool = e.dataTransfer.getData("application/x-tool") as ElementType;
    const field = e.dataTransfer.getData("application/x-field");
    if (tool) {
      addElement(tool, x, y, location);
    } else if (field) {
      addElement("field", x, y, location);
      const id = useDesigner.getState().selectedIds[0];
      if (id) mutateElement(id, (el) => (el.value = field));
    }
  };
}

function FreeCanvas() {
  const report = useDesigner((s) => s.report)!;
  const zoom = useDesigner((s) => s.zoom);
  const select = useDesigner((s) => s.select);
  const guides = useDesigner((s) => s.guides);
  const pageRef = useRef<HTMLDivElement>(null);
  const [marquee, setMarquee] = useState<{ x0: number; y0: number; x1: number; y1: number } | null>(null);
  const { width, height } = pageDimensions(report.page);
  const onDrop = useDropHandler(pageRef, { container: "body" });

  const onPagePointerDown = (e: React.PointerEvent) => {
    if (e.target !== pageRef.current) return;
    select([]);
    const rect = pageRef.current!.getBoundingClientRect();
    const start = { x: (e.clientX - rect.left) / zoom, y: (e.clientY - rect.top) / zoom };
    setMarquee({ x0: start.x, y0: start.y, x1: start.x, y1: start.y });

    const move = (ev: PointerEvent) => {
      setMarquee((m) => (m ? { ...m, x1: (ev.clientX - rect.left) / zoom, y1: (ev.clientY - rect.top) / zoom } : m));
    };
    const up = () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      setMarquee((m) => {
        if (m) {
          const [lx, rx] = [Math.min(m.x0, m.x1), Math.max(m.x0, m.x1)];
          const [ty, by] = [Math.min(m.y0, m.y1), Math.max(m.y0, m.y1)];
          if (rx - lx > 3 || by - ty > 3) {
            const hits = (report.body?.elements ?? [])
              .filter((el) => el.bounds.x < rx && el.bounds.x + el.bounds.width > lx && el.bounds.y < by && el.bounds.y + el.bounds.height > ty)
              .map((el) => el.id);
            select(hits);
          }
        }
        return null;
      });
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
  };

  return (
    <div className="canvas-wrap" onDragOver={(e) => e.preventDefault()} onDrop={onDrop}>
      <div
        className="page"
        ref={pageRef}
        onPointerDown={onPagePointerDown}
        style={{ width, height, transform: `scale(${zoom})`, transformOrigin: "top center" }}
      >
        <Margins />
        {(report.body?.elements ?? []).map((el) => (
          <ElementView key={el.id} element={el} />
        ))}
        {guides.x !== null && <div className="guide guide-v" style={{ left: guides.x }} />}
        {guides.y !== null && <div className="guide guide-h" style={{ top: guides.y }} />}
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

function BandedCanvas() {
  const report = useDesigner((s) => s.report)!;
  const zoom = useDesigner((s) => s.zoom);
  const { width } = pageDimensions(report.page);
  const usableWidth = width - report.page.margins.left - report.page.margins.right;

  return (
    <div className="canvas-wrap">
      <div className="band-stack" style={{ width: usableWidth, transform: `scale(${zoom})`, transformOrigin: "top center" }}>
        {report.bands.length === 0 && <div className="hint" style={{ padding: 24 }}>Add a band to start.</div>}
        {report.bands.map((band, index) => (
          <BandStrip key={`${band.type}-${index}`} band={band} index={index} width={usableWidth} />
        ))}
      </div>
    </div>
  );
}

function BandStrip({ band, index, width }: { band: Band; index: number; width: number }) {
  const zoom = useDesigner((s) => s.zoom);
  const selectedBand = useDesigner((s) => s.selectedBand);
  const selectBand = useDesigner((s) => s.selectBand);
  const select = useDesigner((s) => s.select);
  const checkpoint = useDesigner((s) => s.checkpoint);
  const patchBand = useDesigner((s) => s.patchBand);
  const areaRef = useRef<HTMLDivElement>(null);
  const onDrop = useDropHandler(areaRef, { container: "band", bandIndex: index });

  const beginHeightResize = (e: React.PointerEvent) => {
    e.stopPropagation();
    const startY = e.clientY;
    const h0 = band.height;
    checkpoint();
    const onMove = (ev: PointerEvent) => {
      patchBand(index, (b) => (b.height = Math.max(8, Math.round(h0 + (ev.clientY - startY) / zoom))));
    };
    const onUp = () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
    };
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
  };

  return (
    <div className={`band ${selectedBand === index ? "sel" : ""}`}>
      <button className="band-tag" onClick={() => selectBand(index)} title="Edit band">
        {BAND_LABEL[band.type]}
        {band.type === "detail" && band.dataSource ? ` · ${band.dataSource}` : ""}
      </button>
      <div
        className="band-area"
        ref={areaRef}
        style={{ width, height: band.height }}
        onDragOver={(e) => e.preventDefault()}
        onDrop={onDrop}
        onPointerDown={(e) => {
          if (e.target === areaRef.current) select([]);
        }}
      >
        {band.elements.map((el) => (
          <ElementView key={el.id} element={el} />
        ))}
        <div className="band-resize" onPointerDown={beginHeightResize} title="Drag to resize band" />
      </div>
    </div>
  );
}

function Margins() {
  const report = useDesigner((s) => s.report)!;
  const m = report.page.margins;
  return <div className="page-margins" style={{ inset: `${m.top}px ${m.right}px ${m.bottom}px ${m.left}px` }} />;
}

const BAND_LABEL: Record<Band["type"], string> = {
  reportHeader: "Report header",
  pageHeader: "Page header",
  groupHeader: "Group header",
  detail: "Detail",
  groupFooter: "Group footer",
  pageFooter: "Page footer",
  reportFooter: "Report footer",
};
