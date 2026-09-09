import type { ChartSpec } from "../types";

const PALETTE = [
  "#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#7c3aed",
  "#0891b2", "#db2777", "#65a30d", "#ea580c", "#4f46e5",
];

const CATS = ["A", "B", "C", "D", "E"];
const PATTERNS = [
  [6, 9, 4, 7, 5],
  [3, 5, 8, 4, 6],
  [7, 3, 6, 8, 4],
];

/** Static, representative preview of a chart element for the designer canvas. */
export function ChartPreview({ spec, width, height }: { spec: ChartSpec; width: number; height: number }) {
  const w = Math.max(40, width);
  const h = Math.max(30, height);
  const kind = spec.type;
  const isPie = kind === "pie";
  const isBar = kind === "bar";

  const seriesDefs = spec.series.length > 0 ? spec.series : [{ name: "Series 1", value: "", color: null }];
  const series = seriesDefs.map((s, i) => ({
    name: s.name || `Series ${i + 1}`,
    color: s.color || PALETTE[i % PALETTE.length],
    values: PATTERNS[i % PATTERNS.length],
  }));

  const pad = 6;
  const titleH = spec.title ? 16 : 0;
  const legendItems = isPie
    ? CATS.map((c, i) => ({ name: c, color: PALETTE[i % PALETTE.length] }))
    : series.map((s) => ({ name: s.name, color: s.color }));
  const legendW = spec.showLegend ? 74 : 0;
  const leftGutter = isPie ? pad : isBar ? 40 : spec.showGrid ? 30 : 8;
  const bottomGutter = isPie ? pad : 14;

  const px = pad + leftGutter;
  const py = pad + titleH;
  const pw = Math.max(10, w - 2 * pad - leftGutter - legendW);
  const ph = Math.max(10, h - 2 * pad - titleH - bottomGutter);
  const pr = px + pw;
  const pb = py + ph;

  const max = 10;
  const yOf = (v: number) => pb - (v / max) * ph;
  const xOf = (v: number) => px + (v / max) * pw;
  const n = CATS.length;

  const shapes: React.ReactNode[] = [];

  if (isPie) {
    const vals = PATTERNS[0];
    const total = vals.reduce((a, b) => a + b, 0);
    const cx = px + pw / 2;
    const cy = py + ph / 2;
    const r = (Math.min(pw, ph) / 2) * 0.92;
    let a = -Math.PI / 2;
    vals.forEach((v, i) => {
      const sweep = (v / total) * Math.PI * 2;
      const a1 = a + sweep;
      const large = sweep > Math.PI ? 1 : 0;
      const d = `M ${cx} ${cy} L ${cx + r * Math.cos(a)} ${cy + r * Math.sin(a)} A ${r} ${r} 0 ${large} 1 ${cx + r * Math.cos(a1)} ${cy + r * Math.sin(a1)} Z`;
      shapes.push(<path key={`w${i}`} d={d} fill={PALETTE[i % PALETTE.length]} stroke="#fff" strokeWidth={1} />);
      a = a1;
    });
  } else {
    if (spec.showGrid) {
      for (let t = 0; t <= 4; t++) {
        if (isBar) {
          const gx = px + (pw * t) / 4;
          shapes.push(<line key={`g${t}`} x1={gx} y1={py} x2={gx} y2={pb} stroke="#e5e7eb" />);
        } else {
          const gy = pb - (ph * t) / 4;
          shapes.push(<line key={`g${t}`} x1={px} y1={gy} x2={pr} y2={gy} stroke="#e5e7eb" />);
        }
      }
    }
    shapes.push(<line key="ax" x1={px} y1={py} x2={px} y2={pb} stroke="#9ca3af" />);
    shapes.push(<line key="ay" x1={px} y1={pb} x2={pr} y2={pb} stroke="#9ca3af" />);

    if (kind === "line" || kind === "area") {
      series.forEach((s, si) => {
        const pts = s.values.map((v, i) => [px + ((i + 0.5) * pw) / n, yOf(v)] as const);
        if (kind === "area") {
          const poly = [`${pts[0][0]},${pb}`, ...pts.map((p) => `${p[0]},${p[1]}`), `${pts[pts.length - 1][0]},${pb}`].join(" ");
          shapes.push(<polygon key={`a${si}`} points={poly} fill={s.color} opacity={0.35} />);
        }
        shapes.push(
          <polyline
            key={`l${si}`}
            points={pts.map((p) => `${p[0]},${p[1]}`).join(" ")}
            fill="none"
            stroke={s.color}
            strokeWidth={1.75}
          />,
        );
      });
    } else if (isBar) {
      const groupH = ph / n;
      const barH = (groupH * 0.8) / series.length;
      CATS.forEach((_, i) => {
        series.forEach((s, si) => {
          const vx = xOf(s.values[i]);
          shapes.push(
            <rect
              key={`b${i}-${si}`}
              x={px}
              y={py + i * groupH + groupH * 0.1 + si * barH}
              width={Math.max(0, vx - px)}
              height={barH * 0.9}
              fill={s.color}
            />,
          );
        });
      });
    } else {
      const groupW = pw / n;
      const barW = (groupW * 0.8) / series.length;
      CATS.forEach((_, i) => {
        series.forEach((s, si) => {
          const vy = yOf(s.values[i]);
          shapes.push(
            <rect
              key={`c${i}-${si}`}
              x={px + i * groupW + groupW * 0.1 + si * barW}
              y={vy}
              width={barW * 0.9}
              height={pb - vy}
              fill={s.color}
            />,
          );
        });
      });
    }

    CATS.forEach((c, i) => {
      shapes.push(
        <text
          key={`t${i}`}
          x={isBar ? px - 4 : px + ((i + 0.5) * pw) / n}
          y={isBar ? py + (i + 0.5) * (ph / n) : pb + 10}
          fontSize={7}
          fill="#6b7280"
          textAnchor={isBar ? "end" : "middle"}
          dominantBaseline={isBar ? "middle" : "auto"}
        >
          {c}
        </text>,
      );
    });
  }

  return (
    <svg viewBox={`0 0 ${w} ${h}`} width="100%" height="100%" style={{ display: "block" }}>
      {spec.title && (
        <text x={w / 2} y={pad + 10} fontSize={9} fontWeight={700} fill="#111827" textAnchor="middle">
          {spec.title}
        </text>
      )}
      {shapes}
      {legendW > 0 &&
        legendItems.slice(0, Math.max(1, Math.floor(ph / 13))).map((it, i) => (
          <g key={`lg${i}`} transform={`translate(${pr + 8}, ${py + i * 13})`}>
            <rect width={8} height={8} y={1} fill={it.color} />
            <text x={12} y={8} fontSize={7} fill="#6b7280">{it.name}</text>
          </g>
        ))}
    </svg>
  );
}
