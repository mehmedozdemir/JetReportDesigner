import { useEffect, useState } from "react";
import { AlertTriangle, Check, Copy, Plus, Trash2, Users } from "lucide-react";
import { api } from "../api";
import { useAuth, type PendingInvite, type TeamMember, type TenantInfo } from "../auth";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function TeamDialog({ onClose }: { onClose: () => void }) {
  const currentUserId = useAuth((s) => s.user?.id);
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [pending, setPending] = useState<PendingInvite[]>([]);
  const [inviteRole, setInviteRole] = useState<"Designer" | "Viewer">("Viewer");
  const [newInvite, setNewInvite] = useState<PendingInvite | null>(null);
  const [copied, setCopied] = useState(false);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const refresh = async () => {
    const [t, m, p] = await Promise.all([api.getTenant(), api.listTeam(), api.listInvites()]);
    setTenant(t);
    setMembers(m);
    setPending(p);
  };

  useEffect(() => {
    refresh().catch((e) => setErr(msg(e)));
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const changeRole = async (id: string, role: string) => {
    setErr(null);
    try {
      await api.setUserRole(id, role);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    }
  };

  const generateInvite = async () => {
    setErr(null);
    setBusy(true);
    setCopied(false);
    try {
      const invite = await api.createInvite(inviteRole);
      setNewInvite(invite);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusy(false);
    }
  };

  const revoke = async (code: string) => {
    setErr(null);
    try {
      await api.revokeInvite(code);
      if (newInvite?.code === code) setNewInvite(null);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    }
  };

  const copyCode = (code: string) => {
    void navigator.clipboard.writeText(code);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal settings-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Team"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <Users /> Team{tenant ? ` — ${tenant.name}` : ""}
          </h2>
        </header>

        <div className="settings-body">
          <section className="settings-section">
            <h3>Members</h3>
            {members.map((m) => (
              <div className="settings-row" key={m.id}>
                <span>
                  {m.email}
                  {m.id === currentUserId && <span className="hint"> (you)</span>}
                </span>
                <div className="settings-control">
                  <select
                    value={m.roles[0] ?? "Viewer"}
                    onChange={(e) => void changeRole(m.id, e.target.value)}
                    disabled={m.id === currentUserId}
                    title={m.id === currentUserId ? "You cannot change your own role" : undefined}
                  >
                    <option value="Designer">Designer</option>
                    <option value="Viewer">Viewer</option>
                  </select>
                </div>
              </div>
            ))}
          </section>

          <section className="settings-section">
            <h3>Invite a teammate</h3>
            <div className="settings-row">
              <span>Role</span>
              <div className="settings-control">
                <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value as "Designer" | "Viewer")}>
                  <option value="Viewer">Viewer</option>
                  <option value="Designer">Designer</option>
                </select>
                <button className="btn primary" onClick={() => void generateInvite()} disabled={busy}>
                  <Plus size={14} /> Generate code
                </button>
              </div>
            </div>
            {newInvite && (
              <div className="settings-row">
                <span>Share this code — expires {new Date(newInvite.expiresAtUtc).toLocaleString()}</span>
                <div className="settings-control">
                  <code style={{ fontSize: 14, letterSpacing: "0.08em" }}>{newInvite.code}</code>
                  <button className="mini" onClick={() => copyCode(newInvite.code)} title="Copy code" aria-label="Copy code">
                    {copied ? <Check size={13} /> : <Copy size={13} />}
                  </button>
                </div>
              </div>
            )}
          </section>

          {pending.length > 0 && (
            <section className="settings-section">
              <h3>Pending invites</h3>
              {pending.map((p) => (
                <div className="settings-row" key={p.code}>
                  <span>
                    <code>{p.code}</code> · {p.role} · expires {new Date(p.expiresAtUtc).toLocaleDateString()}
                  </span>
                  <button className="mini danger" onClick={() => void revoke(p.code)} title="Revoke invite" aria-label="Revoke invite">
                    <Trash2 size={13} />
                  </button>
                </div>
              ))}
            </section>
          )}

          {err && (
            <div className="error small">
              <AlertTriangle /> <span>{err}</span>
            </div>
          )}
        </div>

        <footer>
          <span style={{ marginLeft: "auto" }} />
          <button className="btn primary" onClick={onClose}>
            Done
          </button>
        </footer>
      </div>
    </div>
  );
}
