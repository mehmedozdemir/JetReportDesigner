import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, Plus, Trash2, X } from "lucide-react";
import { api } from "../api";
import type { ConnectionResponse } from "../types";

const PROVIDERS: { value: string; label: string }[] = [
  { value: "sqlServer", label: "SQL Server" },
  { value: "postgreSql", label: "PostgreSQL" },
  { value: "oracle", label: "Oracle" },
];
const providerLabel = (v: string) => PROVIDERS.find((p) => p.value === v)?.label ?? v;
const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

type Draft = { name: string; provider: string; connStr: string };
const BLANK: Draft = { name: "", provider: "sqlServer", connStr: "" };

export function ConnectionsDialog({
  connections,
  onChange,
  onClose,
}: {
  connections: ConnectionResponse[];
  onChange: (list: ConnectionResponse[]) => void;
  onClose: () => void;
}) {
  const [selId, setSelId] = useState<string | null>(connections[0]?.id ?? null);
  const [creating, setCreating] = useState(connections.length === 0);
  const [draft, setDraft] = useState<Draft>(BLANK);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const selected = useMemo(() => connections.find((c) => c.id === selId) ?? null, [connections, selId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    if (creating) setDraft(BLANK);
    else if (selected) setDraft({ name: selected.name, provider: selected.provider, connStr: "" });
  }, [creating, selected]);

  const refresh = async () => {
    const list = await api.listConnections();
    onChange(list);
    return list;
  };

  const save = async () => {
    setErr(null);
    setBusy(true);
    try {
      if (creating) {
        const c = await api.createConnection(draft.name.trim(), draft.provider, draft.connStr);
        await refresh();
        setCreating(false);
        setSelId(c.id);
      } else if (selected) {
        await api.updateConnection(
          selected.id,
          draft.name.trim(),
          draft.provider,
          draft.connStr.trim() ? draft.connStr : null,
        );
        await refresh();
      }
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!selected) return;
    setErr(null);
    try {
      await api.deleteConnection(selected.id);
      const list = await refresh();
      setSelId(list[0]?.id ?? null);
      setCreating(list.length === 0);
    } catch (e) {
      setErr(msg(e));
    }
  };

  const canSave = draft.name.trim().length > 0 && (!creating || draft.connStr.trim().length > 0);

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal md-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Connections"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>Connections</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="md-body">
          <div className="md-list">
            <ul>
              {connections.map((c) => (
                <li key={c.id}>
                  <button
                    className={!creating && selId === c.id ? "on" : ""}
                    onClick={() => {
                      setCreating(false);
                      setErr(null);
                      setSelId(c.id);
                    }}
                  >
                    <span className="md-list-name">{c.name}</span>
                    <span className="md-list-sub">{providerLabel(c.provider)}</span>
                  </button>
                </li>
              ))}
              {connections.length === 0 && <li className="md-empty">No connections yet</li>}
            </ul>
            <button
              className={`md-new${creating ? " on" : ""}`}
              onClick={() => {
                setCreating(true);
                setErr(null);
                setSelId(null);
              }}
            >
              <Plus /> New connection
            </button>
          </div>

          <div className="md-detail">
            <label className="field">
              <span>Name</span>
              <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
            </label>
            <label className="field">
              <span>Provider</span>
              <select value={draft.provider} onChange={(e) => setDraft({ ...draft, provider: e.target.value })}>
                {PROVIDERS.map((p) => (
                  <option key={p.value} value={p.value}>
                    {p.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Connection string</span>
              <textarea
                rows={3}
                spellCheck={false}
                value={draft.connStr}
                placeholder={
                  creating
                    ? "Server=…;Database=…;User Id=…;Password=…;TrustServerCertificate=True"
                    : "Leave blank to keep the stored one"
                }
                onChange={(e) => setDraft({ ...draft, connStr: e.target.value })}
              />
              <span className="hint" style={{ fontWeight: 400 }}>
                Self-signed SQL Server needs <code>TrustServerCertificate=True;Encrypt=False</code>.
              </span>
            </label>

            {err && (
              <div className="error small">
                <AlertTriangle /> <span>{err}</span>
              </div>
            )}

            <div className="row" style={{ marginTop: 8 }}>
              <button className="btn primary" onClick={save} disabled={!canSave || busy}>
                {creating ? "Create" : "Save"}
              </button>
              {!creating && selected && (
                <button className="mini danger" onClick={remove}>
                  <Trash2 /> Delete
                </button>
              )}
            </div>
          </div>
        </div>

        <footer>
          <span className="hint" style={{ marginRight: "auto" }}>
            Connection strings are stored encrypted and never shown again.
          </span>
          <button className="btn primary" onClick={onClose}>
            Done
          </button>
        </footer>
      </div>
    </div>
  );
}
