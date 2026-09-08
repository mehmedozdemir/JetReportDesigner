import { useState } from "react";
import { Database, Pencil, Plus, Tag } from "lucide-react";
import { useDesigner } from "../store";
import { DataSourceDialog, KIND_META } from "./DataSourceDialog";

export function DataPanel() {
  const report = useDesigner((s) => s.report);
  const [open, setOpen] = useState(false);

  if (!report) return null;

  const source = report.dataSources[0];
  const configured = !!source && source.kind !== "none";
  const fields = source?.fields ?? [];
  const Meta = configured ? KIND_META[source!.kind] : null;

  return (
    <div className="panel">
      <h2>
        <Database /> Data source
      </h2>

      {configured ? (
        <button className="ds-card" onClick={() => setOpen(true)} title="Edit data source">
          <span className="ds-card-main">
            {Meta && <Meta.Icon size={15} />}
            <span className="ds-card-name">{source!.name}</span>
            <span className="ds-kind-tag">{source!.kind.toUpperCase()}</span>
          </span>
          <span className="ds-card-foot">
            <span className={`ds-card-status ${fields.length ? "ok" : "warn"}`}>
              {fields.length
                ? `● ${fields.length} field${fields.length === 1 ? "" : "s"}`
                : "No fields — open to preview"}
            </span>
            <span className="ds-card-edit">
              <Pencil size={13} /> Edit
            </span>
          </span>
        </button>
      ) : (
        <div className="ds-empty">
          <p className="hint">
            No data source yet. Add JSON, a REST URL or a SQL query — or design with parameters only.
          </p>
          <button className="btn primary" onClick={() => setOpen(true)} style={{ width: "100%" }}>
            <Plus size={14} /> Add data source
          </button>
        </div>
      )}

      {fields.length > 0 && (
        <div className="field-tree">
          <h3>{source!.name} · fields</h3>
          {fields.map((f) => (
            <div
              key={f.name}
              className="field-chip"
              draggable
              onDragStart={(e) => e.dataTransfer.setData("application/x-field", `{${source!.name}.${f.name}}`)}
              title={`Drag onto the page — binds {${source!.name}.${f.name}}`}
            >
              <span className="fname">
                <Tag /> {f.name}
              </span>
              <em>{f.type}</em>
            </div>
          ))}
        </div>
      )}

      {open && <DataSourceDialog onClose={() => setOpen(false)} />}
    </div>
  );
}
