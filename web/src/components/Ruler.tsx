import { useEffect, useMemo, useRef, useState } from "react";
import { useDesigner } from "../store";
import { pxToUnit, usePrefs } from "../prefs";

const NICE_STEPS = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];

type Axis = "x" | "y";

export function Ruler({
  axis,
  wrapRef,
  pageRef,
}: {
  axis: Axis;
  wrapRef: React.RefObject<HTMLDivElement>;
  pageRef: React.RefObject<HTMLDivElement>;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const zoom = useDesigner((s) => s.zoom);
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const unit = usePrefs((s) => s.rulerUnit);
  const [cursor, setCursor] = useState<number | null>(null);

  const selRange = useMemo(() => {
    if (!report || selectedIds.length === 0) return null;
    const all = [...(report.body?.elements ?? []), ...report.bands.flatMap((b) => b.elements)];
    const set = new Set(selectedIds);
    const hit = all.filter((e) => set.has(e.id));
    if (hit.length === 0) return null;
    const lo = Math.min(...hit.map((e) => (axis === "x" ? e.bounds.x : e.bounds.y)));
    const hi = Math.max(...hit.map((e) => (axis === "x" ? e.bounds.x + e.bounds.width : e.bounds.y + e.bounds.height)));
    return { lo, hi };
  }, [report, selectedIds, axis]);

  // ---- draw ----
  useEffect(() => {
    const cv = canvasRef.current;
    const wrap = wrapRef.current;
    if (!cv || !wrap) return;

    let raf = 0;
    const draw = () => {
      raf = 0;
      const page = pageRef.current;
      const dpr = window.devicePixelRatio || 1;
      const rect = cv.getBoundingClientRect();
      const W = Math.max(1, Math.round(rect.width));
      const H = Math.max(1, Math.round(rect.height));
      if (cv.width !== W * dpr || cv.height !== H * dpr) {
        cv.width = W * dpr;
        cv.height = H * dpr;
      }
      const g = cv.getContext("2d");
      if (!g) return;
      g.setTransform(dpr, 0, 0, dpr, 0, 0);

      const css = getComputedStyle(document.documentElement);
      const bg = css.getPropertyValue("--ruler-bg").trim() || "#f4f6fb";
      const tick = css.getPropertyValue("--ruler-tick").trim() || "#9aa3b5";
      const labelCol = css.getPropertyValue("--ruler-label").trim() || "#667085";
      const accent = css.getPropertyValue("--accent").trim() || "#2563eb";

      g.fillStyle = bg;
      g.fillRect(0, 0, W, H);
      if (!page) return;

      const pr = page.getBoundingClientRect();
      const across = axis === "x" ? H : W; // ruler thickness
      const span = axis === "x" ? W : H; // ruler length
      const origin = axis === "x" ? pr.left - rect.left : pr.top - rect.top; // page 0 in ruler px
      const perUnit = zoom; // page px -> device px

      const step = NICE_STEPS.find((s) => s * perUnit >= 60) ?? 2000;
      const minor = step / (step % 5 === 0 ? 5 : 4);

      g.lineWidth = 1;
      g.strokeStyle = tick;
      g.fillStyle = labelCol;
      g.font = '9px "Inter", system-ui, sans-serif';

      const firstUnit = Math.max(0, Math.floor(-origin / perUnit / minor) * minor);
      const lastUnit = (span - origin) / perUnit;
      for (let u = firstUnit; u <= lastUnit + 1e-6; u += minor) {
        const p = Math.round(origin + u * perUnit) + 0.5;
        if (p < 0 || p > span) continue;
        const major = Math.abs(u % step) < 1e-6 || Math.abs((u % step) - step) < 1e-6;
        const len = major ? across * 0.62 : across * 0.32;
        g.beginPath();
        if (axis === "x") {
          g.moveTo(p, across);
          g.lineTo(p, across - len);
        } else {
          g.moveTo(across, p);
          g.lineTo(across - len, p);
        }
        g.stroke();

        if (major) {
          const label = String(Math.round(pxToUnit(u, unit)));
          if (axis === "x") {
            g.textBaseline = "top";
            g.fillText(label, p + 3, 2);
          } else {
            g.save();
            g.translate(3, p - 3);
            g.rotate(-Math.PI / 2);
            g.textBaseline = "top";
            g.fillText(label, 0, 0);
            g.restore();
          }
        }
      }

      if (selRange) {
        const a = origin + selRange.lo * perUnit;
        const b = origin + selRange.hi * perUnit;
        g.fillStyle = accent + "2e";
        if (axis === "x") g.fillRect(a, 0, b - a, across);
        else g.fillRect(0, a, across, b - a);
        g.strokeStyle = accent;
        g.beginPath();
        for (const q of [a, b]) {
          const r = Math.round(q) + 0.5;
          if (axis === "x") {
            g.moveTo(r, 0);
            g.lineTo(r, across);
          } else {
            g.moveTo(0, r);
            g.lineTo(across, r);
          }
        }
        g.stroke();
      }
    };

    const schedule = () => {
      if (!raf) raf = requestAnimationFrame(draw);
    };
    schedule();
    wrap.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", schedule);
    const ro = new ResizeObserver(schedule);
    ro.observe(wrap);
    ro.observe(cv);
    return () => {
      wrap.removeEventListener("scroll", schedule);
      window.removeEventListener("resize", schedule);
      ro.disconnect();
      if (raf) cancelAnimationFrame(raf);
    };
  }, [axis, wrapRef, pageRef, zoom, unit, selRange]);

  // ---- cursor marker ----
  useEffect(() => {
    const wrap = wrapRef.current;
    const cv = canvasRef.current;
    if (!wrap || !cv) return;
    const onMove = (e: MouseEvent) => {
      const rect = cv.getBoundingClientRect();
      setCursor(axis === "x" ? e.clientX - rect.left : e.clientY - rect.top);
    };
    const onLeave = () => setCursor(null);
    wrap.addEventListener("mousemove", onMove);
    wrap.addEventListener("mouseleave", onLeave);
    return () => {
      wrap.removeEventListener("mousemove", onMove);
      wrap.removeEventListener("mouseleave", onLeave);
    };
  }, [axis, wrapRef]);

  return (
    <div className={`ruler-slot ruler-slot-${axis}`}>
      <canvas ref={canvasRef} className="ruler-canvas" />
      {cursor != null && (
        <div className="ruler-cursor" style={axis === "x" ? { left: cursor } : { top: cursor }} />
      )}
    </div>
  );
}
