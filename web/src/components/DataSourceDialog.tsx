import { useEffect, useMemo, useState } from "react";
import type { LucideIcon } from "lucide-react";
import {
  AlertTriangle,
  Ban,
  Database,
  FileJson,
  Globe,
  ListTree,
  Play,
  Plug,
  Plus,
  Tag,
  Trash2,
  X,
} from "lucide-react";
import { api } from "../api";
import { useDesigner } from "../store";
import { ConnectionsDialog } from "./ConnectionsDialog";
import { SavedQueriesDialog } from "./SavedQueriesDialog";
import type {
  ConnectionRef,
  ConnectionResponse,
  DataField,
  DataSourceDefinition,
  DataSourceKind,
  SqlQueryResponse,
} from "../types";

export const KIND_META: Record<DataSourceKind, { Icon: LucideIcon; label: string }> = {
  json: { Icon: FileJson, label: "JSON" },
  rest: { Icon: Globe, label: "REST" },
  sql: { Icon: Database, label: "SQL" },
  none: { Icon: Ban, label: "None" },
};

const SAMPLE_JSON = JSON.stringify(
  [
    { customer: "Acme Ltd", total: 1250.5, orderDate: "2026-03-09" },
    { customer: "Globex", total: 90, orderDate: "2026-03-10" },
  ],
  null,
  2,
);

type KV = { key: string; value: string };
const toKV = (o: Record<string, string> = {}): KV[] => Object.entries(o).map(([key, value]) => ({ key, value }));
const fromKV = (rows: KV[]): Record<string, string> =>
  Object.fromEntries(rows.filter((r) => r.key.trim()).map((r) => [r.key, r.value]));

