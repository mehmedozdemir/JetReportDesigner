import type { ColorScale } from "./types";

/** The scale's default when you switch one on: white → blue, the usual "more is darker". */
export const DEFAULT_COLOR_SCALE: ColorScale = { lowColor: "#ffffff", highColor: "#2563eb" };

const parse = (hex: string | null | undefined): [number, number, number] => {
  let h = (hex ?? "").replace("#", "");
  if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
  const n = /^[0-9a-f]{6}$/i.test(h) ? parseInt(h, 16) : 0xffffff;
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
};

const mix = (from: string, to: string, t: number) => {
  const a = parse(from);
  const b = parse(to);
  const c = a.map((v, i) => Math.round(v + (b[i] - v) * t));
  return `#${c.map((v) => v.toString(16).padStart(2, "0")).join("")}`;
};

/** Mirrors ColorScalePainter.Fill on the server so the canvas shows what will actually render. */
export function scaleFill(scale: ColorScale, value: number, min: number, max: number): string {
  // Every value the same: nothing is "high" or "low", so sit at the middle of the scale rather
  // than painting the whole thing as max.
  const t = max - min <= Number.EPSILON ? 0.5 : Math.min(1, Math.max(0, (value - min) / (max - min)));
  if (!scale.midColor) return mix(scale.lowColor, scale.highColor, t);
  return t <= 0.5 ? mix(scale.lowColor, scale.midColor, t * 2) : mix(scale.midColor, scale.highColor, (t - 0.5) * 2);
}

/** Mirrors ColorScalePainter.TextOn: black or white, whichever stays readable on `background`. */
export function textOn(background: string): string {
  const channel = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  const [r, g, b] = parse(background);
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b) > 0.179 ? "#000000" : "#ffffff";
}
