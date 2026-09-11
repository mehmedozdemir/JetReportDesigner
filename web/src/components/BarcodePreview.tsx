import type { BarcodeSpec } from "../types";

// A fixed, representative 21x21 QR-ish module pattern (three corner finder squares
// plus a scattered fill) — not a real encode, just something that reads as "a QR code"
// at a glance, the same spirit as the table element's two fake preview rows.
const QR_SIZE = 21;
function qrDark(x: number, y: number): boolean {
  const inFinder = (fx: number, fy: number) =>
    (x >= fx && x < fx + 7 && y >= fy && y < fy + 7) &&
    (x === fx || x === fx + 6 || y === fy || y === fy + 6 || (x >= fx + 2 && x <= fx + 4 && y >= fy + 2 && y <= fy + 4));
  if (inFinder(0, 0) || inFinder(QR_SIZE - 7, 0) || inFinder(0, QR_SIZE - 7)) return true;
  if ((x < 8 && y < 8) || (x >= QR_SIZE - 8 && y < 8) || (x < 8 && y >= QR_SIZE - 8)) return false;
  return ((x * 7 + y * 13 + x * y) % 5) < 2;
}

// A fixed run-length pattern for 1D symbologies (bar widths in arbitrary units).
const BAR_RUNS = [2, 1, 1, 2, 1, 3, 1, 1, 2, 2, 1, 1, 3, 1, 2, 1, 1, 2, 1, 1, 3, 2, 1, 1, 2, 1, 1, 1, 2];

export function BarcodePreview({ spec, width, height }: { spec: BarcodeSpec; width: number; height: number }) {
  const w = Math.max(20, width);
  const h = Math.max(16, height);
  const is2D = spec.symbology === "qr" || spec.symbology === "dataMatrix";
  const fore = spec.foreColor || "#000000";
  const back = spec.backColor || "#ffffff";

  if (is2D) {
    const size = spec.symbology === "dataMatrix" ? 16 : QR_SIZE;
    const module = Math.min(w, h) / size;
    const ox = (w - module * size) / 2;
    const oy = (h - module * size) / 2;
    const cells: React.ReactNode[] = [];
    for (let y = 0; y < size; y++) {
      for (let x = 0; x < size; x++) {
        if (qrDark(x % QR_SIZE, y % QR_SIZE)) {
          cells.push(<rect key={`${x}-${y}`} x={ox + x * module} y={oy + y * module} width={module} height={module} fill={fore} />);
        }
      }
    }
    return (
      <svg viewBox={`0 0 ${w} ${h}`} width="100%" height="100%" style={{ display: "block" }}>
        <rect x={0} y={0} width={w} height={h} fill={back} />
        {cells}
      </svg>
    );
  }

  const textH = spec.showText ? 11 : 0;
  const barH = Math.max(1, h - textH);
  const totalUnits = BAR_RUNS.reduce((a, b) => a + b, 0);
  const unit = w / totalUnits;
  let x = 0;
  const bars: React.ReactNode[] = [];
  BAR_RUNS.forEach((run, i) => {
    if (i % 2 === 0) {
      bars.push(<rect key={i} x={x} y={0} width={run * unit} height={barH} fill={fore} />);
    }
    x += run * unit;
  });

  return (
    <svg viewBox={`0 0 ${w} ${h}`} width="100%" height="100%" style={{ display: "block" }}>
      <rect x={0} y={0} width={w} height={h} fill={back} />
      {bars}
      {spec.showText && (
        <text x={w / 2} y={h - 2} fontSize={8} fontFamily="monospace" fill={fore} textAnchor="middle">
          {spec.value || "123456789012"}
        </text>
      )}
    </svg>
  );
}
