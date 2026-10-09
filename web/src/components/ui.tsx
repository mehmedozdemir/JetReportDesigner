import { useEffect, useId, useState, type InputHTMLAttributes, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { create } from "zustand";
import { AlertCircle, Eye, EyeOff, X } from "lucide-react";

// ------------------------------------------------------------------------------------------------
// Text field — outlined, with a label that floats into the border (Material / Google sign-in style)
// ------------------------------------------------------------------------------------------------

type FieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "onChange" | "value"> & {
  label: string;
  value: string;
  onChange: (value: string) => void;
  /** Shown in red under the field; also marks the field invalid for screen readers. */
  error?: string | null;
  /** Shown in grey under the field when there is no error. */
  helper?: ReactNode;
  trailing?: ReactNode;
};

export function TextField({ label, value, onChange, error, helper, trailing, className, id, ...input }: FieldProps) {
  const auto = useId();
  const inputId = id ?? auto;
  const noteId = `${inputId}-note`;
  return (
    <div className={`tf${error ? " tf-error" : ""}${trailing ? " tf-has-trailing" : ""}${className ? ` ${className}` : ""}`}>
      <div className="tf-box">
        <input
          {...input}
          id={inputId}
          value={value}
          placeholder=" "
          aria-invalid={!!error}
          aria-describedby={error || helper ? noteId : undefined}
          onChange={(e) => onChange(e.target.value)}
        />
        <label htmlFor={inputId}>{label}</label>
        {trailing && <span className="tf-trailing">{trailing}</span>}
      </div>
      {(error || helper) && (
        <div id={noteId} className="tf-note" role={error ? "alert" : undefined}>
          {error ? (
            <>
              <AlertCircle size={14} /> {error}
            </>
          ) : (
            helper
          )}
        </div>
      )}
    </div>
  );
}

/** A password field with a show/hide toggle. */
export function PasswordField(props: Omit<FieldProps, "type" | "trailing">) {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);
  return (
    <TextField
      {...props}
      type={visible ? "text" : "password"}
      spellCheck={false}
      autoCapitalize="none"
      trailing={
        <button
          type="button"
          className="tf-icon-btn"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? t("auth.hidePassword") : t("auth.showPassword")}
          title={visible ? t("auth.hidePassword") : t("auth.showPassword")}
        >
          {visible ? <EyeOff size={18} /> : <Eye size={18} />}
        </button>
      }
    />
  );
}

/** The password rules, ticked off live as the person types (mirrors the server's Identity options). */
export function passwordChecks(password: string) {
  return [
    { key: "length", ok: password.length >= 8 },
    { key: "upper", ok: /[A-Z]/.test(password) },
    { key: "lower", ok: /[a-z]/.test(password) },
    { key: "digit", ok: /\d/.test(password) },
  ] as const;
}

export function PasswordRules({ password }: { password: string }) {
  const { t } = useTranslation();
  return (
    <ul className="pw-rules" aria-label={t("auth.rules.title")}>
      {passwordChecks(password).map((c) => (
        <li key={c.key} className={c.ok ? "ok" : undefined}>
          <span className="pw-dot" aria-hidden /> {t(`auth.rules.${c.key}`)}
          <span className="sr-only">{c.ok ? ` — ${t("auth.rules.met")}` : ""}</span>
        </li>
      ))}
    </ul>
  );
}

// ------------------------------------------------------------------------------------------------
// Avatar — initials on a colour picked from the email, so a person keeps the same colour everywhere
// ------------------------------------------------------------------------------------------------

const AVATAR_COLORS = ["#0b57d0", "#146c2e", "#b3261e", "#7c4dff", "#00639b", "#8c4a00", "#9c27b0", "#006a6a"];

export function Avatar({ text, seed, size = 32 }: { text: string; seed: string; size?: number }) {
  let h = 0;
  for (const ch of seed) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return (
    <span
      className="avatar"
      aria-hidden
      style={{ width: size, height: size, fontSize: Math.round(size * 0.42), background: AVATAR_COLORS[h % AVATAR_COLORS.length] }}
    >
      {text}
    </span>
  );
}

// ------------------------------------------------------------------------------------------------
// Switch — an on/off setting (instead of a checkbox) where the change applies immediately
// ------------------------------------------------------------------------------------------------

export function Switch({ checked, onChange, label, description }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string }) {
  const id = useId();
  return (
    <div className="switch-row">
      <label htmlFor={id} className="switch-text">
        <span>{label}</span>
        {description && <small>{description}</small>}
      </label>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        className={`switch${checked ? " on" : ""}`}
        onClick={() => onChange(!checked)}
      >
        <span className="switch-thumb" />
      </button>
    </div>
  );
}

// ------------------------------------------------------------------------------------------------
// Snackbar — short confirmation at the bottom of the screen ("Invite created · Copy link")
// ------------------------------------------------------------------------------------------------

interface Snack {
  id: number;
  text: string;
  action?: { label: string; run: () => void };
  tone?: "default" | "error";
}

const useSnacks = create<{ items: Snack[]; push(s: Omit<Snack, "id">): void; drop(id: number): void }>((set) => ({
  items: [],
  push: (s) => set((st) => ({ items: [...st.items.slice(-2), { ...s, id: Date.now() + Math.random() }] })),
  drop: (id) => set((st) => ({ items: st.items.filter((i) => i.id !== id) })),
}));

/** Show a short message at the bottom of the screen. */
export function notify(text: string, options: { action?: Snack["action"]; tone?: Snack["tone"] } = {}) {
  useSnacks.getState().push({ text, ...options });
}

function SnackItem({ snack }: { snack: Snack }) {
  const { t } = useTranslation();
  const drop = useSnacks((s) => s.drop);
  useEffect(() => {
    const timer = window.setTimeout(() => drop(snack.id), snack.action ? 8000 : 4500);
    return () => window.clearTimeout(timer);
  }, [snack, drop]);
  return (
    <div className={`snack${snack.tone === "error" ? " snack-error" : ""}`} role="status">
      <span>{snack.text}</span>
      {snack.action && (
        <button
          className="snack-action"
          onClick={() => {
            snack.action!.run();
            drop(snack.id);
          }}
        >
          {snack.action.label}
        </button>
      )}
      <button className="snack-close" onClick={() => drop(snack.id)} aria-label={t("common.close")}>
        <X size={16} />
      </button>
    </div>
  );
}

/** Mounted once at the app root. */
export function SnackbarHost() {
  const items = useSnacks((s) => s.items);
  return (
    <div className="snack-stack" aria-live="polite">
      {items.map((s) => (
        <SnackItem key={s.id} snack={s} />
      ))}
    </div>
  );
}

/** Copy to the clipboard and say so. */
export async function copyText(text: string, done: string) {
  try {
    await navigator.clipboard.writeText(text);
    notify(done);
  } catch {
    notify(text);
  }
}
