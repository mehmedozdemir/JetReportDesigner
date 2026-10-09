import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Copy, KeyRound, Loader2, Lock, Mail, MoreVertical, Search, Trash2, UserMinus, UserPlus, X } from "lucide-react";
import { api } from "../api";
import {
  displayNameOf,
  initials,
  useAuth,
  type CreatedInvite,
  type PendingInvite,
  type TeamMember,
  type TenantInfo,
} from "../auth";
import { timeAgo } from "../time";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";
import { ConfirmButton } from "./ConfirmButton";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { PageHeader } from "./PageHeader";
import { Avatar, copyText, notify, TextField } from "./ui";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** "Invite people" — email + role (+ how long the invite lasts). Shows the link to copy whether or not a mail went out. */
function InviteDialog({ emailConfigured, onClose, onCreated }: { emailConfigured: boolean; onClose: () => void; onCreated: () => void }) {
  const { t, i18n } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(onClose);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<"Viewer" | "Designer">("Viewer");
  const [hours, setHours] = useState(72);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<CreatedInvite | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) return setError(t("auth.err.emailInvalid"));
    setBusy(true);
    setError(null);
    try {
      const invite = await api.createInvite(role, hours, email.trim() || null);
      setCreated(invite);
      onCreated();
    } catch (err) {
      setError(msg(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div ref={dialogRef} className="modal ws-dialog" role="dialog" aria-modal="true" aria-labelledby="invite-title" onMouseDown={(e) => e.stopPropagation()}>
        <div className="ws-dialog-head">
          <h2 id="invite-title">{created ? t("team.inviteReady") : t("team.inviteTitle")}</h2>
          <button className="icon-btn" onClick={onClose} aria-label={t("common.close")}><X size={20} /></button>
        </div>

        {created ? (
          <div className="ws-dialog-body">
            <p className="ws-dialog-text">
              {created.emailSent
                ? t("team.inviteSent", { email: created.email })
                : created.email && emailConfigured
                  ? t("team.inviteNotSent", { email: created.email })
                  : created.email
                    ? t("team.inviteNoMail", { email: created.email })
                    : t("team.inviteShare")}
            </p>
            <div className="copy-field">
              <code>{created.link}</code>
              <button className="btn tonal" onClick={() => void copyText(created.link, t("team.linkCopied"))}>
                <Copy size={16} /> {t("team.copyLink")}
              </button>
            </div>
            <p className="hint">
              {t("team.inviteMeta", {
                role: t(`roles.${created.role}`),
                code: created.code,
                date: new Date(created.expiresAtUtc).toLocaleString(i18n.language),
              })}
            </p>
            <div className="ws-dialog-actions">
              <button className="btn text" onClick={() => setCreated(null)}>{t("team.inviteAnother")}</button>
              <button className="btn primary" onClick={onClose}>{t("common.done")}</button>
            </div>
          </div>
        ) : (
          <form className="ws-dialog-body" onSubmit={(e) => void submit(e)} noValidate>
            <TextField
              label={t("team.inviteEmail")}
              type="email"
              autoFocus
              value={email}
              onChange={(v) => { setEmail(v); setError(null); }}
              error={error}
              helper={emailConfigured ? t("team.inviteEmailHelper") : t("team.inviteNoMailHelper")}
            />
            <fieldset className="radio-cards">
              <legend>{t("team.role")}</legend>
              {(["Viewer", "Designer"] as const).map((r) => (
                <label key={r} className={role === r ? "on" : undefined}>
                  <input type="radio" name="role" checked={role === r} onChange={() => setRole(r)} />
                  <span>
                    <strong>{t(`roles.${r}`)}</strong>
                    <small>{t(`roles.${r}Hint`)}</small>
                  </span>
                </label>
              ))}
            </fieldset>
            <label className="select-field">
              <span>{t("team.inviteValid")}</span>
              <select value={hours} onChange={(e) => setHours(Number(e.target.value))}>
                <option value={24}>{t("team.validDays", { count: 1 })}</option>
                <option value={72}>{t("team.validDays", { count: 3 })}</option>
                <option value={168}>{t("team.validDays", { count: 7 })}</option>
                <option value={720}>{t("team.validDays", { count: 30 })}</option>
              </select>
            </label>
            <div className="ws-dialog-actions">
              <button type="button" className="btn text" onClick={onClose}>{t("common.cancel")}</button>
              <button type="submit" className="btn primary" disabled={busy}>
                {busy ? <Loader2 size={16} className="spin" /> : email.trim() && emailConfigured ? <Mail size={16} /> : <UserPlus size={16} />}
                {email.trim() && emailConfigured ? t("team.sendInvite") : t("team.createInvite")}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

/** Team/organization management — who is in the organization, their role, and inviting people. */
export function TeamPage() {
  const { t, i18n } = useTranslation();
  const currentUserId = useAuth((s) => s.user?.id);
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [pending, setPending] = useState<PendingInvite[]>([]);
  const [emailConfigured, setEmailConfigured] = useState(false);
  const [query, setQuery] = useState("");
  const [inviting, setInviting] = useState(false);
  const [menu, setMenu] = useState<{ x: number; y: number; items: MenuItem[] } | null>(null);
  const [removing, setRemoving] = useState<TeamMember | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const refresh = async () => {
    const [tn, m, p, caps] = await Promise.all([api.getTenant(), api.listTeam(), api.listInvites(), api.tenantCapabilities()]);
    setTenant(tn);
    setMembers(m);
    setPending(p);
    setEmailConfigured(caps.emailConfigured);
  };

  useEffect(() => {
    refresh()
      .catch((e) => setErr(msg(e)))
      .finally(() => setLoaded(true));
  }, []);

  const shown = useMemo(() => {
    const q = query.trim().toLocaleLowerCase(i18n.language);
    return members.filter((m) => !q || `${m.displayName ?? ""} ${m.email}`.toLocaleLowerCase(i18n.language).includes(q));
  }, [members, query, i18n.language]);

  const run = async (fn: () => Promise<unknown>, done?: string) => {
    setErr(null);
    try {
      await fn();
      await refresh();
      if (done) notify(done);
    } catch (e) {
      notify(msg(e), { tone: "error" });
    }
  };

  const resetLink = async (m: TeamMember) => {
    try {
      const link = await api.createResetLink(m.id);
      await copyText(link.url, t("team.resetLinkCopied", { name: displayNameOf(m) }));
    } catch (e) {
      notify(msg(e), { tone: "error" });
    }
  };

  const openMenu = (e: React.MouseEvent, m: TeamMember) => {
    const r = e.currentTarget.getBoundingClientRect();
    setMenu({
      x: r.right,
      y: r.bottom + 4,
      items: [
        { label: t("team.resetLink"), icon: KeyRound, onClick: () => void resetLink(m) },
        { sep: true },
        { label: t("team.removeMember", { email: m.email }), icon: UserMinus, danger: true, onClick: () => setRemoving(m) },
      ],
    });
  };

  if (!loaded) {
    return (
      <section className="start-section">
        <div className="share-loading"><Loader2 size={16} className="spin" /></div>
      </section>
    );
  }

  return (
    <section className="start-section">
      <PageHeader
        title={t("team.title")}
        description={tenant ? t("team.description", { org: tenant.name, count: members.length }) : undefined}
        actions={
          <button className="btn primary" onClick={() => setInviting(true)}>
            <UserPlus size={16} /> {t("team.invitePeople")}
          </button>
        }
      />

      {err && <div className="ws-banner ws-banner-error">{err}</div>}
      {!emailConfigured && (
        <div className="ws-banner">
          <Mail size={18} />
          <span>
            {t("team.noMailBanner")} <a href="/email-settings">{t("team.setUpMail")}</a>
          </span>
        </div>
      )}

      <div className="table-toolbar">
        <label className="search-pill">
          <Search size={18} />
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("team.search")} aria-label={t("team.search")} />
        </label>
      </div>

      <div className="data-card">
        <table className="ws-table">
          <thead>
            <tr>
              <th>{t("team.member")}</th>
              <th>{t("team.role")}</th>
              <th className="hide-sm">{t("team.lastSignIn")}</th>
              <th aria-label={t("common.moreActions")} />
            </tr>
          </thead>
          <tbody>
            {shown.map((m) => {
              const self = m.id === currentUserId;
              return (
                <tr key={m.id}>
                  <td>
                    <div className="person">
                      <Avatar text={initials(m)} seed={m.email} size={36} />
                      <div className="person-text">
                        <strong>
                          {displayNameOf(m)}
                          {self && <span className="you-chip">{t("team.you")}</span>}
                          {m.lockedOut && (
                            <span className="status-chip status-chip-warn" title={t("team.lockedHint")}>
                              <Lock size={12} /> {t("team.locked")}
                            </span>
                          )}
                        </strong>
                        {m.displayName && <span>{m.email}</span>}
                      </div>
                    </div>
                  </td>
                  <td>
                    {self ? (
                      <span className="role-chip" title={t("team.ownRoleLocked")}>{t(`roles.${m.roles[0] ?? "Viewer"}`)}</span>
                    ) : (
                      <select
                        className="ws-select"
                        value={m.roles[0] ?? "Viewer"}
                        onChange={(e) => void run(() => api.setUserRole(m.id, e.target.value), t("team.roleChanged", { name: displayNameOf(m) }))}
                        aria-label={t("team.roleFor", { name: displayNameOf(m) })}
                      >
                        <option value="Designer">{t("roles.Designer")}</option>
                        <option value="Viewer">{t("roles.Viewer")}</option>
                      </select>
                    )}
                  </td>
                  <td className="hide-sm muted">
                    {m.lastLoginAtUtc ? (
                      <span title={new Date(m.lastLoginAtUtc).toLocaleString(i18n.language)}>{timeAgo(m.lastLoginAtUtc, i18n.language)}</span>
                    ) : (
                      t("team.never")
                    )}
                  </td>
                  <td className="row-actions">
                    {!self && (
                      <button className="icon-btn" onClick={(e) => openMenu(e, m)} aria-label={t("team.actionsFor", { name: displayNameOf(m) })}>
                        <MoreVertical size={18} />
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
            {shown.length === 0 && (
              <tr>
                <td colSpan={4} className="empty-row">{t("team.noMatch")}</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {pending.length > 0 && (
        <>
          <h3 className="section-title">{t("team.pendingInvites")}</h3>
          <div className="data-card">
            <table className="ws-table">
              <thead>
                <tr>
                  <th>{t("team.invitee")}</th>
                  <th>{t("team.role")}</th>
                  <th className="hide-sm">{t("team.expiresCol")}</th>
                  <th aria-label={t("common.moreActions")} />
                </tr>
              </thead>
              <tbody>
                {pending.map((p) => (
                  <tr key={p.code}>
                    <td>
                      <div className="person">
                        <span className="invite-icon"><Mail size={18} /></span>
                        <div className="person-text">
                          <strong>{p.email ?? t("team.anyone")}</strong>
                          <span className="mono">{p.code}</span>
                        </div>
                      </div>
                    </td>
                    <td><span className="role-chip">{t(`roles.${p.role}`)}</span></td>
                    <td className="hide-sm muted">{new Date(p.expiresAtUtc).toLocaleDateString(i18n.language)}</td>
                    <td className="row-actions">
                      <button className="icon-btn" onClick={() => void copyText(p.link, t("team.linkCopied"))} title={t("team.copyLink")} aria-label={t("team.copyLink")}>
                        <Copy size={18} />
                      </button>
                      <ConfirmButton icon={Trash2} title={t("team.revokeInvite")} confirmLabel={t("team.revoke")} onConfirm={() => void run(() => api.revokeInvite(p.code), t("team.inviteRevoked"))} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {menu && <ContextMenu x={menu.x} y={menu.y} items={menu.items} onClose={() => setMenu(null)} />}
      {inviting && <InviteDialog emailConfigured={emailConfigured} onClose={() => setInviting(false)} onCreated={() => void refresh()} />}
      {removing && (
        <RemoveDialog
          member={removing}
          onCancel={() => setRemoving(null)}
          onConfirm={() => {
            const m = removing;
            setRemoving(null);
            void run(() => api.removeUser(m.id), t("team.removed", { name: displayNameOf(m) }));
          }}
        />
      )}
    </section>
  );
}

function RemoveDialog({ member, onCancel, onConfirm }: { member: TeamMember; onCancel: () => void; onConfirm: () => void }) {
  const { t } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(onCancel);
  return (
    <div className="modal-backdrop" onMouseDown={onCancel}>
      <div ref={dialogRef} className="modal ws-dialog ws-dialog-sm" role="alertdialog" aria-modal="true" aria-labelledby="remove-title" onMouseDown={(e) => e.stopPropagation()}>
        <div className="ws-dialog-body">
          <h2 id="remove-title" className="ws-dialog-title">{t("team.removeTitle", { name: displayNameOf(member) })}</h2>
          <p className="ws-dialog-text">{t("team.removeText")}</p>
          <div className="ws-dialog-actions">
            <button className="btn text" onClick={onCancel} autoFocus>{t("common.cancel")}</button>
            <button className="btn danger" onClick={onConfirm}>{t("common.remove")}</button>
          </div>
        </div>
      </div>
    </div>
  );
}
