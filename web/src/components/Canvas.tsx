import { useEffect, useRef, useState } from "react";
import type { LucideIcon } from "lucide-react";
import { AlignEndHorizontal, AlignStartHorizontal, ClipboardPaste, Rows2, Rows3, Table2 } from "lucide-react";
import { useDesigner, type ElementLocation } from "../store";
import { usePrefs } from "../prefs";
import { pageDimensions, type Band, type BandType, type ElementType } from "../types";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { ElementView } from "./ElementView";
import { Ruler } from "./Ruler";
import { backgroundImageCss } from "../image";

const BAND_META: Record<Band["type"], { label: string; Icon: LucideIcon }> = {
  reportHeader: { label: "Report header", Icon: AlignStartHorizontal },
  pageHeader: { label: "Page header", Icon: AlignStartHorizontal },
  groupHeader: { label: "Group header", Icon: Rows2 },
  detail: { label: "Detail", Icon: Table2 },
  groupFooter: { label: "Group footer", Icon: Rows2 },
  pageFooter: { label: "Page footer", Icon: AlignEndHorizontal },
  reportFooter: { label: "Report footer", Icon: AlignEndHorizontal },
};

const BAND_ORDER: BandType[] = [
  "reportHeader",
  "pageHeader",
  "groupHeader",
  "detail",
  "groupFooter",
  "pageFooter",
  "reportFooter",
];

type Refs = {
  wrapRef: React.RefObject<HTMLDivElement>;
  pageRef: React.RefObject<HTMLDivElement>;
  onBgContextMenu: (e: React.MouseEvent) => void;
};

export function Canvas({ active = true }: { active?: boolean }) {
  const layoutMode = useDesigner((s) => s.report?.layoutMode);
  const showRulers = usePrefs((s) => s.showRulers);
  const showGrid = usePrefs((s) => s.showGrid);
  const unit = usePrefs((s) => s.rulerUnit);
  const wrapRef = useRef<HTMLDivElement>(null);
  const pageRef = useRef<HTMLDivElement>(null);
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);

  const onBgContextMenu = (e: React.MouseEvent) => {
    // ignore right-clicks that land on an element or a resize handle
    if ((e.target as HTMLElement).closest("[data-el-id], .handle, .band-resize")) return;
    e.preventDefault();
    setMenu({ x: e.clientX, y: e.clientY });
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!active) return;
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
  }, [active]);

  if (!layoutMode) {
    return (
      <div className="canvas-frame">
        <div className="canvas-wrap empty">Select or create a report</div>
      </div>
    );
  }

  const refs: Refs = { wrapRef, pageRef, onBgContextMenu };
  return (
    <div className={`canvas-frame${showRulers ? " ruled" : ""}${showGrid ? "" : " no-grid"}`}>
      {showRulers && (
        <>
          <div className="ruler-corner">{unit}</div>
          <Ruler axis="x" wrapRef={wrapRef} pageRef={pageRef} />
          <Ruler axis="y" wrapRef={wrapRef} pageRef={pageRef} />
        </>
      )}
      {layoutMode === "free" ? <FreeCanvas {...refs} /> : <BandedCanvas {...refs} />}
      {menu && (
        <ContextMenu
          x={menu.x}
          y={menu.y}
          items={layoutMode === "banded" ? bandMenuItems() : freeMenuItems()}
          onClose={() => setMenu(null)}
        />
      )}
    </div>
  );
}

function bandMenuItems(): MenuItem[] {
  const s = useDesigner.getState();
  const bands = s.report?.bands ?? [];
  return BAND_ORDER.map((type) => {
    const idx = bands.findIndex((b) => b.type === type);
    return {
      label: BAND_META[type].label,
      icon: BAND_META[type].Icon,
      checked: idx >= 0,
      onClick: () => (idx >= 0 ? s.removeBand(idx) : s.addBand(type)),
    };
  });
}

function freeMenuItems(): MenuItem[] {
  const s = useDesigner.getState();
  return [
    { label: "Paste", icon: ClipboardPaste, onClick: () => s.paste() },
    { sep: true },
    {
      label: "Select all",
      onClick: () => s.select((s.report?.body?.elements ?? []).map((e) => e.id)),
    },
  ];
}

function useDropHandler(pageRef: React.RefObject<HTMLDivElement>, location: ElementLocation) {
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

function FreeCanvas({ wrapRef, pageRef, onBgContextMenu }: Refs) {
  const report = useDesigner((s) => s.report)!;
  const zoom = useDesigner((s) => s.zoom);
  const select = useDesigner((s) => s.select);
  const guides = useDesigner((s) => s.guides);
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
    <div
      className="canvas-wrap"
      ref={wrapRef}
      onDragOver={(e) => e.preventDefault()}
      onDrop={onDrop}
      onContextMenu={onBgContextMenu}
    >
      <div
        className="page"
        ref={pageRef}
        onPointerDown={onPagePointerDown}
        style={{
          width,
          height,
          transform: `scale(${zoom})`,
          transformOrigin: "top center",
          ...backgroundImageCss(report.page.backgroundImage),
        }}
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

function BandedCanvas({ wrapRef, pageRef, onBgContextMenu }: Refs) {
  const report = useDesigner((s) => s.report)!;
  const zoom = useDesigner((s) => s.zoom);
  const { width } = pageDimensions(report.page);
  const usableWidth = width - report.page.margins.left - report.page.margins.right;

  return (
    <div className="canvas-wrap" ref={wrapRef} onContextMenu={onBgContextMenu}>
      <div
        className="band-stack"
        ref={pageRef}
        style={{
          width: usableWidth,
          transform: `scale(${zoom})`,
          transformOrigin: "top center",
          ...backgroundImageCss(report.page.backgroundImage),
        }}
      >
        {report.bands.length === 0 && (
          <div className="empty-hint-block">
            <Rows3 />
            <div>No bands yet — right-click the canvas to add one.</div>
          </div>
        )}
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

  const { label, Icon } = BAND_META[band.type];
  const isGroup = band.type === "groupHeader" || band.type === "groupFooter";
  const groupLevelCount = useDesigner((s) =>
    new Set(s.report!.bands.filter((b) => b.type === "groupHeader" || b.type === "groupFooter").map((b) => b.groupLevel ?? 0)).size,
  );

  return (
    <div className={`band ${selectedBand === index ? "sel" : ""}`}>
      <button className="band-tag" onClick={() => selectBand(index)} title="Edit band">
        <Icon size={12} />
        {label}
        {band.type === "detail" && band.dataSource ? ` · ${band.dataSource}` : ""}
        {isGroup && groupLevelCount > 1 ? ` · level ${band.groupLevel ?? 0}` : ""}
      </button>
      <div
        className="band-area"
        ref={areaRef}
        style={{ width, height: band.height, ...backgroundImageCss(band.backgroundImage) }}
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
