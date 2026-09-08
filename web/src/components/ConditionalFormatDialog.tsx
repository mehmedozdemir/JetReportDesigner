import { useEffect } from "react";
import { ArrowDown, ArrowUp, Ban, Plus, Trash2, X } from "lucide-react";
import type { ComparisonOp, FormatRule, ReportStyle } from "../types";

const OPS: { value: ComparisonOp; label: string; needsValue: boolean }[] = [
  { value: "eq", label: "= equals", needsValue: true },
  { value: "ne", label: "≠ not equal", needsValue: true },
  { value: "gt", label: "> greater than", needsValue: true },
  { value: "ge", label: "≥ greater or equal", needsValue: true },
  { value: "lt", label: "< less than", needsValue: true },
  { value: "le", label: "≤ less or equal", needsValue: true },
  { value: "contains", label: "contains", needsValue: true },
  { value: "startsWith", label: "starts with", needsValue: true },
  { value: "endsWith", label: "ends with", needsValue: true },
  { value: "isEmpty", label: "is empty", needsValue: false },
  { value: "isNotEmpty", label: "is not empty", needsValue: false },
];
const needsValue = (op: ComparisonOp) => OPS.find((o) => o.value === op)?.needsValue ?? true;

const emptyRule = (field: string): FormatRule => ({
  field,
  op: "eq",
  value: "",
  style: {},
});

function withFont(style: ReportStyle, key: "bold" | "italic", on: boolean): ReportStyle {
  const font: NonNullable<ReportStyle["font"]> = { ...(style.font ?? {}) };
  if (on) font[key] = true;
  else delete font[key];
  const { font: _drop, ...rest } = style;
  return Object.keys(font).length > 0 ? { ...rest, font } : rest;
}

export function ConditionalFormatDialog({
  rules,
  fields,
  allowHidden,
  title = "Conditional formatting",
  onChange,
  onClose,
}: {
  rules: FormatRule[];
  fields: string[];
  allowHidden: boolean;
  title?: string;
  onChange: (rules: FormatRule[]) => void;
  onClose: () => void;
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const patch = (i: number, next: Partial<FormatRule>) =>
    onChange(rules.map((r, j) => (j === i ? { ...r, ...next } : r)));
  const patchStyle = (i: number, next: Partial<ReportStyle>) =>
    patch(i, { style: { ...rules[i].style, ...next } });
  const move = (i: number, dir: -1 | 1) => {
    const j = i + dir;
    if (j < 0 || j >= rules.length) return;
    const copy = [...rules];
    [copy[i], copy[j]] = [copy[j], copy[i]];
    onChange(copy);
  };
  const listId = "cf-fields";

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal cf-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>{title}</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="cf-body">
          {rules.length === 0 && (
            <p className="hint">
              No rules yet. Each rule tests a row field; every matching rule&apos;s formatting is applied, in order.
            </p>
          )}

          <datalist id={listId}>
            {fields.map((f) => (
              <option key={f} value={f} />
            ))}
          </datalist>

          {rules.map((rule, i) => {
            const style = rule.style ?? {};
            return (
              <div key={i} className="cf-rule">
                <div className="cf-cond">
                  <span className="cf-when">When</span>
                  <input
                    list={listId}
                    placeholder="field"
                    value={rule.field}
                    onChange={(e) => patch(i, { field: e.target.value })}
                  />
                  <select value={rule.op} onChange={(e) => patch(i, { op: e.target.value as ComparisonOp })}>
                    {OPS.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                  {needsValue(rule.op) && (
                    <input
                      placeholder="value"
                      value={rule.value}
                      onChange={(e) => patch(i, { value: e.target.value })}
                    />
                  )}
                  <span style={{ marginLeft: "auto" }} />
                  <button className="mini" title="Move up" aria-label="Move up" onClick={() => move(i, -1)} disabled={i === 0}>
                    <ArrowUp />
                  </button>
                  <button
                    className="mini"
                    title="Move down"
                    aria-label="Move down"
                    onClick={() => move(i, 1)}
                    disabled={i === rules.length - 1}
                  >
                    <ArrowDown />
                  </button>
                  <button
                    className="mini danger"
                    title="Delete rule"
                    aria-label="Delete rule"
                    onClick={() => onChange(rules.filter((_, j) => j !== i))}
                  >
                    <Trash2 />
                  </button>
                </div>

                <div className="cf-format">
                  <span className="cf-then">Then</span>

                  <label className="cf-swatch" title="Background">
                    <span>Fill</span>
                    <span className="row">
                      <input
                        type="color"
                        value={style.background ?? "#fde68a"}
                        onChange={(e) => patchStyle(i, { background: e.target.value })}
                      />
                      <button
                        type="button"
                        className={`mini ${style.background ? "" : "on"}`}
                        title="No fill"
                        aria-label="No fill"
                        onClick={() => patchStyle(i, { background: null })}
                      >
                        <Ban />
                      </button>
                    </span>
                  </label>

                  <label className="cf-swatch" title="Text colour">
                    <span>Text</span>
                    <span className="row">
                      <input
                        type="color"
                        value={style.color ?? "#b91c1c"}
                        onChange={(e) => patchStyle(i, { color: e.target.value })}
                      />
                      <button
                        type="button"
                        className={`mini ${style.color ? "" : "on"}`}
                        title="Inherit colour"
                        aria-label="Inherit colour"
                        onClick={() => patchStyle(i, { color: null })}
                      >
                        <Ban />
                      </button>
                    </span>
                  </label>

                  <button
                    type="button"
                    className={`mini ${style.font?.bold ? "on" : ""}`}
                    onClick={() => patch(i, { style: withFont(style, "bold", !style.font?.bold) })}
                  >
                    B
                  </button>
                  <button
                    type="button"
                    className={`mini ${style.font?.italic ? "on" : ""}`}
                    onClick={() => patch(i, { style: withFont(style, "italic", !style.font?.italic) })}
                  >
                    I
                  </button>

                  {allowHidden && (
                    <label className="cf-hide">
                      <input
                        type="checkbox"
                        checked={!!rule.hidden}
                        onChange={(e) => patch(i, { hidden: e.target.checked })}
                      />
                      Hide
                    </label>
                  )}

                  <span
                    className="cf-preview"
                    style={{
                      background: style.background ?? "transparent",
                      color: style.color ?? "var(--text-primary)",
                      fontWeight: style.font?.bold ? 700 : 400,
                      fontStyle: style.font?.italic ? "italic" : "normal",
                    }}
                  >
                    Sample
                  </span>
                </div>
              </div>
            );
          })}

          <button className="mini" onClick={() => onChange([...rules, emptyRule(fields[0] ?? "")])}>
            <Plus /> Add rule
          </button>
        </div>

        <footer>
          <span className="hint" style={{ marginRight: "auto" }}>
            Rules evaluate against the current row.
          </span>
          <button className="btn primary" onClick={onClose}>
            Done
          </button>
        </footer>
      </div>
    </div>
  );
}
