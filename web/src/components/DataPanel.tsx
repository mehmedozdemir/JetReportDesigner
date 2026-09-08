import { useState } from "react";
import { api } from "../api";
import { useDesigner } from "../store";
import type { DataSourceDefinition } from "../types";

const SAMPLE = JSON.stringify(
  [
    { customer: "Acme Ltd", total: 1250.5, orderDate: "2026-03-09" },
    { customer: "Globex", total: 90, orderDate: "2026-03-10" },
  ],
  null,
  2,
);

export function DataPanel() {
  const report = useDesigner((s) => s.report);
  const mutate = useDesigner((s) => s.mutate);
  const source: DataSourceDefinition | undefined = report?.dataSources[0];

  const [name, setName] = useState(source?.name ?? "orders");
  const [json, setJson] = useState(source?.json?.inlineData ?? "");
  const [path, setPath] = useState(source?.json?.resultPath ?? "$");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!report) return null;

  const apply = async () => {
    setBusy(true);
    setError(null);
    try {
      const draft: DataSourceDefinition = {
        name: name.trim() || "orders",
        kind: "json",
        json: { inlineData: json, resultPath: path.trim() || "$" },
        fields: [],
      };
      const { fields } = await api.schema(draft);
      mutate((r) => {
        r.dataSources = [{ ...draft, fields }];
      });
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="panel">
      <h2>Data</h2>
      <label className="field">
        <span>Source name</span>
        <input value={name} onChange={(e) => setName(e.target.value)} />
      </label>
      <label className="field">
        <span>JSON</span>
        <textarea
          rows={8}
          spellCheck={false}
          placeholder={SAMPLE}
          value={json}
          onChange={(e) => setJson(e.target.value)}
        />
      </label>
      <label className="field">
        <span>Result path</span>
        <input value={path} onChange={(e) => setPath(e.target.value)} placeholder="$ or $.data.items" />
      </label>
      <div className="row">
        <button onClick={() => setJson(SAMPLE)} disabled={busy}>Sample</button>
        <button className="primary" onClick={apply} disabled={busy}>Load fields</button>
      </div>
      {error && <div className="error small">{error}</div>}

      {source?.fields?.length ? (
        <div className="field-tree">
          <h3>{source.name}</h3>
          {source.fields.map((f) => (
            <div
              key={f.name}
              className="field-chip"
              draggable
              onDragStart={(e) =>
                e.dataTransfer.setData("application/x-field", `{${source.name}.${f.name}}`)
              }
              title={`Drag onto the page — binds {${source.name}.${f.name}}`}
            >
              <span>{f.name}</span>
              <em>{f.type}</em>
            </div>
          ))}
        </div>
      ) : (
        <p className="hint">Paste JSON and load fields, then drag a field onto the page.</p>
      )}
    </div>
  );
}