export function DataSourceDialog({ onClose }: { onClose: () => void }) {
  const report = useDesigner((s) => s.report);
  const setDataSource = useDesigner((s) => s.setDataSource);
  const existing = report?.dataSources[0];

  const [kind, setKind] = useState<DataSourceKind>(existing?.kind ?? "json");
  const [name, setName] = useState(existing?.name ?? "orders");
  const [json, setJson] = useState(existing?.json?.inlineData ?? "");
  const [jsonPath, setJsonPath] = useState(existing?.json?.resultPath ?? "$");
  const [url, setUrl] = useState(existing?.rest?.url ?? "");
  const [headers, setHeaders] = useState<KV[]>(toKV(existing?.rest?.headers));
  const [query, setQuery] = useState<KV[]>(toKV(existing?.rest?.query));
  const [restPath, setRestPath] = useState(existing?.rest?.resultPath ?? "$");
  const [connectionName, setConnectionName] = useState(existing?.sql?.connection ?? "");
  const [command, setCommand] = useState(existing?.sql?.commandText ?? "");
  const [sqlParams, setSqlParams] = useState<KV[]>(
    (existing?.sql?.parameters ?? []).map((p) => ({ key: p.name, value: p.value })),
  );
  const [connections, setConnections] = useState<ConnectionResponse[]>([]);
  const [savedQueries, setSavedQueries] = useState<SqlQueryResponse[]>([]);
  const [savedQueryId, setSavedQueryId] = useState("");
  const [showConns, setShowConns] = useState(false);
  const [showQueries, setShowQueries] = useState(false);
  const [fields, setFields] = useState<DataField[]>(existing?.fields ?? []);
  const [preview, setPreview] = useState<Record<string, unknown>[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedConn = useMemo(
    () => connections.find((c) => c.name === connectionName) ?? null,
    [connections, connectionName],
  );

  useEffect(() => {
    void api.listConnections().then(setConnections).catch(() => undefined);
  }, []);

  useEffect(() => {
    if (!selectedConn) {
      setSavedQueries([]);
      return;
    }
    void api.listSqlQueries(selectedConn.id).then(setSavedQueries).catch(() => setSavedQueries([]));
  }, [selectedConn]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const build = (): { source: DataSourceDefinition; connRef: ConnectionRef | null } => {
    const source: DataSourceDefinition = { name: name.trim() || "data", kind, fields, json: null, rest: null, sql: null };
    if (kind === "json") {
      source.json = { inlineData: json, resultPath: jsonPath.trim() || "$" };
    } else if (kind === "rest") {
      source.rest = { url: url.trim(), method: "GET", headers: fromKV(headers), query: fromKV(query), resultPath: restPath.trim() || "$" };
    } else if (kind === "sql") {
      source.sql = {
        connection: connectionName,
        commandText: command,
        parameters: sqlParams.filter((p) => p.key.trim()).map((p) => ({ name: p.key, value: p.value })),
        timeoutSeconds: 30,
        maxRows: 50000,
      };
    }
    const conn = connections.find((c) => c.name === connectionName);
    const connRef: ConnectionRef | null =
      kind === "sql" && conn ? { name: conn.name, connectionId: conn.id, provider: conn.provider } : null;
    return { source, connRef };
  };

  const runPreview = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await api.previewSource(build().source, 15);
      setFields(result.fields);
      setPreview(result.rows);
      const { source, connRef } = build();
      setDataSource({ ...source, fields: result.fields }, connRef);
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  };

  const done = () => {
    if (kind === "none") {
      setDataSource(null);
    } else {
      const { source, connRef } = build();
      setDataSource(source, connRef);
    }
    onClose();
  };

  return (
    <>
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal ds-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Data source"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <Database /> Data source
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="ds-body">
          <ul className="ds-kinds">
            {(Object.keys(KIND_META) as DataSourceKind[]).map((k) => {
              const M = KIND_META[k];
              return (
                <li key={k}>
                  <button className={k === kind ? "on" : ""} onClick={() => setKind(k)}>
                    <M.Icon size={14} /> {M.label}
                  </button>
                </li>
              );
            })}
          </ul>

          <div className="ds-form">
            {kind === "none" ? (
              <p className="hint">
                This report has no data source. Bindings can still use <code>{"{param:name}"}</code>,{" "}
                <code>pageNumber()</code> and <code>now()</code>.
              </p>
            ) : (
              <label className="field">
                <span>Source name</span>
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </label>
            )}

            {kind === "json" && (
              <>
                <label className="field">
                  <span>JSON</span>
                  <textarea
                    rows={12}
                    spellCheck={false}
                    placeholder={SAMPLE_JSON}
                    value={json}
                    onChange={(e) => setJson(e.target.value)}
                  />
                </label>
                <div className="grid2">
                  <label className="field">
                    <span>Result path</span>
                    <input value={jsonPath} onChange={(e) => setJsonPath(e.target.value)} placeholder="$ or $.data.items" />
                  </label>
                  <div className="field">
                    <span>&nbsp;</span>
                    <button className="mini" onClick={() => setJson(SAMPLE_JSON)}>
                      Use sample data
                    </button>
                  </div>
                </div>
              </>
            )}

            {kind === "rest" && (
              <>
                <label className="field">
                  <span>URL (GET)</span>
                  <input
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                    placeholder="https://api.example.com/{param:tenant}/orders"
                  />
                </label>
                <KVEditor label="Query" rows={query} onChange={setQuery} placeholder="from · {param:from}" />
                <KVEditor label="Headers" rows={headers} onChange={setHeaders} placeholder="Authorization · Bearer …" />
                <label className="field">
                  <span>Result path</span>
                  <input value={restPath} onChange={(e) => setRestPath(e.target.value)} placeholder="$.data.items" />
                </label>
              </>
            )}

            {kind === "sql" && (
              <>
                <label className="field">
                  <span>Connection</span>
                  <div className="row">
                    <select
                      style={{ flex: 1 }}
                      value={connectionName}
                      onChange={(e) => {
                        setConnectionName(e.target.value);
                        setSavedQueryId("");
                      }}
                    >
                      <option value="">— pick a connection —</option>
                      {connections.map((c) => (
                        <option key={c.id} value={c.name}>
                          {c.name} ({providerLabel(c.provider)})
                        </option>
                      ))}
                    </select>
                    <button
                      className="mini"
                      title="Manage connections"
                      aria-label="Manage connections"
                      onClick={() => setShowConns(true)}
                    >
                      <Plug />
                    </button>
                  </div>
                </label>

                {selectedConn && (
                  <label className="field">
                    <span>Saved query</span>
                    <div className="row">
                      <select
                        style={{ flex: 1 }}
                        value={savedQueryId}
                        onChange={(e) => {
                          setSavedQueryId(e.target.value);
                          const q = savedQueries.find((x) => x.id === e.target.value);
                          if (q) setCommand(q.commandText);
                        }}
                      >
                        <option value="">— new / unsaved —</option>
                        {savedQueries.map((q) => (
                          <option key={q.id} value={q.id}>
                            {q.name}
                          </option>
                        ))}
                      </select>
                      <button
                        className="mini"
                        title="Manage saved queries"
                        aria-label="Manage saved queries"
                        onClick={() => setShowQueries(true)}
                      >
                        <ListTree />
                      </button>
                    </div>
                  </label>
                )}

                <label className="field">
                  <span>SELECT query</span>
                  <textarea
                    rows={8}
                    spellCheck={false}
                    value={command}
                    onChange={(e) => {
                      setCommand(e.target.value);
                      setSavedQueryId("");
                    }}
                    placeholder="SELECT id, customer, total FROM orders WHERE order_date >= :from"
                  />
                </label>
                <KVEditor label="Parameters" rows={sqlParams} onChange={setSqlParams} placeholder="from · {param:from}" />
              </>
            )}

            {kind !== "none" && (
              <button
                className="btn primary"
                onClick={runPreview}
                disabled={busy}
                style={{ width: "100%", marginTop: 4 }}
              >
                <Play size={14} /> Test &amp; preview
              </button>
            )}
            {error && (
              <div className="error small">
                <AlertTriangle /> <span>{error}</span>
              </div>
            )}

            {fields.length > 0 && (
              <>
                <h3>Fields</h3>
                <div className="ds-fieldgrid">
                  {fields.map((f) => (
                    <span key={f.name} className="ds-field">
                      <Tag size={12} /> {f.name} <em>{f.type}</em>
                    </span>
                  ))}
                </div>
              </>
            )}

            {preview.length > 0 && (
              <div className="data-preview">
                <table>
                  <thead>
                    <tr>{fields.map((f) => <th key={f.name}>{f.name}</th>)}</tr>
                  </thead>
                  <tbody>
                    {preview.slice(0, 8).map((row, i) => (
                      <tr key={i}>
                        {fields.map((f) => (
                          <td key={f.name}>{String((row as Record<string, unknown>)[f.name] ?? "")}</td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        <footer>
          <span className="hint" style={{ marginRight: "auto" }}>
            Preview runs against the server.
          </span>
          <button className="btn" onClick={onClose}>
            Cancel
          </button>
          <button className="btn primary" onClick={done}>
            Done
          </button>
        </footer>
      </div>
    </div>

    {showConns && (
      <ConnectionsDialog
        connections={connections}
        onChange={setConnections}
        onClose={() => setShowConns(false)}
      />
    )}
    {showQueries && selectedConn && (
      <SavedQueriesDialog
        connectionId={selectedConn.id}
        connectionName={selectedConn.name}
        seedText={command}
        onPick={(t) => {
          setCommand(t);
          setSavedQueryId("");
        }}
        onClose={() => {
          setShowQueries(false);
          void api.listSqlQueries(selectedConn.id).then(setSavedQueries).catch(() => undefined);
        }}
      />
    )}
    </>
  );
}

function KVEditor({
  label,
  rows,
  onChange,
  placeholder,
}: {
  label: string;
  rows: KV[];
  onChange: (rows: KV[]) => void;
  placeholder?: string;
}) {
  const set = (i: number, patch: Partial<KV>) => onChange(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  return (
    <div className="field">
      <span>{label}</span>
      {rows.map((r, i) => (
        <div key={i} className="row" style={{ marginBottom: 3 }}>
          <input value={r.key} placeholder="key" onChange={(e) => set(i, { key: e.target.value })} />
          <input value={r.value} placeholder="value" onChange={(e) => set(i, { value: e.target.value })} />
          <button className="mini danger" onClick={() => onChange(rows.filter((_, j) => j !== i))} aria-label="Remove row">
            <Trash2 />
          </button>
        </div>
      ))}
      <button className="mini" onClick={() => onChange([...rows, { key: "", value: "" }])} title={placeholder}>
        <Plus /> Row
      </button>
    </div>
  );
}

const PROVIDERS: { value: string; label: string }[] = [
  { value: "sqlServer", label: "SQL Server" },
  { value: "postgreSql", label: "PostgreSQL" },
  { value: "oracle", label: "Oracle" },
];
const providerLabel = (v: string) => PROVIDERS.find((p) => p.value === v)?.label ?? v;

