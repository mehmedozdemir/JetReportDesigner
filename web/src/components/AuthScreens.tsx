import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { AlertCircle, Building2, CheckCircle2, ChevronDown, FileBarChart2, Info, Loader2, Ticket } from "lucide-react";
import {
  accountRequest,
  AuthError,
  displayNameOf,
  initials,
  useAuth,
  type InvitePreview,
} from "../auth";
import { LANGUAGES, type LanguageCode } from "../i18n";
import { usePrefs } from "../prefs";
import { useFocusTrap } from "../useFocusTrap";
import { Avatar, PasswordField, PasswordRules, passwordChecks, TextField } from "./ui";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Paths that belong to the signed-out screens; anything else shows sign-in and keeps the URL, so
 * after signing in the person lands where the link pointed. */
export const AUTH_PATHS = ["/signin", "/signup", "/forgot-password", "/reset-password"];
export const isAuthPath = (path: string) => AUTH_PATHS.includes(path) || path.startsWith("/join/");

// ------------------------------------------------------------------------------------------------
// Layout
// ------------------------------------------------------------------------------------------------

function AuthLayout({ title, subtitle, aside, children }: { title: string; subtitle?: ReactNode; aside?: ReactNode; children: ReactNode }) {
  const { t } = useTranslation();
  const language = usePrefs((s) => s.language);
  const set = usePrefs((s) => s.set);
  return (
    <div className="auth">
      <main className="auth-card">
        <div className="auth-head">
          <div className="auth-logo">
            <FileBarChart2 size={30} />
          </div>
          <h1>{title}</h1>
          {subtitle && <div className="auth-subtitle">{subtitle}</div>}
          {aside}
        </div>
        <div className="auth-body">{children}</div>
      </main>
      <footer className="auth-footer">
        <select
          value={language}
          onChange={(e) => set("language", e.target.value as LanguageCode)}
          aria-label={t("settings.language")}
        >
          {LANGUAGES.map((l) => (
            <option key={l.code} value={l.code}>{l.label}</option>
          ))}
        </select>
        <span>JetReportDesigner</span>
      </footer>
    </div>
  );
}

function Actions({ left, children }: { left?: ReactNode; children: ReactNode }) {
  return (
    <div className="auth-actions">
      <div>{left}</div>
      <div>{children}</div>
    </div>
  );
}

function Primary({ busy, children, disabled }: { busy?: boolean; children: ReactNode; disabled?: boolean }) {
  return (
    <button type="submit" className="btn primary" disabled={busy || disabled}>
      {busy && <Loader2 size={16} className="spin" />}
      {children}
    </button>
  );
}

function Banner({ tone = "error", children }: { tone?: "error" | "info" | "success"; children: ReactNode }) {
  const Icon = tone === "success" ? CheckCircle2 : tone === "error" ? AlertCircle : Info;
  return (
    <div className={`auth-banner auth-banner-${tone}`} role={tone === "error" ? "alert" : "status"}>
      <Icon size={18} /> <div>{children}</div>
    </div>
  );
}

/** The "this is the account you are signing in to" pill on the password step. */
function AccountChip({ email, onChange }: { email: string; onChange: () => void }) {
  return (
    <button type="button" className="auth-chip" onClick={onChange}>
      <Avatar text={initials({ email })} seed={email} size={22} />
      <span>{email}</span>
      <ChevronDown size={16} />
    </button>
  );
}

// ------------------------------------------------------------------------------------------------
// Sign in — email first, then password (identifier-first, like Google)
// ------------------------------------------------------------------------------------------------

