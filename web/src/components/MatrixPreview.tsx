import type { MatrixSpec } from "../types";

// Fixed representative sample — same spirit as the table element's two fake rows.
const COLS = ["Q1", "Q2", "Q3"];
const ROWS: { key: string; values: number[] }[] = [
  { key: "North", values: [12, 18, 9] },
  { key: "South", values: [7, 14, 11] },
];

export function MatrixPreview({ spec }: { spec: MatrixSpec }) {
  const colTotals = COLS.map((_, c) => ROWS.reduce((sum, r) => sum + r.values[c], 0));
  const grandTotal = colTotals.reduce((a, b) => a + b, 0);

  return (
    <table className="tbl-preview">
      <thead>
        <tr>
          <th>{spec.rowHeader || "Row"}</th>
          {COLS.map((c) => <th key={c} style={{ textAlign: "right" }}>{c}</th>)}
          {spec.showRowTotals && <th style={{ textAlign: "right" }}>Total</th>}
        </tr>
      </thead>
      <tbody>
        {ROWS.map((r) => (
          <tr key={r.key}>
            <td>{r.key}</td>
            {r.values.map((v, i) => <td key={i} style={{ textAlign: "right" }}>{v}</td>)}
            {spec.showRowTotals && (
              <td style={{ textAlign: "right", fontWeight: 600 }}>{r.values.reduce((a, b) => a + b, 0)}</td>
            )}
          </tr>
        ))}
        {spec.showColumnTotals && (
          <tr>
            <td style={{ fontWeight: 600 }}>Total</td>
            {colTotals.map((v, i) => <td key={i} style={{ textAlign: "right", fontWeight: 600 }}>{v}</td>)}
            {spec.showRowTotals && <td style={{ textAlign: "right", fontWeight: 600 }}>{grandTotal}</td>}
          </tr>
        )}
      </tbody>
    </table>
  );
}
