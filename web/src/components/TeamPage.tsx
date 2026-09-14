import { useEffect, useState } from "react";
import { AlertTriangle, Check, Copy, Loader2, Plus, Trash2 } from "lucide-react";
import { api } from "../api";
import { useAuth, type PendingInvite, type TeamMember, type TenantInfo } from "../auth";
import { ConfirmButton } from "./ConfirmButton";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Team/organization management — a page reached from the Start screen, not a dialog over the
 * design surface: who's on the team, their role, and inviting new people belongs with the rest
 * of "which report am I working on", not buried inside a report you happen to have open. */
export function TeamPage() {
  const currentUserId = useAuth((s) => s.user?.id);
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [pending, setPending] = useState<PendingInvite[]>([]);
  const [inviteRole, setInviteRole] = useState<"Designer" | "Viewer">("Viewer");
  const [newInvite, setNewInvite] = useState<PendingInvite | null>(null);
  const [copied, setCopied] = useState(false);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const refresh = async () => {
    const [t, m, p] = await Promise.all([api.getTenant(), api.listTeam(), api.listInvites()]);
    setTenant(t);
    setMembers(m);
    setPending(p);
  };

  useEffect(() => {
    refresh()
      .catch((e) => setErr(msg(e)))
      .finally(() => setLoaded(true));
  }, []);

  if (!loaded) {
    return (
      <section className="start-section">
        <div className="share-loading">
          <Loader2 size={16} className="spin" />
        </div>
      </section>
    );
  }

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
    <>
      <section className="start-section">
        <h3>Members{tenant ? ` — ${tenant.name}` : ""}</h3>
        <table className="drive-table">
          <thead>
            <tr>
              <th>Member</th>
              <th>Role</th>
            </tr>
          </thead>
          <tbody>
            {members.map((m) => (
              <tr key={m.id}>
                <td className="drive-table-name">
                  {m.email}
                  {m.id === currentUserId && <span className="hint"> (you)</span>}
                </td>
                <td>
                  <select
                    value={m.roles[0] ?? "Viewer"}
                    onChange={(e) => void changeRole(m.id, e.target.value)}
                    disabled={m.id === currentUserId}
                    title={m.id === currentUserId ? "You cannot change your own role" : undefined}
                  >
                    <option value="Designer">Designer</option>
                    <option value="Viewer">Viewer</option>
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="start-section start-section-narrow">
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
        <section className="start-section start-section-narrow">
          <h3>Pending invites</h3>
          {pending.map((p) => (
            <div className="settings-row" key={p.code}>
              <span>
                <code>{p.code}</code> · {p.role} · expires {new Date(p.expiresAtUtc).toLocaleDateString()}
              </span>
              <ConfirmButton icon={Trash2} title="Revoke invite" confirmLabel="Revoke" onConfirm={() => void revoke(p.code)} />
            </div>
          ))}
        </section>
      )}

      {err && (
        <section className="start-section start-section-narrow">
          <div className="error small">
            <AlertTriangle /> <span>{err}</span>
          </div>
        </section>
      )}
    </>
  );
}