function SignIn() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const login = useAuth((s) => s.login);
  const prefill = new URLSearchParams(location.search).get("email") ?? "";

  const [step, setStep] = useState<"email" | "password">(prefill ? "password" : "email");
  const [email, setEmail] = useState(prefill);
  const [password, setPassword] = useState("");
  const [emailError, setEmailError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const notice = (location.state as { notice?: string } | null)?.notice;

  const next = (e: FormEvent) => {
    e.preventDefault();
    const value = email.trim();
    if (!value) return setEmailError(t("auth.err.emailRequired"));
    if (!EMAIL.test(value)) return setEmailError(t("auth.err.emailInvalid"));
    setEmail(value);
    setEmailError(null);
    setStep("password");
  };

  const signIn = async (e: FormEvent) => {
    e.preventDefault();
    if (!password) return setPasswordError(t("auth.err.passwordRequired"));
    setBusy(true);
    setPasswordError(null);
    setBanner(null);
    try {
      await login(email, password);
      if (isAuthPath(location.pathname)) navigate("/reports", { replace: true });
    } catch (err) {
      const error = err as AuthError;
      if (error.status === 401) setPasswordError(t("auth.err.wrongPassword"));
      else setBanner(error.message);
    } finally {
      setBusy(false);
    }
  };

  if (step === "email") {
    return (
      <AuthLayout title={t("auth.signIn.title")} subtitle={t("auth.signIn.subtitle")}>
        <form onSubmit={next} noValidate>
          {notice && <Banner tone="success">{notice}</Banner>}
          <TextField
            label={t("auth.email")}
            type="email"
            autoComplete="username"
            inputMode="email"
            autoFocus
            value={email}
            onChange={(v) => {
              setEmail(v);
              setEmailError(null);
            }}
            error={emailError}
          />
          <Actions left={<button type="button" className="btn text" onClick={() => navigate("/signup")}>{t("auth.createAccount")}</button>}>
            <Primary>{t("auth.next")}</Primary>
          </Actions>
        </form>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title={t("auth.signIn.welcome")}
      aside={<AccountChip email={email} onChange={() => { setStep("email"); setPassword(""); setPasswordError(null); setBanner(null); }} />}
    >
      <form onSubmit={(e) => void signIn(e)} noValidate>
        {/* Lets password managers pair the password with this account. */}
        <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
        {banner && <Banner>{banner}</Banner>}
        <PasswordField
          label={t("auth.password")}
          autoComplete="current-password"
          autoFocus
          value={password}
          onChange={(v) => {
            setPassword(v);
            setPasswordError(null);
          }}
          error={passwordError}
        />
        <Actions
          left={
            <button type="button" className="btn text" onClick={() => navigate(`/forgot-password?email=${encodeURIComponent(email)}`)}>
              {t("auth.forgot")}
            </button>
          }
        >
          <Primary busy={busy}>{t("auth.signIn.submit")}</Primary>
        </Actions>
      </form>
    </AuthLayout>
  );
}

// ------------------------------------------------------------------------------------------------
// Create account — name & email → password → organization (new, or the invite's)
// ------------------------------------------------------------------------------------------------

function SignUp({ invite, inviteCode }: { invite?: InvitePreview | null; inviteCode?: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const register = useAuth((s) => s.register);

  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [name, setName] = useState("");
  const [email, setEmail] = useState(invite?.email ?? "");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [mode, setMode] = useState<"org" | "invite">(inviteCode ? "invite" : "org");
  const [organization, setOrganization] = useState("");
  const [code, setCode] = useState(inviteCode ?? "");
  const [errors, setErrors] = useState<Record<string, string | null>>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const fail = (field: string, message: string) => setErrors((e) => ({ ...e, [field]: message }));
  const clear = (field: string) => setErrors((e) => ({ ...e, [field]: null }));

  const step1 = (e: FormEvent) => {
    e.preventDefault();
    let ok = true;
    if (!name.trim()) (fail("name", t("auth.err.nameRequired")), (ok = false));
    if (!EMAIL.test(email.trim())) (fail("email", email.trim() ? t("auth.err.emailInvalid") : t("auth.err.emailRequired")), (ok = false));
    if (ok) setStep(2);
  };

  const step2 = (e: FormEvent) => {
    e.preventDefault();
    if (!passwordChecks(password).every((c) => c.ok)) return fail("password", t("auth.err.passwordRules"));
    if (password !== confirm) return fail("confirm", t("auth.err.passwordMismatch"));
    setStep(3);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (mode === "org" && !organization.trim()) return fail("organization", t("auth.err.orgRequired"));
    if (mode === "invite" && !code.trim()) return fail("code", t("auth.err.codeRequired"));
    setBusy(true);
    setBanner(null);
    try {
      await register({
        email: email.trim(),
        password,
        displayName: name.trim(),
        ...(mode === "org" ? { organizationName: organization.trim() } : { inviteCode: code.trim() }),
      });
      navigate("/reports", { replace: true });
    } catch (err) {
      const error = err as AuthError;
      if (error.field("email")) {
        fail("email", error.field("email")!);
        setStep(1);
      } else if (error.field("password")) {
        fail("password", error.field("password")!);
        setStep(2);
      } else if (error.field("inviteCode")) {
        fail("code", error.field("inviteCode")!);
      } else {
        setBanner(error.message);
      }
    } finally {
      setBusy(false);
    }
  };

  const signInLink = (
    <button type="button" className="btn text" onClick={() => navigate("/signin")}>{t("auth.signInInstead")}</button>
  );
  const back = (to: 1 | 2) => (
    <button type="button" className="btn text" onClick={() => setStep(to)}>{t("auth.back")}</button>
  );
  const inviteCard = invite && (
    <div className="auth-invite">
      <Building2 size={18} />
      <div>
        <strong>{invite.organizationName}</strong>
        <span>{t("auth.join.as", { role: t(`roles.${invite.role}`) })}</span>
      </div>
    </div>
  );

  return (
    <AuthLayout
      title={invite ? t("auth.join.title", { org: invite.organizationName }) : t("auth.signUp.title")}
      subtitle={invite && step === 3 ? t("auth.join.confirm") : t(`auth.signUp.step${step}`)}
      aside={
        <>
          {inviteCard}
          <ol className="auth-steps" aria-label={t("auth.signUp.progress")}>
            {[1, 2, 3].map((n) => (
              <li key={n} className={n === step ? "on" : n < step ? "done" : undefined} aria-current={n === step ? "step" : undefined} />
            ))}
          </ol>
        </>
      }
    >
      {step === 1 && (
        <form onSubmit={step1} noValidate>
          <TextField label={t("auth.fullName")} autoComplete="name" autoFocus value={name} onChange={(v) => { setName(v); clear("name"); }} error={errors.name} maxLength={100} />
          <TextField
            label={t("auth.email")}
            type="email"
            autoComplete="email"
            inputMode="email"
            value={email}
            onChange={(v) => { setEmail(v); clear("email"); }}
            error={errors.email}
            helper={t("auth.signUp.emailHelper")}
          />
          <Actions left={signInLink}>
            <Primary>{t("auth.next")}</Primary>
          </Actions>
        </form>
      )}

      {step === 2 && (
        <form onSubmit={step2} noValidate>
          <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
          <PasswordField label={t("auth.password")} autoComplete="new-password" autoFocus value={password} onChange={(v) => { setPassword(v); clear("password"); }} error={errors.password} />
          <PasswordRules password={password} />
          <PasswordField label={t("auth.confirmPassword")} autoComplete="new-password" value={confirm} onChange={(v) => { setConfirm(v); clear("confirm"); }} error={errors.confirm} />
          <Actions left={back(1)}>
            <Primary>{t("auth.next")}</Primary>
          </Actions>
        </form>
      )}

      {step === 3 && (
        <form onSubmit={(e) => void submit(e)} noValidate>
          {banner && <Banner>{banner}</Banner>}
          {!invite && (
            <div className="auth-choice" role="radiogroup" aria-label={t("auth.signUp.step3")}>
              <button type="button" role="radio" aria-checked={mode === "org"} className={mode === "org" ? "on" : undefined} onClick={() => setMode("org")}>
                <Building2 size={20} />
                <span>
                  <strong>{t("auth.signUp.newOrg")}</strong>
                  <small>{t("auth.signUp.newOrgHint")}</small>
                </span>
              </button>
              <button type="button" role="radio" aria-checked={mode === "invite"} className={mode === "invite" ? "on" : undefined} onClick={() => setMode("invite")}>
                <Ticket size={20} />
                <span>
                  <strong>{t("auth.signUp.joinOrg")}</strong>
                  <small>{t("auth.signUp.joinOrgHint")}</small>
                </span>
              </button>
            </div>
          )}
          {mode === "org" ? (
            <TextField label={t("auth.organization")} autoComplete="organization" autoFocus value={organization} onChange={(v) => { setOrganization(v); clear("organization"); }} error={errors.organization} maxLength={200} />
          ) : (
            !invite && (
              <TextField
                label={t("auth.inviteCode")}
                autoFocus
                value={code}
                onChange={(v) => { setCode(v.toUpperCase()); clear("code"); }}
                error={errors.code}
                helper={t("auth.signUp.codeHelper")}
                autoCapitalize="characters"
                spellCheck={false}
              />
            )
          )}
          {invite && !errors.code && (
            <Banner tone="info">{t("auth.join.summary", { name: name.trim(), org: invite.organizationName, role: t(`roles.${invite.role}`) })}</Banner>
          )}
          {errors.code && invite && <Banner>{errors.code}</Banner>}
          <Actions left={back(2)}>
            <Primary busy={busy}>{t("auth.signUp.submit")}</Primary>
          </Actions>
        </form>
      )}
    </AuthLayout>
  );
}

/** /join/:code — reads the invite first, so the page can say which organization it leads to. */
function Join() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { code = "" } = useParams();
  const [invite, setInvite] = useState<InvitePreview | null | "invalid">(null);

  useEffect(() => {
    accountRequest<InvitePreview>(`/api/auth/invites/${encodeURIComponent(code)}`, "GET")
      .then(setInvite)
      .catch(() => setInvite("invalid"));
  }, [code]);

  if (invite === null) {
    return (
      <AuthLayout title={t("auth.join.loading")}>
        <div className="auth-loading"><Loader2 size={20} className="spin" /></div>
      </AuthLayout>
    );
  }

  if (invite === "invalid") {
    return (
      <AuthLayout title={t("auth.join.invalidTitle")} subtitle={t("auth.join.invalidText")}>
        <Actions left={<button type="button" className="btn text" onClick={() => navigate("/signup")}>{t("auth.createAccount")}</button>}>
          <button type="button" className="btn primary" onClick={() => navigate("/signin")}>{t("auth.signIn.submit")}</button>
        </Actions>
      </AuthLayout>
    );
  }

  return <SignUp invite={invite} inviteCode={code.toUpperCase()} />;
}

// ------------------------------------------------------------------------------------------------
// Forgot / reset password
// ------------------------------------------------------------------------------------------------

function Forgot() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState(new URLSearchParams(location.search).get("email") ?? "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!EMAIL.test(email.trim())) return setError(email.trim() ? t("auth.err.emailInvalid") : t("auth.err.emailRequired"));
    setBusy(true);
    try {
      await accountRequest("/api/auth/forgot-password", "POST", { email: email.trim() });
      setSent(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (sent) {
    return (
      <AuthLayout title={t("auth.forgotPage.sentTitle")} subtitle={t("auth.forgotPage.sentText", { email: email.trim() })}>
        <Banner tone="info">{t("auth.forgotPage.noMail")}</Banner>
        <Actions>
          <button type="button" className="btn primary" onClick={() => navigate(`/signin?email=${encodeURIComponent(email.trim())}`)}>{t("auth.backToSignIn")}</button>
        </Actions>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("auth.forgotPage.title")} subtitle={t("auth.forgotPage.subtitle")}>
      <form onSubmit={(e) => void submit(e)} noValidate>
        <TextField label={t("auth.email")} type="email" autoComplete="username" inputMode="email" autoFocus value={email} onChange={(v) => { setEmail(v); setError(null); }} error={error} />
        <Actions left={<button type="button" className="btn text" onClick={() => navigate("/signin")}>{t("auth.backToSignIn")}</button>}>
          <Primary busy={busy}>{t("auth.forgotPage.submit")}</Primary>
        </Actions>
      </form>
    </AuthLayout>
  );
}

function Reset() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const params = new URLSearchParams(useLocation().search);
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<Record<string, string | null>>({});
  const [banner, setBanner] = useState<string | null>(!email || !token ? t("auth.resetPage.badLink") : null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!passwordChecks(password).every((c) => c.ok)) return setErrors({ password: t("auth.err.passwordRules") });
    if (password !== confirm) return setErrors({ confirm: t("auth.err.passwordMismatch") });
    setBusy(true);
    setBanner(null);
    try {
      await accountRequest("/api/auth/reset-password", "POST", { email, token, newPassword: password });
      navigate(`/signin?email=${encodeURIComponent(email)}`, { replace: true, state: { notice: t("auth.resetPage.done") } });
    } catch (err) {
      const error = err as AuthError;
      if (error.field("password")) setErrors({ password: error.field("password")! });
      else setBanner(error.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout title={t("auth.resetPage.title")} aside={email ? <AccountChip email={email} onChange={() => navigate("/signin")} /> : undefined}>
      <form onSubmit={(e) => void submit(e)} noValidate>
        <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
        {banner && <Banner>{banner}</Banner>}
        <PasswordField label={t("auth.newPassword")} autoComplete="new-password" autoFocus value={password} onChange={(v) => { setPassword(v); setErrors({}); }} error={errors.password} />
        <PasswordRules password={password} />
        <PasswordField label={t("auth.confirmPassword")} autoComplete="new-password" value={confirm} onChange={(v) => { setConfirm(v); setErrors({}); }} error={errors.confirm} />
        <Actions left={<button type="button" className="btn text" onClick={() => navigate("/forgot-password")}>{t("auth.resetPage.newLink")}</button>}>
          <Primary busy={busy} disabled={!email || !token}>{t("auth.resetPage.submit")}</Primary>
        </Actions>
      </form>
    </AuthLayout>
  );
}

/** Everything a signed-out visitor can see, picked by the URL. */
export function AuthScreens() {
  const { pathname } = useLocation();
  if (pathname === "/signup") return <SignUp />;
  if (pathname.startsWith("/join/")) return <Join />;
  if (pathname === "/forgot-password") return <Forgot />;
  if (pathname === "/reset-password") return <Reset />;
  return <SignIn />;
}

// ------------------------------------------------------------------------------------------------
// Session expired — asked for over the app, so nothing on screen (e.g. unsaved design) is lost
// ------------------------------------------------------------------------------------------------

export function SessionExpiredDialog() {
  const { t } = useTranslation();
  const user = useAuth((s) => s.user);
  const login = useAuth((s) => s.login);
  const logout = useAuth((s) => s.logout);
  const dialogRef = useFocusTrap<HTMLDivElement>();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLDivElement>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!user) return logout();
    setBusy(true);
    setError(null);
    try {
      await login(user.email, password);
    } catch (err) {
      const error = err as AuthError;
      setError(error.status === 401 ? t("auth.err.wrongPassword") : error.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop session-backdrop">
      <div ref={dialogRef} className="modal session-dialog" role="alertdialog" aria-modal="true" aria-labelledby="session-title">
        <form onSubmit={(e) => void submit(e)} noValidate>
          <h2 id="session-title" className="session-title">{t("auth.session.title")}</h2>
          <p className="session-text">{t("auth.session.text")}</p>
          {user && (
            <div className="session-account">
              <Avatar text={initials(user)} seed={user.email} size={28} />
              <div>
                <strong>{displayNameOf(user)}</strong>
                <span>{user.email}</span>
              </div>
            </div>
          )}
          <input type="email" name="username" autoComplete="username" value={user?.email ?? ""} readOnly hidden />
          <div ref={inputRef}>
            <PasswordField label={t("auth.password")} autoComplete="current-password" autoFocus value={password} onChange={(v) => { setPassword(v); setError(null); }} error={error} />
          </div>
          <div className="auth-actions">
            <div>
              <button type="button" className="btn text" onClick={logout}>{t("auth.session.other")}</button>
            </div>
            <div>
              <Primary busy={busy}>{t("auth.session.submit")}</Primary>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
