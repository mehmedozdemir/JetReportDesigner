import type { SparklineSpec } from "../types";

// Two distinct fixed series so the two fake preview rows don't look identical — same spirit
// as MatrixPreview's fixture data.
const SAMPLE_SERIES = [
  [3, 6, 4, 8, 5, 9, 7],
  [8, 5, 7, 3, 6, 2, 9],
];

const W = 52;
const H = 16;
const PAD = 2;

/** Design-time preview of a sparkline column: the actual shape a reader will see, drawn from
 *  fixture data — the same approach the table element already uses for its other two fake rows,
 *  since the canvas never runs the real data source. */
export function SparklinePreview({ spec, seed }: { spec: SparklineSpec; seed: number }) {
  const values = SAMPLE_SERIES[seed % SAMPLE_SERIES.length];
  const min = Math.min(...values);
  const max = Math.max(...values);
  const innerW = W - 2 * PAD;
  const innerH = H - 2 * PAD;
  const toY = (v: number) => H - PAD - ((v - min) / (max - min || 1)) * innerH;
  const n = values.length;

  if (spec.type === "bar") {
    const slot = innerW / n;
    const barW = Math.max(1, slot * 0.65);
    return (
      <svg width={W} height={H} viewBox={`0 0 ${W} ${H}`} aria-hidden="true">
        {values.map((v, i) => {
          const y = toY(v);
          const last = i === n - 1;
          return (
            <rect
              key={i}
              x={PAD + i * slot + (slot - barW) / 2}
              y={y}
              width={barW}
              height={Math.max(0.5, H - PAD - y)}
              fill={last && spec.highlightColor ? spec.highlightColor : spec.color}
            />
          );
        })}
      </svg>
    );
  }

  const pts = values.map((v, i) => [PAD + (i * innerW) / (n - 1), toY(v)] as const);
  const line = pts.map(([x, y]) => `${x},${y}`).join(" ");
  const floorY = Math.max(...pts.map(([, y]) => y));
  const area = `${pts[0][0]},${floorY} ${line} ${pts[pts.length - 1][0]},${floorY}`;

  return (
    <svg width={W} height={H} viewBox={`0 0 ${W} ${H}`} aria-hidden="true">
      {spec.showArea && <polygon points={area} fill={spec.color} opacity={0.22} />}
      <polyline points={line} fill="none" stroke={spec.color} strokeWidth={1.25} />
      {spec.highlightColor ? (
        <circle cx={pts[n - 1][0]} cy={pts[n - 1][1]} r={2} fill={spec.highlightColor} />
      ) : (
        <circle cx={pts[n - 1][0]} cy={pts[n - 1][1]} r={1.4} fill={spec.color} />
      )}
    </svg>
  );
}
