import { useCallback } from "react";
import { useDesigner } from "../store";
import type { Bounds } from "../types";

export type ResizeHandle = "nw" | "n" | "ne" | "e" | "se" | "s" | "sw" | "w";

interface Start {
  pointerX: number;
  pointerY: number;
  bounds: Record<string, Bounds>;
}

const GRID = 4;
const snap = (v: number) => Math.round(v / GRID) * GRID;

/**
 * Pointer-driven move/resize for the current selection. One undo checkpoint is
 * pushed on pointer-down; live updates during the drag skip history.
 */
export function useCanvasDrag() {
  const zoom = useDesigner((s) => s.zoom);
  const checkpoint = useDesigner((s) => s.checkpoint);
  const mutate = useDesigner((s) => s.mutate);

  const beginMove = useCallback(
    (e: React.PointerEvent, ids: string[]) => {
      e.stopPropagation();
      const report = useDesigner.getState().report;
      if (!report?.body) return;
      (e.target as Element).setPointerCapture?.(e.pointerId);

      const bounds: Record<string, Bounds> = {};
      report.body.elements.forEach((el) => {
        if (ids.includes(el.id)) bounds[el.id] = { ...el.bounds };
      });
      const start: Start = { pointerX: e.clientX, pointerY: e.clientY, bounds };
      checkpoint();

      const onMove = (ev: PointerEvent) => {
        const dx = (ev.clientX - start.pointerX) / zoom;
        const dy = (ev.clientY - start.pointerY) / zoom;
        mutate((r) => {
          r.body?.elements.forEach((el) => {
            const b = start.bounds[el.id];
            if (b) {
              el.bounds.x = snap(b.x + dx);
              el.bounds.y = snap(b.y + dy);
            }
          });
        }, false);
      };
      const onUp = (ev: PointerEvent) => {
        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);
        (e.target as Element).releasePointerCapture?.(ev.pointerId);
      };
      window.addEventListener("pointermove", onMove);
      window.addEventListener("pointerup", onUp);
    },
    [zoom, checkpoint, mutate],
  );

  const beginResize = useCallback(
    (e: React.PointerEvent, id: string, handle: ResizeHandle) => {
      e.stopPropagation();
      const report = useDesigner.getState().report;
      const element = report?.body?.elements.find((el) => el.id === id);
      if (!element) return;
      (e.target as Element).setPointerCapture?.(e.pointerId);

      const b0 = { ...element.bounds };
      const px = e.clientX;
      const py = e.clientY;
      checkpoint();

      const onMove = (ev: PointerEvent) => {
        const dx = (ev.clientX - px) / zoom;
        const dy = (ev.clientY - py) / zoom;
        mutate((r) => {
          const el = r.body?.elements.find((x) => x.id === id);
          if (!el) return;
          let { x, y, width, height } = b0;
          if (handle.includes("e")) width = b0.width + dx;
          if (handle.includes("s")) height = b0.height + dy;
          if (handle.includes("w")) {
            width = b0.width - dx;
            x = b0.x + dx;
          }
          if (handle.includes("n")) {
            height = b0.height - dy;
            y = b0.y + dy;
          }
          el.bounds.x = snap(x);
          el.bounds.y = snap(y);
          el.bounds.width = Math.max(0, snap(width));
          el.bounds.height = Math.max(0, snap(height));
        }, false);
      };
      const onUp = (ev: PointerEvent) => {
        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);
        (e.target as Element).releasePointerCapture?.(ev.pointerId);
      };
      window.addEventListener("pointermove", onMove);
      window.addEventListener("pointerup", onUp);
    },
    [zoom, checkpoint, mutate],
  );

  return { beginMove, beginResize };
}
