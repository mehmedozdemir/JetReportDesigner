import { useEffect, useState } from "react";
import { AlertTriangle, Plus, Trash2, X } from "lucide-react";
import { api } from "../api";
import type { SqlQueryResponse } from "../types";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function SavedQueriesDialog({
  connectionId,
  connectionName,
  seedText,
  onPick,
  onClose,
}: {
  connectionId: string;
  connectionName: string;
  seedText: string;
  onPick: (commandText: string) => void;
  onClose: () => void;
}) {
  const [list, setList] = useState<SqlQueryResponse[]>([]);
  const [selId, setSelId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const startNew = () => {
    setCreating(true);
    setSelId(null);
    setName("");
    setText(seedText || "");
    setErr(null);
  };

  const load = async () => {
    try {
      const l = await api.listSqlQueries(connectionId);
      setList(l);
      return l;
    } catch (e) {
      setErr(msg(e));
      return [];
    }
  };

  useEffect(() => {
    void load().then((l) => {
      if (l[0]) {
        setSelId(l[0].id);
        setName(l[0].name);
        setText(l[0].commandText);
        setCreating(false);
      } else {
        startNew();
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connectionId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const selected = list.find((q) => q.id === selId) ?? null;

  const pick = (q: SqlQueryResponse) => {
    setCreating(false);
    setSelId(q.id);
    setName(q.name);
    setText(q.commandText);
    setErr(null);
  };

  const save = async () => {
    setErr(null);
    setBusy(true);
    try {
      if (creating) {
        const q = await api.createSqlQuery(connectionId, name.trim(), text);
        await load();
        setCreating(false);
        setSelId(q.id);
      } else if (selected) {
        const q = await api.updateSqlQuery(selected.id, name.trim(), text);
        await load();
        setSelId(q.id);
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
      await api.deleteSqlQuery(selected.id);
      const l = await load();
      if (l[0]) pick(l[0]);
      else startNew();
    } catch (e) {
      setErr(msg(e));
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal md-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Saved queries"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>Saved queries · {connectionName}</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="md-body">
          <div className="md-list">
            <ul>
              {list.map((q) => (
                <li key={q.id}>
                  <button className={!creating && selId === q.id ? "on" : ""} onClick={() => pick(q)}>
                    <span className="md-list-name">{q.name}</span>
                  </button>
                </li>
              ))}
              {list.length === 0 && <li className="md-empty">No saved queries</li>}
            </ul>
            <button className={`md-new${creating ? " on" : ""}`} onClick={startNew}>
              <Plus /> New query
            </button>
          </div>

          <div className="md-detail">
            <label className="field">
              <span>Name</span>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Recent orders" />
            </label>
            <label className="field">
              <span>SQL (SELECT only)</span>
              <textarea
                rows={9}
                spellCheck={false}
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="SELECT id, customer, total FROM orders WHERE order_date >= :from"
              />
            </label>

            {err && (
              <div className="error small">
                <AlertTriangle /> <span>{err}</span>
              </div>
            )}

            <div className="row" style={{ marginTop: 8 }}>
              <button className="btn" onClick={save} disabled={!name.trim() || !text.trim() || busy}>
                {creating ? "Save new" : "Save"}
              </button>
              {!creating && selected && (
                <button className="mini danger" onClick={remove}>
                  <Trash2 /> Delete
                </button>
              )}
              <span style={{ marginLeft: "auto" }} />
              <button className="btn primary" onClick={() => { onPick(text); onClose(); }} disabled={!text.trim()}>
                Use this query
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
