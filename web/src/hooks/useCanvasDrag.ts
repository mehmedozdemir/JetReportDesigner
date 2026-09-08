import { useCallback } from "react";
import { useDesigner } from "../store";
import { pageDimensions } from "../types";
import type { Bounds, ReportElement } from "../types";

export type ResizeHandle = "nw" | "n" | "ne" | "e" | "se" | "s" | "sw" | "w";

const GRID = 4;
const SNAP = 5;
const snap = (v: number) => Math.round(v / GRID) * GRID;

function allElements(): ReportElement[] {
  const r = useDesigner.getState().report;
  if (!r) return [];
  return [...(r.body?.elements ?? []), ...r.bands.flatMap((b) => b.elements)];
}

/** X/Y coordinates other elements (and the page margins) offer as alignment targets. */
function alignmentTargets(movingIds: Set<string>): { xs: number[]; ys: number[] } {
  const r = useDesigner.getState().report;
  const xs: number[] = [];
  const ys: number[] = [];
  if (!r) return { xs, ys };
  for (const el of allElements()) {
    if (movingIds.has(el.id)) continue;
    const b = el.bounds;
    xs.push(b.x, b.x + b.width / 2, b.x + b.width);
    ys.push(b.y, b.y + b.height / 2, b.y + b.height);
  }
  if (r.layoutMode === "free") {
    const { width } = pageDimensions(r.page);
    xs.push(r.page.margins.left, width - r.page.margins.right);
    ys.push(r.page.margins.top);
  }
  return { xs, ys };
}

/** Returns a snap adjustment and the guide coordinate for one axis. */
function snapAxis(edges: number[], targets: number[]): { delta: number; guide: number | null } {
  let best: { delta: number; guide: number } | null = null;
  for (const edge of edges) {
    for (const t of targets) {
      const d = t - edge;
      if (Math.abs(d) <= SNAP && (best === null || Math.abs(d) < Math.abs(best.delta))) {
        best = { delta: d, guide: t };
      }
    }
  }
  return best ? { delta: best.delta, guide: best.guide } : { delta: 0, guide: null };
}

/**
 * Pointer-driven move/resize for the current selection (in body or a band). One
 * undo checkpoint on pointer-down; live updates during the drag skip history.
 */
export function useCanvasDrag() {
  const zoom = useDesigner((s) => s.zoom);
  const checkpoint = useDesigner((s) => s.checkpoint);
  const mutate = useDesigner((s) => s.mutate);

  const beginMove = useCallback(
    (e: React.PointerEvent, ids: string[]) => {
      e.stopPropagation();
      (e.target as Element).setPointerCapture?.(e.pointerId);

      const movingIds = new Set(ids);
      const startBounds: Record<string, Bounds> = {};
      allElements().forEach((el) => {
        if (movingIds.has(el.id)) startBounds[el.id] = { ...el.bounds };
      });
      const primary = startBounds[ids[0]];
      const targets = alignmentTargets(movingIds);
      const px = e.clientX;
      const py = e.clientY;
      checkpoint();
      const setGuides = useDesigner.getState().setGuides;

      const onMove = (ev: PointerEvent) => {
        let dx = snap((ev.clientX - px) / zoom);
        let dy = snap((ev.clientY - py) / zoom);
        let guideX: number | null = null;
        let guideY: number | null = null;

        if (primary && !ev.altKey) {
          const sx = snapAxis([primary.x + dx, primary.x + primary.width / 2 + dx, primary.x + primary.width + dx], targets.xs);
          const sy = snapAxis([primary.y + dy, primary.y + primary.height / 2 + dy, primary.y + primary.height + dy], targets.ys);
          dx += sx.delta;
          dy += sy.delta;
          guideX = sx.guide;
          guideY = sy.guide;
        }
        setGuides(guideX, guideY);

        mutate((r) => {
          const apply = (el: ReportElement) => {
            const b = startBounds[el.id];
            if (b) {
              el.bounds.x = Math.round(b.x + dx);
              el.bounds.y = Math.round(b.y + dy);
            }
          };
          r.body?.elements.forEach(apply);
          r.bands.forEach((band) => band.elements.forEach(apply));
        }, false);
      };
      const onUp = (ev: PointerEvent) => {
        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);
        setGuides(null, null);
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
      const element = allElements().find((el) => el.id === id);
      if (!element) return;
      (e.target as Element).setPointerCapture?.(e.pointerId);

      const b0 = { ...element.bounds };
      const px = e.clientX;
      const py = e.clientY;
      checkpoint();

      const onMove = (ev: PointerEvent) => {
        const dx = (ev.clientX - px) / zoom;
        const dy = (ev.clientY - py) / zoom;
        useDesigner.getState().mutateElement(
          id,
          (el) => {
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
          },
          false,
        );
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
