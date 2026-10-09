import { useEffect, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Loader2 } from "lucide-react";
import { api } from "../api";
import { accountRequest, AuthError, displayNameOf, initials, isDesigner, useAuth, type TenantInfo } from "../auth";
import { PageHeader } from "./PageHeader";
import { Avatar, notify, PasswordField, PasswordRules, passwordChecks, TextField } from "./ui";

/** The signed-in person's own account: name, password, and (for Designers) the organization's name. */
export function AccountPage() {
  const { t, i18n } = useTranslation();
  const user = useAuth((s) => s.user);
  const token = useAuth((s) => s.token);
  const setUser = useAuth((s) => s.setUser);
  const designer = isDesigner(user);

  const [name, setName] = useState(user?.displayName ?? "");
  const [savingName, setSavingName] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);

  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [pwErrors, setPwErrors] = useState<Record<string, string | null>>({});
  const [savingPw, setSavingPw] = useState(false);

  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [orgName, setOrgName] = useState("");
  const [orgError, setOrgError] = useState<string | null>(null);
  const [savingOrg, setSavingOrg] = useState(false);

  useEffect(() => {
    api.getTenant().then((tn) => {
      setTenant(tn);
      setOrgName(tn.name);
    }).catch(() => undefined);
  }, []);

  if (!user) return null;

  const saveName = async (e: FormEvent) => {
    e.preventDefault();
    setSavingName(true);
    setNameError(null);
    try {
      setUser(await api.updateMe(name.trim()));
      notify(t("account.nameSaved"));
    } catch (err) {
      setNameError((err as Error).message);
    } finally {
      setSavingName(false);
    }
  };

  const savePassword = async (e: FormEvent) => {
    e.preventDefault();
    if (!current) return setPwErrors({ current: t("auth.err.passwordRequired") });
    if (!passwordChecks(next).every((c) => c.ok)) return setPwErrors({ next: t("auth.err.passwordRules") });
    if (next !== confirm) return setPwErrors({ confirm: t("auth.err.passwordMismatch") });
    setSavingPw(true);
    setPwErrors({});
    try {
      await accountRequest("/api/auth/me/password", "POST", { currentPassword: current, newPassword: next }, token);
      setCurrent("");
      setNext("");
      setConfirm("");
      notify(t("account.passwordChanged"));
    } catch (err) {
      const error = err as AuthError;
      setPwErrors({
        current: error.field("currentPassword") ?? null,
        next: error.field("password") ?? (error.field("currentPassword") ? null : error.message),
      });
    } finally {
      setSavingPw(false);
    }
  };

  const saveOrg = async (e: FormEvent) => {
    e.preventDefault();
    if (!orgName.trim()) return setOrgError(t("account.orgRequired"));
    setSavingOrg(true);
    setOrgError(null);
    try {
      setTenant(await api.renameTenant(orgName.trim()));
      notify(t("account.orgSaved"));
    } catch (err) {
      setOrgError((err as Error).message);
    } finally {
      setSavingOrg(false);
    }
  };

  return (
    <>
      <PageHeader narrow title={t("account.title")} description={t("account.description")} />

      <section className="start-section start-section-narrow account-hero">
        <Avatar text={initials(user)} seed={user.email} size={72} />
        <div>
          <h2>{displayNameOf(user)}</h2>
          <p>{user.email}</p>
          <span className="role-chip">{designer ? t("nav.designer") : t("nav.viewer")}</span>
          {tenant && <span className="hint" style={{ marginLeft: 8 }}>{tenant.name}</span>}
          {user.lastLoginAtUtc && (
            <p className="hint" style={{ marginTop: 6 }}>
              {t("account.lastLogin", { date: new Date(user.lastLoginAtUtc).toLocaleString(i18n.language) })}
            </p>
          )}
        </div>
      </section>

      <section className="start-section start-section-narrow">
        <h3>{t("account.profile")}</h3>
        <form onSubmit={(e) => void saveName(e)} className="card-form" noValidate>
          <TextField label={t("auth.fullName")} autoComplete="name" value={name} onChange={(v) => { setName(v); setNameError(null); }} error={nameError} maxLength={100} />
          <TextField label={t("auth.email")} value={user.email} onChange={() => undefined} readOnly helper={t("account.emailFixed")} />
          <div className="card-actions">
            <button className="btn primary" type="submit" disabled={savingName || name.trim() === (user.displayName ?? "")}>
              {savingName && <Loader2 size={16} className="spin" />} {t("common.save")}
            </button>
          </div>
        </form>
      </section>

      <section className="start-section start-section-narrow">
        <h3>{t("account.password")}</h3>
        <form onSubmit={(e) => void savePassword(e)} className="card-form" noValidate>
          <input type="email" name="username" autoComplete="username" value={user.email} readOnly hidden />
          <PasswordField label={t("account.currentPassword")} autoComplete="current-password" value={current} onChange={(v) => { setCurrent(v); setPwErrors({}); }} error={pwErrors.current} />
          <PasswordField label={t("auth.newPassword")} autoComplete="new-password" value={next} onChange={(v) => { setNext(v); setPwErrors({}); }} error={pwErrors.next} />
          {next && <PasswordRules password={next} />}
          <PasswordField label={t("auth.confirmPassword")} autoComplete="new-password" value={confirm} onChange={(v) => { setConfirm(v); setPwErrors({}); }} error={pwErrors.confirm} />
          <div className="card-actions">
            <button className="btn primary" type="submit" disabled={savingPw || !current || !next}>
              {savingPw && <Loader2 size={16} className="spin" />} {t("account.changePassword")}
            </button>
          </div>
        </form>
      </section>

      {designer && tenant && (
        <section className="start-section start-section-narrow">
          <h3>{t("account.organization")}</h3>
          <form onSubmit={(e) => void saveOrg(e)} className="card-form" noValidate>
            <TextField label={t("auth.organization")} value={orgName} onChange={(v) => { setOrgName(v); setOrgError(null); }} error={orgError} maxLength={200} />
            <div className="card-actions">
              <button className="btn primary" type="submit" disabled={savingOrg || orgName.trim() === tenant.name}>
                {savingOrg && <Loader2 size={16} className="spin" />} {t("common.save")}
              </button>
            </div>
          </form>
        </section>
      )}
    </>
  );
}
