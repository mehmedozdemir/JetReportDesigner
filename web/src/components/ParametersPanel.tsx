import { useDesigner } from "../store";
import type { ParameterType } from "../types";

const TYPES: ParameterType[] = ["string", "number", "boolean", "date", "dateTime"];
const NO_PARAMS: never[] = [];

export function ParametersPanel() {
  const params = useDesigner((s) => s.report?.parameters) ?? NO_PARAMS;
  const addParameter = useDesigner((s) => s.addParameter);
  const patchParameter = useDesigner((s) => s.patchParameter);
  const removeParameter = useDesigner((s) => s.removeParameter);
  const hasReport = useDesigner((s) => !!s.report);

  if (!hasReport) return null;

  return (
    <div className="panel">
      <h2>Parameters</h2>
      {params.length === 0 && <p className="hint">Used as {"{param:name}"} in bindings, REST and SQL.</p>}
      {params.map((p, i) => (
        <div key={i} className="param-row">
          <div className="row">
            <input
              value={p.name}
              placeholder="name"
              onChange={(e) => patchParameter(i, (x) => (x.name = e.target.value))}
            />
            <select value={p.type} onChange={(e) => patchParameter(i, (x) => (x.type = e.target.value as ParameterType))}>
              {TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
            <button className="mini" onClick={() => removeParameter(i)}>×</button>
          </div>
          <div className="row">
            <input
              value={p.label ?? ""}
              placeholder="label"
              onChange={(e) => patchParameter(i, (x) => (x.label = e.target.value))}
            />
            <input
              value={p.defaultValue == null ? "" : String(p.defaultValue)}
              placeholder="default"
              onChange={(e) => patchParameter(i, (x) => (x.defaultValue = e.target.value || null))}
            />
          </div>
          <label className="row" style={{ fontSize: 11, color: "var(--muted)" }}>
            <input
              type="checkbox"
              checked={p.required}
              onChange={(e) => patchParameter(i, (x) => (x.required = e.target.checked))}
            />
            required
          </label>
        </div>
      ))}
      <button className="mini" onClick={addParameter}>+ parameter</button>
    </div>
  );
}
