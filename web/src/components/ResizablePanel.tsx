import { useRef } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";

const MIN_WIDTH = 200;
const MAX_WIDTH = 480;
const COLLAPSED_WIDTH = 28;

export { COLLAPSED_WIDTH };

/**
 * A drag-to-resize, click-to-collapse wrapper for the left/right side panels. Occupies a
 * named CSS grid area ("left" or "right") in the parent `.app` grid; width itself is driven
 * by the parent setting `grid-template-columns` from the same width/collapsed state this
 * component reports back via onWidthChange/onToggleCollapsed.
 */
export function ResizablePanel({
  side,
  width,
  collapsed,
  onWidthChange,
  onToggleCollapsed,
  children,
}: {
  side: "left" | "right";
  width: number;
  collapsed: boolean;
  onWidthChange: (width: number) => void;
  onToggleCollapsed: (collapsed: boolean) => void;
  children: React.ReactNode;
}) {
  const drag = useRef<{ startX: number; startWidth: number } | null>(null);

  const startDrag = (e: React.MouseEvent) => {
    e.preventDefault();
    drag.current = { startX: e.clientX, startWidth: width };
    const onMove = (ev: MouseEvent) => {
      if (!drag.current) return;
      const delta = ev.clientX - drag.current.startX;
      const raw = side === "left" ? drag.current.startWidth + delta : drag.current.startWidth - delta;
      onWidthChange(Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, raw)));
    };
    const onUp = () => {
      drag.current = null;
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  };

  if (collapsed) {
    return (
      <button
        className={`panel-collapsed-strip panel-collapsed-${side}`}
        style={{ gridArea: side }}
        onClick={() => onToggleCollapsed(false)}
        title={`Expand ${side === "left" ? "the toolbox panel" : "the properties panel"}`}
        aria-label="Expand panel"
      >
        {side === "left" ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
      </button>
    );
  }

  return (
    <div className="panel-slot" style={{ gridArea: side }}>
      {children}
      <div className={`resize-handle resize-${side}`} onMouseDown={startDrag}>
        <button
          type="button"
          className="panel-collapse-btn"
          onClick={(e) => {
            e.stopPropagation();
            onToggleCollapsed(true);
          }}
          title="Collapse panel"
          aria-label="Collapse panel"
        >
          {side === "left" ? <ChevronLeft size={12} /> : <ChevronRight size={12} />}
        </button>
      </div>
    </div>
  );
}
